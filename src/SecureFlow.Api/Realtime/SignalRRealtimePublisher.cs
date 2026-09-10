using Microsoft.AspNetCore.SignalR;
using SecureFlow.Contracts.Realtime;

namespace SecureFlow.Api.Realtime;

public sealed class SignalRRealtimePublisher(
    IHubContext<RealtimeHub> hubContext,
    ILogger<SignalRRealtimePublisher> logger) : IRealtimePublisher
{
    public Task PublishWorkItemUpdatedAsync(
        WorkItemUpdatedRealtimeEvent message,
        CancellationToken cancellationToken = default) =>
        PublishAsync(
            RealtimeGroupNames.Project(message.ProjectId),
            RealtimeEventNames.WorkItemUpdated,
            message,
            message.EventId,
            cancellationToken);

    public Task PublishCommentCreatedAsync(
        CommentCreatedRealtimeEvent message,
        CancellationToken cancellationToken = default) =>
        PublishAsync(
            RealtimeGroupNames.Project(message.ProjectId),
            RealtimeEventNames.CommentCreated,
            message,
            message.EventId,
            cancellationToken);

    public Task PublishNotificationCreatedAsync(
        Guid recipientUserId,
        NotificationCreatedRealtimeEvent message,
        CancellationToken cancellationToken = default) =>
        PublishAsync(
            RealtimeGroupNames.User(recipientUserId),
            RealtimeEventNames.NotificationCreated,
            message,
            message.EventId,
            cancellationToken);

    private async Task PublishAsync<TMessage>(
        string groupName,
        string eventName,
        TMessage message,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.Group(groupName).SendAsync(eventName, message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Best-effort SignalR publication failed for event {EventId} ({EventName}).", eventId, eventName);
        }
    }
}
