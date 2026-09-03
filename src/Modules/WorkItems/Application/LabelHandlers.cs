using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Application.Models;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public static class LabelErrors
{
    public static readonly Error NotFound = new("Labels.NotFound", "Label bulunamadı.");
    public static readonly Error DuplicateName = new("Labels.DuplicateName", "Bu Project içinde aynı isimli Label zaten var.");
}

public sealed class LabelHandlers(IWorkItemsDbContext dbContext, IClock clock)
{
    public Task<WorkItemScope?> FindScopeAsync(Guid labelId, CancellationToken cancellationToken) =>
        dbContext.Labels.AsNoTracking().Where(label => label.Id == labelId)
            .Select(label => new WorkItemScope(label.ProjectId, label.OrganizationId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Result<IReadOnlyList<LabelSummary>>> ListAsync(
        Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await dbContext.GetWorkItemProjectContextAsync(projectId, userId, cancellationToken);
        if (project?.HasProjectAccess != true)
            return Result.Failure<IReadOnlyList<LabelSummary>>(LabelErrors.NotFound);

        var labels = await dbContext.Labels.AsNoTracking()
            .Where(label => label.ProjectId == projectId && label.OrganizationId == project.OrganizationId)
            .OrderBy(label => label.Name).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyList<LabelSummary>>(labels.Select(LabelSummary.From).ToArray());
    }

    public async Task<Result<LabelSummary>> CreateAsync(
        Guid projectId, string name, Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var access = await RequireMutableProjectAsync(projectId, userId, Permissions.LabelCreate, cancellationToken);
        if (access.IsFailure) return Result.Failure<LabelSummary>(access.Error);

        var label = Label.Create(access.Value.OrganizationId, projectId, name, userId, clock.UtcNow);
        dbContext.Labels.Add(label);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(LabelSummary.From(label));
        }
        catch (DbUpdateException exception) when (IsNameConflict(exception))
        {
            return Result.Failure<LabelSummary>(LabelErrors.DuplicateName);
        }
    }

    public async Task<Result> UpdateAsync(Guid labelId, string name, Guid userId, CancellationToken cancellationToken)
    {
        var scope = await FindScopeAsync(labelId, cancellationToken);
        if (scope is null) return Result.Failure(LabelErrors.NotFound);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var access = await RequireMutableProjectAsync(scope.ProjectId, userId, Permissions.LabelUpdate, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);

        var label = await dbContext.Labels.SingleOrDefaultAsync(item => item.Id == labelId, cancellationToken);
        if (label is null) return Result.Failure(LabelErrors.NotFound);
        label.Update(name, clock.UtcNow);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (IsNameConflict(exception))
        {
            return Result.Failure(LabelErrors.DuplicateName);
        }
    }

    public async Task<Result> DeleteAsync(Guid labelId, Guid userId, CancellationToken cancellationToken)
    {
        var scope = await FindScopeAsync(labelId, cancellationToken);
        if (scope is null) return Result.Failure(LabelErrors.NotFound);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var access = await RequireMutableProjectAsync(scope.ProjectId, userId, Permissions.LabelDelete, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);

        var label = await dbContext.Labels.SingleOrDefaultAsync(item => item.Id == labelId, cancellationToken);
        if (label is null) return Result.Failure(LabelErrors.NotFound);
        dbContext.Labels.Remove(label);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetAssignmentAsync(
        Guid workItemId, Guid labelId, Guid userId, bool assigned, CancellationToken cancellationToken)
    {
        var projectId = await dbContext.WorkItems.AsNoTracking().Where(item => item.Id == workItemId)
            .Select(item => (Guid?)item.ProjectId).SingleOrDefaultAsync(cancellationToken);
        if (projectId is null) return Result.Failure(WorkItemErrors.NotFound);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var access = await RequireMutableProjectAsync(projectId.Value, userId, Permissions.LabelAssign, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);

        var workItem = await dbContext.WorkItems.SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken);
        if (workItem is null) return Result.Failure(WorkItemErrors.NotFound);
        if (workItem.IsArchived) return Result.Failure(WorkItemErrors.Archived);

        // Scope the label lookup to the already authorized WorkItem; foreign IDs are never disclosed.
        var label = await dbContext.Labels.SingleOrDefaultAsync(item => item.Id == labelId
            && item.ProjectId == workItem.ProjectId && item.OrganizationId == workItem.OrganizationId, cancellationToken);
        if (label is null) return Result.Failure(LabelErrors.NotFound);

        var assignment = await dbContext.WorkItemLabels.SingleOrDefaultAsync(
            item => item.WorkItemId == workItemId && item.LabelId == labelId, cancellationToken);
        if (assigned && assignment is null)
            dbContext.WorkItemLabels.Add(WorkItemLabel.Create(workItem, label, userId, clock.UtcNow));
        else if (!assigned && assignment is not null)
            dbContext.WorkItemLabels.Remove(assignment);

        // All label/work-item mutations serialize on their Project row. Repeated requests are no-ops.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<WorkItemProjectContext>> RequireMutableProjectAsync(
        Guid projectId, Guid userId, string permission, CancellationToken cancellationToken)
    {
        if (!await dbContext.LockProjectAsync(projectId, cancellationToken))
            return Result.Failure<WorkItemProjectContext>(LabelErrors.NotFound);
        var project = await dbContext.GetWorkItemProjectContextAsync(projectId, userId, cancellationToken);
        if (project?.HasProjectAccess != true)
            return Result.Failure<WorkItemProjectContext>(LabelErrors.NotFound);
        if (!project.Permissions.Contains(permission, StringComparer.Ordinal))
            return Result.Failure<WorkItemProjectContext>(WorkItemErrors.Forbidden);
        if (project.IsArchived)
            return Result.Failure<WorkItemProjectContext>(WorkItemErrors.ProjectArchived);
        return Result.Success(project);
    }

    private static bool IsNameConflict(DbUpdateException exception) => exception.InnerException is PostgresException
    {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "UX_Labels_ProjectId_NormalizedName",
    };
}
