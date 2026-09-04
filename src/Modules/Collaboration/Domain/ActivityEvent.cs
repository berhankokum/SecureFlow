using System.Text.Json;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration.Domain;

public sealed class ActivityEvent : Entity
{
    public const int TypeMaxLength = 64;

    private ActivityEvent()
    {
    }

    private ActivityEvent(
        Guid id,
        Guid organizationId,
        Guid? projectId,
        Guid? workItemId,
        Guid actorId,
        string type,
        string metadata,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        ProjectId = OptionalId(projectId, nameof(projectId));
        WorkItemId = OptionalId(workItemId, nameof(workItemId));
        ActorId = RequireId(actorId, nameof(actorId));
        Type = NormalizeType(type);
        Metadata = ValidateMetadata(metadata);
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? WorkItemId { get; private set; }

    public Guid ActorId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Metadata { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public static ActivityEvent Create(
        Guid organizationId,
        Guid? projectId,
        Guid? workItemId,
        Guid actorId,
        string type,
        IReadOnlyDictionary<string, string?> metadata,
        DateTimeOffset createdAt) =>
        new(
            Guid.NewGuid(),
            organizationId,
            projectId,
            workItemId,
            actorId,
            type,
            JsonSerializer.Serialize(metadata),
            createdAt);

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static Guid? OptionalId(Guid? value, string parameterName) =>
        value is null || value != Guid.Empty
            ? value
            : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static string NormalizeType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        var normalized = type.Trim().ToUpperInvariant();
        if (normalized.Length > TypeMaxLength)
        {
            throw new ArgumentException($"Type cannot exceed {TypeMaxLength} characters.", nameof(type));
        }

        return normalized;
    }

    private static string ValidateMetadata(string metadata)
    {
        using var document = JsonDocument.Parse(metadata);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Metadata must be a JSON object.", nameof(metadata));
        }

        return metadata;
    }
}
