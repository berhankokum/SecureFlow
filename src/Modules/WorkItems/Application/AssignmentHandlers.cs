using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Activity;
using SecureFlow.Contracts.Organizations;
using SecureFlow.Contracts.Projects;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public static class AssignmentErrors
{
    public static readonly Error InvalidAssignee = new(
        "Assignment.InvalidAssignee",
        "Atanabilir kullanıcı bulunamadı.");
}

public sealed class AssignmentHandler(
    IWorkItemsDbContext dbContext,
    WorkItemPrecondition precondition,
    IActivityWriter activityWriter,
    INotificationWriter notificationWriter,
    IRealtimePublisher realtimePublisher,
    IClock clock)
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

        var previousAssigneeId = workItem.AssigneeId;
        if (precondition.Check(workItem.Version) is { } versionError) return Result.Failure(versionError);
        var now = clock.UtcNow;
        Guid? activityId = null;
        if (workItem.Unassign(now))
        {
            activityId = activityWriter.Add(new ActivityWriteRequest(
                workItem.OrganizationId,
                workItem.ProjectId,
                workItem.Id,
                currentUserId,
                ActivityTypes.WorkItemUnassigned,
                new Dictionary<string, string?>
                {
                    ["previousAssigneeId"] = previousAssigneeId?.ToString(),
                },
                now));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (activityId is { } unassignmentEventId)
        {
            await realtimePublisher.PublishWorkItemUpdatedAsync(
                new WorkItemUpdatedRealtimeEvent(
                    unassignmentEventId,
                    workItem.OrganizationId,
                    workItem.ProjectId,
                    workItem.Id,
                    now),
                cancellationToken);
        }
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

        var previousAssigneeId = workItem.AssigneeId;
        if (precondition.Check(workItem.Version) is { } versionError) return Result.Failure(versionError);
        var now = clock.UtcNow;
        Guid? activityId = null;
        Guid? notificationId = null;
        if (workItem.Assign(assigneeId, now))
        {
            activityId = activityWriter.Add(new ActivityWriteRequest(
                workItem.OrganizationId,
                workItem.ProjectId,
                workItem.Id,
                currentUserId,
                ActivityTypes.WorkItemAssigned,
                new Dictionary<string, string?>
                {
                    ["assigneeId"] = assigneeId.ToString(),
                    ["previousAssigneeId"] = previousAssigneeId?.ToString(),
                },
                now));
            if (assigneeId != currentUserId)
            {
                notificationId = notificationWriter.Add(new NotificationWriteRequest(
                    workItem.OrganizationId,
                    assigneeId,
                    NotificationTypes.WorkItemAssigned,
                    "WorkItem size atandı",
                    $"{projectResult.Value.ProjectKey}-{workItem.Number} size atandı.",
                    NotificationResourceTypes.WorkItem,
                    workItem.Id,
                    activityId.Value,
                    now));
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (activityId is { } assignmentEventId)
        {
            await realtimePublisher.PublishWorkItemUpdatedAsync(
                new WorkItemUpdatedRealtimeEvent(
                    assignmentEventId,
                    workItem.OrganizationId,
                    workItem.ProjectId,
                    workItem.Id,
                    now),
                cancellationToken);
        }

        if (notificationId is { } createdNotificationId)
        {
            await realtimePublisher.PublishNotificationCreatedAsync(
                assigneeId,
                new NotificationCreatedRealtimeEvent(createdNotificationId, createdNotificationId, now),
                cancellationToken);
        }
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
    IActivityWriter activityWriter,
    IRealtimePublicationBuffer realtimeBuffer,
    IClock clock) : IOrganizationMembershipRemovalCleanup
{
    public async Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        // FOR UPDATE forces a concurrent assignment version to become visible or
        // raises a serializable conflict; a snapshot-only SELECT could miss it.
        var workItems = await dbContext.WorkItems
            .FromSqlInterpolated(
                $"""SELECT *, xmin FROM "WorkItems" WHERE "OrganizationId" = {organizationId} AND "AssigneeId" = {userId} AND "ArchivedAt" IS NULL FOR UPDATE""")
            .ToArrayAsync(cancellationToken);
        var now = clock.UtcNow;
        foreach (var workItem in workItems)
        {
            if (workItem.Unassign(now))
            {
                var activityId = AutomaticUnassignmentActivity.Add(
                    activityWriter, workItem, actorId, userId, now);
                realtimeBuffer.Enqueue(new WorkItemUpdatedRealtimeEvent(
                    activityId,
                    workItem.OrganizationId,
                    workItem.ProjectId,
                    workItem.Id,
                    now));
            }
        }
    }
}

public sealed class WorkItemProjectMembershipRemovalCleanup(
    IWorkItemsDbContext dbContext,
    IActivityWriter activityWriter,
    IRealtimePublicationBuffer realtimeBuffer,
    IClock clock) : IProjectMembershipRemovalCleanup
{
    public async Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid projectId,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var workItems = await dbContext.WorkItems
            .Where(item => item.OrganizationId == organizationId
                && item.ProjectId == projectId
                && item.AssigneeId == userId
                && item.ArchivedAt == null)
            .ToArrayAsync(cancellationToken);
        var now = clock.UtcNow;
        foreach (var workItem in workItems)
        {
            if (workItem.Unassign(now))
            {
                var activityId = AutomaticUnassignmentActivity.Add(
                    activityWriter, workItem, actorId, userId, now);
                realtimeBuffer.Enqueue(new WorkItemUpdatedRealtimeEvent(
                    activityId,
                    workItem.OrganizationId,
                    workItem.ProjectId,
                    workItem.Id,
                    now));
            }
        }
    }
}

internal static class AutomaticUnassignmentActivity
{
    public static Guid Add(
        IActivityWriter activityWriter,
        WorkItem workItem,
        Guid actorId,
        Guid previousAssigneeId,
        DateTimeOffset occurredAt) =>
        activityWriter.Add(new ActivityWriteRequest(
            workItem.OrganizationId,
            workItem.ProjectId,
            workItem.Id,
            actorId,
            ActivityTypes.WorkItemUnassigned,
            new Dictionary<string, string?>
            {
                ["previousAssigneeId"] = previousAssigneeId.ToString(),
            },
            occurredAt));
}
