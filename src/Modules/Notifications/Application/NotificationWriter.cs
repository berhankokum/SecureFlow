using SecureFlow.Contracts.Notifications;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.Modules.Notifications.Application;

public sealed class NotificationWriter(INotificationsDbContext dbContext) : INotificationWriter
{
    public Guid Add(NotificationWriteRequest request)
    {
        var notification = Notification.Create(
            request.OrganizationId,
            request.UserId,
            request.Type,
            request.Title,
            request.Message,
            request.ResourceType,
            request.ResourceId,
            request.SourceEventId,
            request.CreatedAt);
        dbContext.Notifications.Add(notification);
        return notification.Id;
    }
}
