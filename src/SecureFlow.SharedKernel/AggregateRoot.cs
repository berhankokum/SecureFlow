namespace SecureFlow.SharedKernel;

public abstract class AggregateRoot : Entity
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
