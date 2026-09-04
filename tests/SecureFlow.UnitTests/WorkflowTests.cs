using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public sealed class WorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_workflow_has_ordered_states_and_locked_transition_permissions()
    {
        var workflow = Workflow.CreateDefault(Guid.NewGuid(), Now);

        Assert.Equal(Workflow.DefaultName, workflow.Name);
        Assert.Equal(
            ["BACKLOG", "TODO", "IN_PROGRESS", "IN_REVIEW", "DONE"],
            workflow.States.OrderBy(state => state.Position).Select(state => state.Name));
        Assert.Equal(
            [WorkflowCategory.Todo, WorkflowCategory.Todo, WorkflowCategory.InProgress,
                WorkflowCategory.InProgress, WorkflowCategory.Done],
            workflow.States.OrderBy(state => state.Position).Select(state => state.Category));
        Assert.Equal(4, workflow.Transitions.Count);

        var done = workflow.States.Single(state => state.Name == "DONE");
        var approval = workflow.Transitions.Single(transition => transition.ToStateId == done.Id);
        Assert.Equal(Permissions.WorkItemApprove, approval.RequiredPermission);
    }

    [Fact]
    public void Work_item_only_transitions_from_its_current_state()
    {
        var workflow = Workflow.CreateDefault(Guid.NewGuid(), Now);
        var backlog = workflow.States.Single(state => state.Name == "BACKLOG");
        var toTodo = workflow.Transitions.Single(transition => transition.FromStateId == backlog.Id);
        var item = Create(backlog.Id);

        item.Transition(toTodo, Now.AddMinutes(1));

        Assert.Equal(toTodo.ToStateId, item.WorkflowStateId);
        Assert.Equal(Now.AddMinutes(1), item.UpdatedAt);
        Assert.Throws<DomainException>(() => item.Transition(toTodo, Now.AddMinutes(2)));
    }

    [Fact]
    public void Archived_work_item_rejects_transition()
    {
        var workflow = Workflow.CreateDefault(Guid.NewGuid(), Now);
        var backlog = workflow.States.Single(state => state.Name == "BACKLOG");
        var item = Create(backlog.Id);
        item.Archive(Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => item.Transition(
            workflow.Transitions.Single(transition => transition.FromStateId == backlog.Id),
            Now.AddMinutes(2)));
    }

    private static WorkItem Create(Guid stateId) => WorkItem.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        1,
        WorkItemType.Task,
        "Workflow test",
        null,
        WorkItemPriority.Medium,
        stateId,
        Guid.NewGuid(),
        null,
        DateOnly.FromDateTime(Now.UtcDateTime),
        Now);
}
