using SecureFlow.Modules.Notifications.Application.Models;

namespace SecureFlow.Modules.Notifications.Presentation.Contracts;

public sealed record NotificationResponse(
    Guid Id,
    Guid OrganizationId,
    string Type,
    string Title,
    string Message,
    string ResourceType,
    Guid? ResourceId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt)
{
    public static NotificationResponse From(NotificationSummary notification) => new(
        notification.Id,
        notification.OrganizationId,
        notification.Type,
        notification.Title,
        notification.Message,
        notification.ResourceType,
        notification.ResourceId,
        notification.IsRead,
        notification.CreatedAt,
        notification.ReadAt);
}

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    int UnreadCount)
{
    public static NotificationListResponse From(NotificationListSummary notifications) =>
        new(notifications.Items.Select(NotificationResponse.From).ToArray(), notifications.UnreadCount);
}

public sealed record NotificationPreferenceResponse(
    bool InAppEnabled,
    bool EmailEnabled,
    DateTimeOffset? UpdatedAt)
{
    public static NotificationPreferenceResponse From(NotificationPreferenceSummary preference) =>
        new(preference.InAppEnabled, preference.EmailEnabled, preference.UpdatedAt);
}

public sealed record UpdateNotificationPreferenceRequest(bool EmailEnabled);
