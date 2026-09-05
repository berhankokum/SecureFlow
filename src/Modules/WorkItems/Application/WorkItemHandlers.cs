using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Activity;
using SecureFlow.Contracts.Identity;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Application.Models;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public sealed record CreateWorkItemCommand(
    Guid ProjectId,
    WorkItemType Type,
    string Title,
    string? Description,
    WorkItemPriority Priority,
    DateOnly? DueDate,
    Guid ReporterId);

public sealed record UpdateWorkItemCommand(
    Guid WorkItemId,
    WorkItemType Type,
    string Title,
    string? Description,
    WorkItemPriority Priority,
    DateOnly? DueDate,
    Guid CurrentUserId);

internal static class WorkItemModelMapper
{
    public static WorkItemSummary ToSummary(
        WorkItem workItem,
        WorkItemProjectContext project,
        WorkflowStateSummary state,
        string reporterEmail,
        string? assigneeEmail,
        IReadOnlyList<WorkItemLabelSummary>? labels = null) =>
        new(
            workItem.Id,
            workItem.OrganizationId,
            workItem.ProjectId,
            project.ProjectKey,
            workItem.Number,
            $"{project.ProjectKey}-{workItem.Number}",
            workItem.Type,
            workItem.Title,
            workItem.Description,
            workItem.Priority,
            workItem.WorkflowStateId,
            state.Name,
            state.Category,
            workItem.ReporterId,
            reporterEmail,
            workItem.AssigneeId,
            assigneeEmail,
            workItem.DueDate,
            workItem.CreatedAt,
            workItem.UpdatedAt,
            workItem.ArchivedAt,
            project.Permissions,
            labels ?? []);

    public static async Task<IReadOnlyDictionary<Guid, WorkflowStateSummary>> LoadStatesAsync(
        IWorkItemsDbContext dbContext,
        IEnumerable<Guid> stateIds,
        CancellationToken cancellationToken)
    {
        var ids = stateIds.Distinct().ToArray();
        var states = await dbContext.WorkflowStates
            .AsNoTracking()
            .Where(state => ids.Contains(state.Id))
            .ToArrayAsync(cancellationToken);
        return states.ToDictionary(
            state => state.Id,
            state => new WorkflowStateSummary(
                state.Id,
                state.Name,
                WorkflowHandlers.ToApiValue(state.Category),
                state.Position));
    }

    public static async Task<ILookup<Guid, WorkItemLabelSummary>> LoadLabelsAsync(
        IWorkItemsDbContext dbContext, Guid projectId, Guid? workItemId, CancellationToken cancellationToken)
    {
        var labels = await (
            from assignment in dbContext.WorkItemLabels.AsNoTracking()
            join label in dbContext.Labels.AsNoTracking() on assignment.LabelId equals label.Id
            where label.ProjectId == projectId && (workItemId == null || assignment.WorkItemId == workItemId)
            orderby label.Name
            select new
            {
                assignment.WorkItemId,
                Label = new WorkItemLabelSummary(
                    label.Id, label.Name, assignment.AssignedBy, assignment.AssignedAt)
            })
            .ToArrayAsync(cancellationToken);
        return labels.ToLookup(item => item.WorkItemId, item => item.Label);
    }
}

public sealed class FindProjectScopeHandler(IWorkItemsDbContext dbContext)
{
    public Task<Guid?> HandleAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.FindProjectOrganizationIdAsync(projectId, cancellationToken);
}

