namespace SecureFlow.Contracts.Realtime;

public static class RealtimeEventNames
{
    public const string WorkItemUpdated = "WorkItemUpdated";
    public const string NotificationCreated = "NotificationCreated";
    public const string CommentCreated = "CommentCreated";
}

public sealed record WorkItemUpdatedRealtimeEvent(
    Guid EventId,
    Guid OrganizationId,
    Guid ProjectId,
    Guid WorkItemId,
    DateTimeOffset OccurredAt);

public sealed record CommentCreatedRealtimeEvent(
    Guid EventId,
    Guid OrganizationId,
    Guid ProjectId,
    Guid WorkItemId,
    Guid CommentId,
    DateTimeOffset OccurredAt);

public sealed record NotificationCreatedRealtimeEvent(
    Guid EventId,
    Guid NotificationId,
    DateTimeOffset OccurredAt);

/// <summary>
/// Commit edilmiş mutation'lardan sonra best-effort real-time invalidation yayını yapar.
/// FAZ 17 Outbox, commit ile publish arasındaki güvenilirlik boşluğunu kapatacaktır.
/// </summary>
public interface IRealtimePublisher
{
    Task PublishWorkItemUpdatedAsync(
        WorkItemUpdatedRealtimeEvent message,
        CancellationToken cancellationToken = default);

    Task PublishCommentCreatedAsync(
        CommentCreatedRealtimeEvent message,
        CancellationToken cancellationToken = default);

    Task PublishNotificationCreatedAsync(
        Guid recipientUserId,
        NotificationCreatedRealtimeEvent message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Başka bir modülün transaction'ı içinde üretilen olayları outer commit sonrasına taşır.
/// Scoped lifetime sayesinde başarısız request'lerde kuyruğa alınan olaylar yayınlanmaz.
/// </summary>
public interface IRealtimePublicationBuffer
{
    void Enqueue(WorkItemUpdatedRealtimeEvent message);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
