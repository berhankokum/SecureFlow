using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Projects.Domain;

public sealed class Project : AggregateRoot
{
    public const int KeyMinLength = 2;
    public const int KeyMaxLength = 10;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectMember> _members = [];

    private Project()
    {
    }

    private Project(
        Guid id,
        Guid organizationId,
        string key,
        string name,
        string? description,
        Guid createdBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        Key = NormalizeKey(key);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        _members.Add(ProjectMember.Create(id, createdBy, ProjectRole.Manager));
    }

    public Guid OrganizationId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    public static Project Create(
        Guid organizationId,
        string key,
        string name,
        string? description,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id cannot be empty.", nameof(organizationId));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("Creator user id cannot be empty.", nameof(createdBy));
        }

        return new Project(Guid.NewGuid(), organizationId, key, name, description, createdBy, createdAt);
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

    public ProjectMember AddMember(Guid userId, ProjectRole role)
    {
        EnsureMutable();
        if (_members.Any(member => member.UserId == userId))
        {
            throw new DomainException("Kullanıcı zaten Project üyesi.");
        }

        var member = ProjectMember.Create(Id, userId, role);
        _members.Add(member);
        return member;
    }

    public bool ChangeMemberRole(Guid userId, ProjectRole role)
    {
        EnsureMutable();
        var member = _members.SingleOrDefault(item => item.UserId == userId);
        if (member is null)
        {
            return false;
        }

        member.ChangeRole(role);
        return true;
    }

    public bool RemoveMember(Guid userId)
    {
        EnsureMutable();
        var member = _members.SingleOrDefault(item => item.UserId == userId);
        return member is not null && _members.Remove(member);
    }

    public static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalized = key.Trim().ToUpperInvariant();

        if (normalized.Length is < KeyMinLength or > KeyMaxLength
            || normalized[0] is < 'A' or > 'Z'
            || normalized.Any(character => character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')))
        {
            throw new ArgumentException(
                "Project key must start with A-Z and contain 2-10 uppercase letters or digits.",
                nameof(key));
        }

        return normalized;
    }

    private void EnsureMutable()
    {
        if (IsArchived)
        {
            throw new DomainException("Archive edilmiş Project değiştirilemez.");
        }
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"Project name cannot exceed {NameMaxLength} characters.", nameof(name));
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
                $"Project description cannot exceed {DescriptionMaxLength} characters.",
                nameof(description));
        }

        return normalized;
    }
}