public sealed class FindWorkItemScopeHandler(IWorkItemsDbContext dbContext)
{
    public Task<WorkItemScope?> HandleAsync(Guid workItemId, CancellationToken cancellationToken) =>
        dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == workItemId)
            .Select(workItem => new WorkItemScope(workItem.ProjectId, workItem.OrganizationId))
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed class CreateWorkItemHandler(
    IWorkItemsDbContext dbContext,
    IUserDirectory userDirectory,
    IActivityWriter activityWriter,
    IRealtimePublisher realtimePublisher,
    IClock clock)
{
    public async Task<Result<WorkItemSummary>> HandleAsync(
        CreateWorkItemCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        if (!await dbContext.LockProjectAsync(command.ProjectId, cancellationToken))
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.NotFound);
        }

        var project = await dbContext.GetWorkItemProjectContextAsync(
            command.ProjectId,
            command.ReporterId,
            cancellationToken);
        if (project?.HasProjectAccess != true)
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.NotFound);
        }

        if (!project.Permissions.Contains(Permissions.WorkItemCreate, StringComparer.Ordinal))
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.Forbidden);
        }

        if (project.IsArchived)
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.ProjectArchived);
        }

        var previousNumber = await dbContext.WorkItems
            .Where(workItem => workItem.ProjectId == command.ProjectId)
            .MaxAsync(workItem => (long?)workItem.Number, cancellationToken) ?? 0;
        if (previousNumber == long.MaxValue)
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.NumberExhausted);
        }


        var initialStateEntity = await (
            from workflow in dbContext.Workflows.AsNoTracking()
            join state in dbContext.WorkflowStates.AsNoTracking()
                on workflow.Id equals state.WorkflowId
            where workflow.OrganizationId == project.OrganizationId
            orderby state.Position
            select state)
            .FirstOrDefaultAsync(cancellationToken);
        if (initialStateEntity is null)
        {
            return Result.Failure<WorkItemSummary>(WorkflowErrors.MissingConfiguration);
        }

        var initialState = new WorkflowStateSummary(
            initialStateEntity.Id,
            initialStateEntity.Name,
            WorkflowHandlers.ToApiValue(initialStateEntity.Category),
            initialStateEntity.Position);

        var now = clock.UtcNow;
        var workItem = WorkItem.Create(
            project.OrganizationId,
            project.ProjectId,
            previousNumber + 1,
            command.Type,
            command.Title,
            command.Description,
            command.Priority,
            initialState.Id,
            command.ReporterId,
            command.DueDate,
            DateOnly.FromDateTime(now.UtcDateTime),
            now);
        dbContext.WorkItems.Add(workItem);
        var activityId = activityWriter.Add(new ActivityWriteRequest(
            workItem.OrganizationId,
            workItem.ProjectId,
            workItem.Id,
            command.ReporterId,
            ActivityTypes.WorkItemCreated,
            new Dictionary<string, string?>
            {
                ["workItemKey"] = $"{project.ProjectKey}-{workItem.Number}",
            },
            now));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await realtimePublisher.PublishWorkItemUpdatedAsync(
                new WorkItemUpdatedRealtimeEvent(
                    activityId,
                    workItem.OrganizationId,
                    workItem.ProjectId,
                    workItem.Id,
                    now),
                cancellationToken);
            var reporter = await userDirectory.FindByIdAsync(command.ReporterId, cancellationToken);
            return Result.Success(WorkItemModelMapper.ToSummary(
                workItem,
                project,
                initialState,
                reporter?.Email ?? "Bilinmeyen kullanıcı",
                assigneeEmail: null));
        }
        catch (DbUpdateException exception) when (IsNumberConflict(exception))
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.NumberConflict);
        }
    }

    private static bool IsNumberConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_WorkItems_ProjectId_Number",
        };
}

public sealed class ListWorkItemsHandler(IWorkItemsDbContext dbContext, IUserDirectory userDirectory)
{
    public async Task<Result<IReadOnlyList<WorkItemSummary>>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.GetWorkItemProjectContextAsync(
            projectId,
            currentUserId,
            cancellationToken);
        if (project?.HasProjectAccess != true)
        {
            return Result.Failure<IReadOnlyList<WorkItemSummary>>(WorkItemErrors.NotFound);
        }

        var workItems = await dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.ProjectId == projectId)
            .OrderBy(workItem => workItem.ArchivedAt != null)
            .ThenByDescending(workItem => workItem.Number)
            .ToArrayAsync(cancellationToken);
        var reporterEmails = await userDirectory.GetEmailsByUserIdsAsync(
            workItems.SelectMany(workItem => workItem.AssigneeId is { } assigneeId
                    ? new[] { workItem.ReporterId, assigneeId }
                    : new[] { workItem.ReporterId })
                .Distinct().ToArray(),
            cancellationToken);

        var labels = await WorkItemModelMapper.LoadLabelsAsync(dbContext, projectId, null, cancellationToken);
        var states = await WorkItemModelMapper.LoadStatesAsync(
            dbContext,
            workItems.Select(workItem => workItem.WorkflowStateId),
            cancellationToken);
        return Result.Success<IReadOnlyList<WorkItemSummary>>(
            workItems.Select(workItem => WorkItemModelMapper.ToSummary(
                workItem,
                project,
                states[workItem.WorkflowStateId],
                reporterEmails.GetValueOrDefault(workItem.ReporterId, "Bilinmeyen kullanıcı"),
                workItem.AssigneeId is { } assigneeId
                    ? reporterEmails.GetValueOrDefault(assigneeId, "Bilinmeyen kullanıcı")
                    : null,
                labels[workItem.Id].ToArray())).ToArray());
    }
}

