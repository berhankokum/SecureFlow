using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Domain;

public sealed class WorkflowTransition : Entity
{
    public const int RequiredPermissionMaxLength = 100;

    private WorkflowTransition()
    {
    }

    internal WorkflowTransition(
        Guid workflowId,
        Guid fromStateId,
        Guid toStateId,
        string? requiredPermission) : base(Guid.NewGuid())
    {
        WorkflowId = RequireId(workflowId, nameof(workflowId));
        FromStateId = RequireId(fromStateId, nameof(fromStateId));
        ToStateId = RequireId(toStateId, nameof(toStateId));
        if (fromStateId == toStateId)
        {
            throw new ArgumentException("Workflow transition states must be different.");
        }

        RequiredPermission = NormalizePermission(requiredPermission);
    }

    public Guid WorkflowId { get; private set; }

    public Guid FromStateId { get; private set; }

    public Guid ToStateId { get; private set; }

    public string? RequiredPermission { get; private set; }

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static string? NormalizePermission(string? permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return null;
        }

        var normalized = permission.Trim().ToLowerInvariant();
        return normalized.Length <= RequiredPermissionMaxLength
            ? normalized
            : throw new ArgumentException(
                $"Required permission cannot exceed {RequiredPermissionMaxLength} characters.",
                nameof(permission));
    }
}
