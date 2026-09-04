using SecureFlow.Contracts.Authorization;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Domain;

public sealed class Workflow : AggregateRoot
{
    public const int NameMaxLength = 200;
    public const string DefaultName = "DEFAULT";

    private readonly List<WorkflowState> _states = [];
    private readonly List<WorkflowTransition> _transitions = [];

    private Workflow()
    {
    }

    private Workflow(Guid id, Guid organizationId, string name, DateTimeOffset createdAt) : base(id)
    {
        OrganizationId = organizationId != Guid.Empty
            ? organizationId
            : throw new ArgumentException("Organization id cannot be empty.", nameof(organizationId));
        Name = NormalizeName(name);
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<WorkflowState> States => _states.AsReadOnly();

    public IReadOnlyCollection<WorkflowTransition> Transitions => _transitions.AsReadOnly();

    public static Workflow CreateDefault(Guid organizationId, DateTimeOffset createdAt)
    {
        var workflow = new Workflow(Guid.NewGuid(), organizationId, DefaultName, createdAt);
        var backlog = workflow.AddState("BACKLOG", WorkflowCategory.Todo, 0);
        var todo = workflow.AddState("TODO", WorkflowCategory.Todo, 1);
        var inProgress = workflow.AddState("IN_PROGRESS", WorkflowCategory.InProgress, 2);
        var inReview = workflow.AddState("IN_REVIEW", WorkflowCategory.InProgress, 3);
        var done = workflow.AddState("DONE", WorkflowCategory.Done, 4);

        workflow.AddTransition(backlog, todo);
        workflow.AddTransition(todo, inProgress);
        workflow.AddTransition(inProgress, inReview);
        workflow.AddTransition(inReview, done, Permissions.WorkItemApprove);
        return workflow;
    }

    private WorkflowState AddState(string name, WorkflowCategory category, int position)
    {
        var state = new WorkflowState(Guid.NewGuid(), Id, name, category, position);
        _states.Add(state);
        return state;
    }

    private void AddTransition(
        WorkflowState fromState,
        WorkflowState toState,
        string? requiredPermission = null) =>
        _transitions.Add(new WorkflowTransition(Id, fromState.Id, toState.Id, requiredPermission));

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        return normalized.Length <= NameMaxLength
            ? normalized
            : throw new ArgumentException(
                $"Workflow name cannot exceed {NameMaxLength} characters.",
                nameof(name));
    }
}