public sealed class GetWorkItemHandler(IWorkItemsDbContext dbContext, IUserDirectory userDirectory)
{
    public async Task<Result<WorkItemSummary>> HandleAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var workItem = await dbContext.WorkItems
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken);
        if (workItem is null)
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.NotFound);
        }

        var project = await dbContext.GetWorkItemProjectContextAsync(
            workItem.ProjectId,
            currentUserId,
            cancellationToken);
        if (project?.HasProjectAccess != true)
        {
            return Result.Failure<WorkItemSummary>(WorkItemErrors.NotFound);
        }

        var emails = await userDirectory.GetEmailsByUserIdsAsync(
            workItem.AssigneeId is { } assigneeId
                ? [workItem.ReporterId, assigneeId]
                : [workItem.ReporterId],
            cancellationToken);
        var labels = await WorkItemModelMapper.LoadLabelsAsync(
            dbContext, workItem.ProjectId, workItemId, cancellationToken);
        var states = await WorkItemModelMapper.LoadStatesAsync(
            dbContext,
            [workItem.WorkflowStateId],
            cancellationToken);
        return Result.Success(WorkItemModelMapper.ToSummary(
            workItem,
            project,
            states[workItem.WorkflowStateId],
            emails.GetValueOrDefault(workItem.ReporterId, "Bilinmeyen kullanıcı"),
            workItem.AssigneeId is { } assignedUserId
                ? emails.GetValueOrDefault(assignedUserId, "Bilinmeyen kullanıcı")
                : null,
            labels[workItemId].ToArray()));
    }
}

public sealed class UpdateWorkItemHandler(
    IWorkItemsDbContext dbContext,
    IRealtimePublisher realtimePublisher,
    IClock clock)
{
    public async Task<Result> HandleAsync(UpdateWorkItemCommand command, CancellationToken cancellationToken)
    {
        var scope = await dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == command.WorkItemId)
            .Select(workItem => new { workItem.ProjectId })
            .SingleOrDefaultAsync(cancellationToken);
        if (scope is null)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        if (!await dbContext.LockProjectAsync(scope.ProjectId, cancellationToken))
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        var project = await dbContext.GetWorkItemProjectContextAsync(
            scope.ProjectId,
            command.CurrentUserId,
            cancellationToken);
        if (project?.HasProjectAccess != true)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        if (!project.Permissions.Contains(Permissions.WorkItemUpdate, StringComparer.Ordinal))
        {
            return Result.Failure(WorkItemErrors.Forbidden);
        }

        if (project.IsArchived)
        {
            return Result.Failure(WorkItemErrors.ProjectArchived);
        }

        var workItem = await dbContext.WorkItems.SingleAsync(
            item => item.Id == command.WorkItemId,
            cancellationToken);
        if (workItem.IsArchived)
        {
            return Result.Failure(WorkItemErrors.Archived);
        }

        var now = clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);
        if (command.DueDate != workItem.DueDate && command.DueDate < currentDate)
        {
            return Result.Failure(WorkItemErrors.InvalidDueDate);
        }

        workItem.Update(
            command.Type,
            command.Title,
            command.Description,
            command.Priority,
            command.DueDate,
            currentDate,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimePublisher.PublishWorkItemUpdatedAsync(
            new WorkItemUpdatedRealtimeEvent(
                Guid.NewGuid(),
                workItem.OrganizationId,
                workItem.ProjectId,
                workItem.Id,
                now),
            cancellationToken);
        return Result.Success();
    }
}

public sealed class ArchiveWorkItemHandler(
    IWorkItemsDbContext dbContext,
    IRealtimePublisher realtimePublisher,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var scope = await dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == workItemId)
            .Select(workItem => new { workItem.ProjectId })
            .SingleOrDefaultAsync(cancellationToken);
        if (scope is null)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        if (!await dbContext.LockProjectAsync(scope.ProjectId, cancellationToken))
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        var project = await dbContext.GetWorkItemProjectContextAsync(
            scope.ProjectId,
            currentUserId,
            cancellationToken);
        if (project?.HasProjectAccess != true)
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        if (!project.Permissions.Contains(Permissions.WorkItemArchive, StringComparer.Ordinal))
        {
            return Result.Failure(WorkItemErrors.Forbidden);
        }

        if (project.IsArchived)
        {
            return Result.Failure(WorkItemErrors.ProjectArchived);
        }

        var workItem = await dbContext.WorkItems.SingleAsync(
            item => item.Id == workItemId,
            cancellationToken);
        var wasArchived = workItem.IsArchived;
        var now = clock.UtcNow;
        workItem.Archive(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (!wasArchived)
        {
            await realtimePublisher.PublishWorkItemUpdatedAsync(
                new WorkItemUpdatedRealtimeEvent(
                    Guid.NewGuid(),
                    workItem.OrganizationId,
                    workItem.ProjectId,
                    workItem.Id,
                    now),
                cancellationToken);
        }
        return Result.Success();
    }
}
