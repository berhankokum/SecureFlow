using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Teams.Domain;

public sealed class Team : AggregateRoot
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private readonly List<TeamMember> _members = [];

    private Team()
    {
    }

    private Team(
        Guid id,
        Guid organizationId,
        string name,
        string? description,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    public static Team Create(
        Guid organizationId,
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id cannot be empty.", nameof(organizationId));
        }

        return new Team(Guid.NewGuid(), organizationId, name, description, createdAt);
    }

    public void Update(string name, string? description, DateTimeOffset updatedAt)
    {
        EnsureMutable();
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (IsArchived)
        {
            return;
        }

        ArchivedAt = archivedAt;
        UpdatedAt = archivedAt;
    }

    public TeamMember AddMember(Guid userId, Guid addedBy, DateTimeOffset addedAt)
    {
        EnsureMutable();

        if (_members.Any(member => member.UserId == userId))
        {
            throw new DomainException("Kullanıcı zaten Team üyesi.");
        }

        var membership = TeamMember.Create(Id, userId, addedBy, addedAt);
        _members.Add(membership);
        return membership;
    }

    public bool RemoveMember(Guid userId)
    {
        EnsureMutable();
        var membership = _members.SingleOrDefault(member => member.UserId == userId);
        return membership is not null && _members.Remove(membership);
    }

    private void EnsureMutable()
    {
        if (IsArchived)
        {
            throw new DomainException("Archive edilmiş Team değiştirilemez.");
        }
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();

        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"Team name cannot exceed {NameMaxLength} characters.", nameof(name));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalized = description.Trim();
        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"Team description cannot exceed {DescriptionMaxLength} characters.",
                nameof(description));
        }

        return normalized;
    }
}
