using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.Files.Application.Abstractions;
using SecureFlow.Modules.Outbox.Infrastructure;
namespace SecureFlow.Api.Realtime;

public sealed class SecureFlowOutboxDispatcher(IHubContext<RealtimeHub> hubContext, IObjectStorage objectStorage) : IOutboxEventDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public Task DispatchAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        if (message.SchemaVersion != 1) throw new UnsupportedOutboxEventException("Unsupported schema version.");
        return message.Type switch
        {
            OutboxEventTypes.WorkItemCreated or OutboxEventTypes.WorkItemUpdated or OutboxEventTypes.WorkItemAssigned or OutboxEventTypes.WorkItemTransitioned => PublishWorkItem(message, cancellationToken),
            OutboxEventTypes.CommentCreated => PublishComment(message, cancellationToken),
            OutboxEventTypes.NotificationCreated => PublishNotification(message, cancellationToken),
            OutboxEventTypes.AttachmentObjectDeleteRequested => DeleteObject(message, cancellationToken),
            OutboxEventTypes.MemberRemoved or OutboxEventTypes.ProjectMemberRemoved => Task.CompletedTask,
            _ => throw new UnsupportedOutboxEventException("Unsupported event type."),
        };
    }
    private Task PublishWorkItem(OutboxEnvelope m, CancellationToken ct) { var p = Read<WorkItemOutboxPayload>(m); return hubContext.Clients.Group(RealtimeGroupNames.Project(p.ProjectId)).SendAsync(RealtimeEventNames.WorkItemUpdated, new WorkItemUpdatedRealtimeEvent(m.EventId, p.OrganizationId, p.ProjectId, p.WorkItemId, m.OccurredAt), ct); }
    private Task PublishComment(OutboxEnvelope m, CancellationToken ct) { var p = Read<CommentOutboxPayload>(m); return hubContext.Clients.Group(RealtimeGroupNames.Project(p.ProjectId)).SendAsync(RealtimeEventNames.CommentCreated, new CommentCreatedRealtimeEvent(m.EventId, p.OrganizationId, p.ProjectId, p.WorkItemId, p.CommentId, m.OccurredAt), ct); }
    private Task PublishNotification(OutboxEnvelope m, CancellationToken ct) { var p = Read<NotificationOutboxPayload>(m); return hubContext.Clients.Group(RealtimeGroupNames.User(p.RecipientUserId)).SendAsync(RealtimeEventNames.NotificationCreated, new NotificationCreatedRealtimeEvent(m.EventId, p.NotificationId, m.OccurredAt), ct); }
    private Task DeleteObject(OutboxEnvelope m, CancellationToken ct) { var p = Read<AttachmentObjectDeletePayload>(m); if (string.IsNullOrWhiteSpace(p.StorageKey)) throw new UnsupportedOutboxEventException("Invalid storage key."); return objectStorage.DeleteAsync(p.StorageKey, ct); }
    private static T Read<T>(OutboxEnvelope m) => JsonSerializer.Deserialize<T>(m.Payload, JsonOptions) ?? throw new UnsupportedOutboxEventException("Invalid payload.");
}
