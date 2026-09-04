using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Application;

public static class NotificationErrors
{
    public static readonly Error NotFound = new(
        "Notifications.NotFound",
        "Notification bulunamadı.");
}
