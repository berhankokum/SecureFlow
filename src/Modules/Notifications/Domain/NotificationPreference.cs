using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Domain;

public sealed class NotificationPreference : Entity
{
    private NotificationPreference()
    {
    }

    private NotificationPreference(Guid userId, bool emailEnabled, DateTimeOffset updatedAt)
        : base(userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(userId));
        }

        UserId = userId;
        EmailEnabled = emailEnabled;
        UpdatedAt = updatedAt;
    }

    public Guid UserId { get; private set; }

    public bool EmailEnabled { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationPreference Create(Guid userId, bool emailEnabled, DateTimeOffset updatedAt) =>
        new(userId, emailEnabled, updatedAt);

    public bool Update(bool emailEnabled, DateTimeOffset updatedAt)
    {
        if (EmailEnabled == emailEnabled)
        {
            return false;
        }

        EmailEnabled = emailEnabled;
        UpdatedAt = updatedAt;
        return true;
    }
}
