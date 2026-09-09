using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Domain;

public sealed class NotificationEmailDelivery : Entity
{
    public const int LastErrorCodeMaxLength = 128;
    public const int MaximumAttempts = 5;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
    ];

    private NotificationEmailDelivery()
    {
    }

    private NotificationEmailDelivery(
        Guid notificationId,
        NotificationEmailDeliveryStatus status,
        DateTimeOffset createdAt)
        : base(notificationId)
    {
        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("Notification id cannot be empty.", nameof(notificationId));
        }

        NotificationId = notificationId;
        Status = status;
        CreatedAt = createdAt;
        NextAttemptAt = status == NotificationEmailDeliveryStatus.Pending ? createdAt : null;
    }

    public Guid NotificationId { get; private set; }

    public NotificationEmailDeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static NotificationEmailDelivery Create(
        Guid notificationId,
        bool emailEnabled,
        DateTimeOffset createdAt) =>
        new(
            notificationId,
            emailEnabled
                ? NotificationEmailDeliveryStatus.Pending
                : NotificationEmailDeliveryStatus.Skipped,
            createdAt);

    public void MarkSent(DateTimeOffset sentAt)
    {
        EnsurePending();
        AttemptCount++;
        LastAttemptAt = sentAt;
        SentAt = sentAt;
        NextAttemptAt = null;
        LastErrorCode = null;
        Status = NotificationEmailDeliveryStatus.Sent;
    }

    public void RecordFailure(string errorCode, DateTimeOffset failedAt)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        AttemptCount++;
        LastAttemptAt = failedAt;
        LastErrorCode = errorCode.Trim().Length <= LastErrorCodeMaxLength
            ? errorCode.Trim()
            : errorCode.Trim()[..LastErrorCodeMaxLength];

        if (AttemptCount >= MaximumAttempts)
        {
            Status = NotificationEmailDeliveryStatus.Failed;
            NextAttemptAt = null;
            return;
        }

        NextAttemptAt = failedAt.Add(RetryDelays[AttemptCount - 1]);
    }

    private void EnsurePending()
    {
        if (Status != NotificationEmailDeliveryStatus.Pending)
        {
            throw new DomainException("Yalnız pending email delivery değiştirilebilir.");
        }
    }
}
