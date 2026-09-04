using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
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

public sealed class WorkflowHandlers(IWorkItemsDbContext dbContext, IClock clock)
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

        workItem.Transition(transition, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
