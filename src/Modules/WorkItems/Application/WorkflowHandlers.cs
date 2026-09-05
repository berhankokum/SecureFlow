using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Activity;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Application.Models;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Application;

public static class WorkflowErrors
{
    public static readonly Error MissingConfiguration = new(
        "Workflow.MissingConfiguration",
        "Workflow yapılandırması bulunamadı.");
    public static readonly Error InvalidTransition = new(
        "Workflow.InvalidWorkflowTransition",
        "Geçersiz workflow transition.");
}

public sealed class WorkflowHandlers(
    IWorkItemsDbContext dbContext,
    IActivityWriter activityWriter,
    INotificationWriter notificationWriter,
    IRealtimePublisher realtimePublisher,
    IClock clock)
{
    public async Task<Result<WorkflowSummary>> GetAsync(
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
            return Result.Failure<WorkflowSummary>(WorkItemErrors.NotFound);
        }

        var workflow = await dbContext.Workflows
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrganizationId == project.OrganizationId,
                cancellationToken);
        if (workflow is null)
        {
            return Result.Failure<WorkflowSummary>(WorkflowErrors.MissingConfiguration);
        }

        var states = await dbContext.WorkflowStates
            .AsNoTracking()
            .Where(state => state.WorkflowId == workflow.Id)
            .OrderBy(state => state.Position)
            .ToArrayAsync(cancellationToken);
        var transitions = await dbContext.WorkflowTransitions
            .AsNoTracking()
            .Where(transition => transition.WorkflowId == workflow.Id)
            .OrderBy(transition => transition.FromStateId)
            .ThenBy(transition => transition.ToStateId)
            .ToArrayAsync(cancellationToken);

        return Result.Success(new WorkflowSummary(
            workflow.Id,
            workflow.OrganizationId,
            workflow.Name,
            states.Select(state => new WorkflowStateSummary(
                state.Id,
                state.Name,
                ToApiValue(state.Category),
                state.Position)).ToArray(),
            transitions.Select(transition => new WorkflowTransitionSummary(
                transition.Id,
                transition.FromStateId,
                transition.ToStateId,
                transition.RequiredPermission)).ToArray()));
    }

    public async Task<Result> TransitionAsync(
        Guid workItemId,
        Guid toStateId,
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

        if (!project.Permissions.Contains(Permissions.WorkItemTransition, StringComparer.Ordinal))
        {
            return Result.Failure(WorkItemErrors.Forbidden);
        }

        if (project.IsArchived)
        {
            return Result.Failure(WorkItemErrors.ProjectArchived);
        }

        if (!await dbContext.LockWorkItemAsync(workItemId, cancellationToken))
        {
            return Result.Failure(WorkItemErrors.NotFound);
        }

        var workItem = await dbContext.WorkItems.SingleAsync(
            item => item.Id == workItemId,
            cancellationToken);
        if (workItem.IsArchived)
        {
            return Result.Failure(WorkItemErrors.Archived);
        }

        var transition = await (
            from candidate in dbContext.WorkflowTransitions
            join workflow in dbContext.Workflows on candidate.WorkflowId equals workflow.Id
            where workflow.OrganizationId == workItem.OrganizationId
                && candidate.FromStateId == workItem.WorkflowStateId
                && candidate.ToStateId == toStateId
            select candidate)
            .SingleOrDefaultAsync(cancellationToken);
        if (transition is null)
        {
            return Result.Failure(WorkflowErrors.InvalidTransition);
        }

        if (transition.RequiredPermission is { } requiredPermission
            && !project.Permissions.Contains(requiredPermission, StringComparer.Ordinal))
        {
            return Result.Failure(WorkItemErrors.Forbidden);
        }

        var states = await dbContext.WorkflowStates
            .AsNoTracking()
            .Where(state => state.Id == transition.FromStateId || state.Id == transition.ToStateId)
            .ToDictionaryAsync(state => state.Id, state => state.Name, cancellationToken);
        if (!states.TryGetValue(transition.FromStateId, out var fromState)
            || !states.TryGetValue(transition.ToStateId, out var toState))
        {
            return Result.Failure(WorkflowErrors.MissingConfiguration);
        }

        var now = clock.UtcNow;
        workItem.Transition(transition, now);
        var activityId = activityWriter.Add(new ActivityWriteRequest(
            workItem.OrganizationId,
            workItem.ProjectId,
            workItem.Id,
            currentUserId,
            ActivityTypes.WorkItemTransitioned,
            new Dictionary<string, string?>
            {
                ["fromState"] = fromState,
                ["toState"] = toState,
            },
            now));
        var recipients = new Guid?[] { workItem.ReporterId, workItem.AssigneeId }
            .OfType<Guid>()
            .Distinct()
            .Where(userId => userId != currentUserId);
        var createdNotifications = new List<(Guid RecipientId, Guid NotificationId)>();
        foreach (var recipientId in recipients)
        {
            if (!await dbContext.CanUserAccessProjectAsync(
                    workItem.ProjectId,
                    workItem.OrganizationId,
                    recipientId,
                    cancellationToken))
            {
                continue;
            }

            var notificationId = notificationWriter.Add(new NotificationWriteRequest(
                workItem.OrganizationId,
                recipientId,
                NotificationTypes.WorkItemStatusChanged,
                "WorkItem durumu değişti",
                $"{project.ProjectKey}-{workItem.Number}: {fromState} → {toState}.",
                NotificationResourceTypes.WorkItem,
                workItem.Id,
                activityId,
                now));
            createdNotifications.Add((recipientId, notificationId));
        }
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
        foreach (var notification in createdNotifications)
        {
            await realtimePublisher.PublishNotificationCreatedAsync(
                notification.RecipientId,
                new NotificationCreatedRealtimeEvent(
                    notification.NotificationId,
                    notification.NotificationId,
                    now),
                cancellationToken);
        }
        return Result.Success();
    }

    internal static string ToApiValue(Domain.WorkflowCategory category) => category switch
    {
        Domain.WorkflowCategory.Todo => "TODO",
        Domain.WorkflowCategory.InProgress => "IN_PROGRESS",
        Domain.WorkflowCategory.Done => "DONE",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };
}
