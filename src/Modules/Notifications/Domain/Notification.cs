using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Domain;

public sealed class Notification : Entity
{
    public const int TypeMaxLength = 64;
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 500;
    public const int ResourceTypeMaxLength = 64;

    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid organizationId,
        Guid userId,
        string type,
        string title,
        string message,
        string resourceType,
        Guid? resourceId,
        Guid sourceEventId,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        UserId = RequireId(userId, nameof(userId));
        Type = Normalize(type, TypeMaxLength, nameof(type), upper: true);
        Title = Normalize(title, TitleMaxLength, nameof(title));
        Message = Normalize(message, MessageMaxLength, nameof(message));
        ResourceType = Normalize(resourceType, ResourceTypeMaxLength, nameof(resourceType), upper: true);
        ResourceId = resourceId is null || resourceId != Guid.Empty
            ? resourceId
            : throw new ArgumentException("Id cannot be empty.", nameof(resourceId));
        SourceEventId = RequireId(sourceEventId, nameof(sourceEventId));
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public Guid? ResourceId { get; private set; }

    public Guid SourceEventId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(
        Guid organizationId,
        Guid userId,
        string type,
        string title,
        string message,
        string resourceType,
        Guid? resourceId,
        Guid sourceEventId,
        DateTimeOffset createdAt) =>
        new(
            Guid.NewGuid(),
            organizationId,
            userId,
            type,
            title,
            message,
            resourceType,
            resourceId,
            sourceEventId,
            createdAt);

    public bool MarkRead(DateTimeOffset readAt)
    {
        if (IsRead)
        {
            return false;
        }

        IsRead = true;
        ReadAt = readAt;
        return true;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static string Normalize(string value, int maxLength, string parameterName, bool upper = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        }

        return upper ? normalized.ToUpperInvariant() : normalized;
    }
}
