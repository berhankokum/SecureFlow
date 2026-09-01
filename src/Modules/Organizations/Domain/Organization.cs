using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Domain;

public sealed class Organization : AggregateRoot
{
    private readonly List<OrganizationMember> _members = [];

    private Organization()
    {
    }

    private Organization(
        Guid id,
        string name,
        string slug,
        string? logoUrl,
        Guid createdBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        LogoUrl = logoUrl;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? LogoUrl { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    public static Organization Create(
        string name,
        string slug,
        string? logoUrl,
        Guid creatorUserId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (creatorUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator user id cannot be empty.", nameof(creatorUserId));
        }

        var organization = new Organization(
            Guid.NewGuid(),
            name.Trim(),
            slug,
            string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            creatorUserId,
            createdAt);

        // BR-ORG-002/003: aggregate hiçbir zaman OWNER'sız oluşturulamaz.
        organization._members.Add(
            OrganizationMember.CreateOwner(organization.Id, creatorUserId, createdAt));

        return organization;
    }
}
