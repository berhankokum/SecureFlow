using SecureFlow.Contracts.Realtime;

namespace SecureFlow.Api.Realtime;

public sealed class RealtimePublicationBuffer(IRealtimePublisher publisher)
    : IRealtimePublicationBuffer
{
    private readonly List<WorkItemUpdatedRealtimeEvent> _workItemEvents = [];

    public void Enqueue(WorkItemUpdatedRealtimeEvent message) => _workItemEvents.Add(message);

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        foreach (var message in _workItemEvents)
        {
            await publisher.PublishWorkItemUpdatedAsync(message, cancellationToken);
        }

        _workItemEvents.Clear();
    }
}
