using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Organizations;
using SecureFlow.Contracts.Projects;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public static class AssignmentErrors
{
    public static readonly Error InvalidAssignee = new(
        "Assignment.InvalidAssignee",
        "Atanabilir kullanıcı bulunamadı.");
}

public sealed class AssignmentHandler(IWorkItemsDbContext dbContext, IClock clock)
{
    public Task<Result> AssignOtherAsync(
        Guid workItemId,
        Guid assigneeId,
        Guid currentUserId,
        CancellationToken cancellationToken) =>
        AssignAsync(workItemId, assigneeId, currentUserId, Permissions.WorkItemAssign, cancellationToken);

    public Task<Result> SelfAssignAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken) =>
        AssignAsync(
            workItemId,
            currentUserId,
            currentUserId,
            Permissions.WorkItemSelfAssign,
            cancellationToken);

    public async Task<Result> UnassignAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var scope = await FindProjectIdAsync(workItemId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var projectResult = await LoadMutableProjectAsync(scope.Value, currentUserId, cancellationToken);
        if (projectResult.IsFailure)
        {
            return Result.Failure(projectResult.Error);
        }

        var workItem = await dbContext.WorkItems.SingleOrDefaultAsync(
            item => item.Id == workItemId,
            cancellationToken);
        if (workItem is null)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        if (workItem.IsArchived)
        {
            return Result.Failure(WorkItemErrors.Archived);
        }

        var permissions = projectResult.Value.Permissions;
        var canUnassign = workItem.AssigneeId == currentUserId
            ? permissions.Contains(Permissions.WorkItemSelfAssign, StringComparer.Ordinal)
                || permissions.Contains(Permissions.WorkItemAssign, StringComparer.Ordinal)
            : permissions.Contains(Permissions.WorkItemAssign, StringComparer.Ordinal);
        if (workItem.AssigneeId is null)
        {
            canUnassign = permissions.Contains(Permissions.WorkItemSelfAssign, StringComparer.Ordinal)
                || permissions.Contains(Permissions.WorkItemAssign, StringComparer.Ordinal);
        }

        if (!canUnassign)
        {
            return Result.Failure(WorkItemErrors.Forbidden);
        }

        workItem.Unassign(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> AssignAsync(
        Guid workItemId,
        Guid assigneeId,
        Guid currentUserId,
        string requiredPermission,
        CancellationToken cancellationToken)
    {
        var scope = await FindProjectIdAsync(workItemId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var projectResult = await LoadMutableProjectAsync(scope.Value, currentUserId, cancellationToken);
        if (projectResult.IsFailure)
        {
            return Result.Failure(projectResult.Error);
        }

        if (!projectResult.Value.Permissions.Contains(requiredPermission, StringComparer.Ordinal))
        {
            return Result.Failure(WorkItemErrors.Forbidden);
        }

        var workItem = await dbContext.WorkItems.SingleOrDefaultAsync(
            item => item.Id == workItemId,
            cancellationToken);
        if (workItem is null)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        if (workItem.IsArchived)
        {
            return Result.Failure(WorkItemErrors.Archived);
        }

        if (!await dbContext.LockOrganizationMemberAsync(
                workItem.OrganizationId,
                assigneeId,
                cancellationToken)
            || !await dbContext.CanUserAccessProjectAsync(
                workItem.ProjectId,
                workItem.OrganizationId,
                assigneeId,
                cancellationToken))
        {
            return Result.Failure(AssignmentErrors.InvalidAssignee);
        }

        workItem.Assign(assigneeId, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private Task<Guid?> FindProjectIdAsync(Guid workItemId, CancellationToken cancellationToken) =>
        dbContext.WorkItems.AsNoTracking()
            .Where(item => item.Id == workItemId)
            .Select(item => (Guid?)item.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Result<WorkItemProjectContext>> LoadMutableProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.LockProjectAsync(projectId, cancellationToken))
        {
            return Result.Failure<WorkItemProjectContext>(WorkItemErrors.NotFound);
        }

        var project = await dbContext.GetWorkItemProjectContextAsync(
            projectId,
            currentUserId,
            cancellationToken);
        if (project?.HasProjectAccess != true)
        {
            return Result.Failure<WorkItemProjectContext>(WorkItemErrors.NotFound);
        }

        return project.IsArchived
            ? Result.Failure<WorkItemProjectContext>(WorkItemErrors.ProjectArchived)
            : Result.Success(project);
    }
}

public sealed class WorkItemOrganizationMembershipRemovalCleanup(
    IWorkItemsDbContext dbContext,
    IClock clock) : IOrganizationMembershipRemovalCleanup
{
    public async Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var workItems = await dbContext.WorkItems
            .Where(item => item.OrganizationId == organizationId
                && item.AssigneeId == userId
                && item.ArchivedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var workItem in workItems)
        {
            workItem.Unassign(clock.UtcNow);
        }
    }
}

public sealed class WorkItemProjectMembershipRemovalCleanup(
    IWorkItemsDbContext dbContext,
    IClock clock) : IProjectMembershipRemovalCleanup
{
    public async Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var workItems = await dbContext.WorkItems
            .Where(item => item.OrganizationId == organizationId
                && item.ProjectId == projectId
                && item.AssigneeId == userId
                && item.ArchivedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var workItem in workItems)
        {
            workItem.Unassign(clock.UtcNow);
        }
    }
}
