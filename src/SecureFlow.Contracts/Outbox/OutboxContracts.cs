namespace SecureFlow.Contracts.Outbox;

public static class OutboxEventTypes
{
    public const string WorkItemCreated = "WorkItemCreated";
    public const string WorkItemUpdated = "WorkItemUpdated";
    public const string WorkItemAssigned = "WorkItemAssigned";
    public const string WorkItemTransitioned = "WorkItemTransitioned";
    public const string CommentCreated = "CommentCreated";
    public const string NotificationCreated = "NotificationCreated";
    public const string MemberRemoved = "MemberRemoved";
    public const string ProjectMemberRemoved = "ProjectMemberRemoved";
    public const string AttachmentObjectDeleteRequested = "AttachmentObjectDeleteRequested";
}

public sealed record OutboxWriteRequest(Guid EventId, string Type, int SchemaVersion, object Payload, DateTimeOffset OccurredAt);
public sealed record OutboxEnvelope(Guid EventId, string Type, int SchemaVersion, string Payload, DateTimeOffset OccurredAt);
public sealed record WorkItemOutboxPayload(Guid OrganizationId, Guid ProjectId, Guid WorkItemId);
public sealed record CommentOutboxPayload(Guid OrganizationId, Guid ProjectId, Guid WorkItemId, Guid CommentId);
public sealed record NotificationOutboxPayload(Guid RecipientUserId, Guid NotificationId);
public sealed record MembershipOutboxPayload(Guid OrganizationId, Guid UserId, Guid? ProjectId = null);
public sealed record AttachmentObjectDeletePayload(string StorageKey);

public interface IOutboxWriter { void Add(OutboxWriteRequest request); }
public interface IDurableOutboxWriter { Task AddAsync(OutboxWriteRequest request, CancellationToken cancellationToken = default); }
public interface IOutboxEventDispatcher { Task DispatchAsync(OutboxEnvelope message, CancellationToken cancellationToken); }

public static class OutboxEvents
{
    public static OutboxWriteRequest WorkItem(string type, Guid eventId, Guid organizationId, Guid projectId, Guid workItemId, DateTimeOffset occurredAt) =>
        new(eventId, type, 1, new WorkItemOutboxPayload(organizationId, projectId, workItemId), occurredAt);
    public static OutboxWriteRequest Comment(Guid eventId, Guid organizationId, Guid projectId, Guid workItemId, Guid commentId, DateTimeOffset occurredAt) =>
        new(eventId, OutboxEventTypes.CommentCreated, 1, new CommentOutboxPayload(organizationId, projectId, workItemId, commentId), occurredAt);
    public static OutboxWriteRequest Notification(Guid notificationId, Guid userId, DateTimeOffset occurredAt) =>
        new(notificationId, OutboxEventTypes.NotificationCreated, 1, new NotificationOutboxPayload(userId, notificationId), occurredAt);
    public static OutboxWriteRequest AttachmentDelete(Guid eventId, string storageKey, DateTimeOffset occurredAt) =>
        new(eventId, OutboxEventTypes.AttachmentObjectDeleteRequested, 1, new AttachmentObjectDeletePayload(storageKey), occurredAt);
}
