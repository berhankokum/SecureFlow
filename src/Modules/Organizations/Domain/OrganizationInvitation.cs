using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Domain;

public sealed class OrganizationInvitation : Entity
{
    private OrganizationInvitation()
    {
    }

    private OrganizationInvitation(
        Guid id,
        Guid organizationId,
        string email,
        OrganizationRole role,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid createdBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public OrganizationRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    public bool IsAccepted => AcceptedAt is not null;

    public bool IsRevoked => RevokedAt is not null;

    public bool IsMarkedExpired => ExpiredAt is not null;

    public bool IsExpired(DateTimeOffset now) => IsMarkedExpired || now >= ExpiresAt;

    public bool IsPending(DateTimeOffset now) => !IsAccepted && !IsRevoked && !IsExpired(now);

    public static OrganizationInvitation Create(
        Guid organizationId,
        string email,
        OrganizationRole role,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid createdBy)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id cannot be empty.", nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("Creator user id cannot be empty.", nameof(createdBy));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Invitation expiration must be after creation.", nameof(expiresAt));
        }

        return new OrganizationInvitation(
            Guid.NewGuid(),
            organizationId,
            NormalizeEmail(email),
            role,
            tokenHash,
            expiresAt,
            createdBy,
            createdAt);
    }

    public void Accept(DateTimeOffset acceptedAt)
    {
        if (IsAccepted)
        {
            throw new DomainException("Invitation daha önce kullanılmış.");
        }

        if (IsRevoked)
        {
            throw new DomainException("Revoke edilmiş invitation kabul edilemez.");
        }

        if (IsMarkedExpired)
        {
            throw new DomainException("Süresi dolmuş invitation kabul edilemez.");
        }

        if (IsExpired(acceptedAt))
        {
            throw new DomainException("Invitation süresi dolmuş.");
        }

        AcceptedAt = acceptedAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (IsAccepted)
        {
            throw new DomainException("Kabul edilmiş invitation revoke edilemez.");
        }

        if (IsRevoked)
        {
            return;
        }

        if (IsMarkedExpired)
        {
            throw new DomainException("Süresi dolmuş invitation revoke edilemez.");
        }

        RevokedAt = revokedAt;
    }

    public bool MarkExpired(DateTimeOffset expiredAt)
    {
        if (IsAccepted || IsRevoked || IsMarkedExpired || expiredAt < ExpiresAt)
        {
            return false;
        }

        ExpiredAt = expiredAt;
        return true;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
