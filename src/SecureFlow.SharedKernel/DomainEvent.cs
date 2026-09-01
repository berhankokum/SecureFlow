namespace SecureFlow.SharedKernel;

public abstract record DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; init; }
}
