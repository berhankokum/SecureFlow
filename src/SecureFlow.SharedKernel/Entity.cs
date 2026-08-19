namespace SecureFlow.SharedKernel;

public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        Id = id;
    }

    protected Entity()
    {
    }

    public Guid Id { get; protected init; }

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);

    public bool Equals(Entity? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    public override bool Equals(object? obj) => Equals(obj as Entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
