using System.Text.Json;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Modules.Outbox.Application;
using SecureFlow.Modules.Outbox.Domain;
namespace SecureFlow.Modules.Outbox.Infrastructure;

public sealed class OutboxWriter(IOutboxDbContext dbContext) : IOutboxWriter
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public void Add(OutboxWriteRequest request)
    {
        Validate(request);
        var payload = JsonSerializer.Serialize(request.Payload, request.Payload.GetType(), JsonOptions);
        if (payload.Length > 16 * 1024) throw new InvalidOperationException("Outbox payload exceeds the 16 KiB limit.");
        dbContext.OutboxMessages.Add(OutboxMessage.Create(request.EventId, request.Type, request.SchemaVersion, payload, request.OccurredAt));
    }
    internal static void Validate(OutboxWriteRequest request)
    {
        if (request.EventId == Guid.Empty || request.SchemaVersion != 1 || !OutboxEventCatalog.Matches(request.Type, request.Payload))
            throw new InvalidOperationException("Unsupported outbox event contract.");
    }
}

internal static class OutboxEventCatalog
{
    public static bool Matches(string type, object payload) => type switch
    {
        OutboxEventTypes.WorkItemCreated or OutboxEventTypes.WorkItemUpdated or OutboxEventTypes.WorkItemAssigned or OutboxEventTypes.WorkItemTransitioned => payload is WorkItemOutboxPayload,
        OutboxEventTypes.CommentCreated => payload is CommentOutboxPayload,
        OutboxEventTypes.NotificationCreated => payload is NotificationOutboxPayload,
        OutboxEventTypes.MemberRemoved or OutboxEventTypes.ProjectMemberRemoved => payload is MembershipOutboxPayload,
        OutboxEventTypes.AttachmentObjectDeleteRequested => payload is AttachmentObjectDeletePayload,
        _ => false,
    };
}
