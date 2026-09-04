using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Domain;

public sealed class WorkflowState : Entity
{
    public const int NameMaxLength = 100;

    private WorkflowState()
    {
    }

    internal WorkflowState(
        Guid id,
        Guid workflowId,
        string name,
        WorkflowCategory category,
        int position) : base(id)
    {
        WorkflowId = workflowId != Guid.Empty
            ? workflowId
            : throw new ArgumentException("Workflow id cannot be empty.", nameof(workflowId));
        Name = NormalizeName(name);
        Category = category;
        Position = position >= 0
            ? position
            : throw new ArgumentOutOfRangeException(nameof(position));
    }

    public Guid WorkflowId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public WorkflowCategory Category { get; private set; }

    public int Position { get; private set; }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim().ToUpperInvariant();
        return normalized.Length <= NameMaxLength
            ? normalized
            : throw new ArgumentException(
                $"Workflow state name cannot exceed {NameMaxLength} characters.",
                nameof(name));
    }
}
