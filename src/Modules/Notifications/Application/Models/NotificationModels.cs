namespace SecureFlow.Modules.Notifications.Application.Models;

public sealed record NotificationSummary(
    Guid Id,
    Guid OrganizationId,
    string Type,
    string Title,
    string Message,
    string ResourceType,
    Guid? ResourceId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record NotificationListSummary(
    IReadOnlyList<NotificationSummary> Items,
    int UnreadCount);

public sealed record NotificationPreferenceSummary(
    bool InAppEnabled,
    bool EmailEnabled,
    DateTimeOffset? UpdatedAt);
