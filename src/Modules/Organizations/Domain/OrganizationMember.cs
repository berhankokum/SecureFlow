using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Domain;

public sealed class OrganizationMember : Entity
{
    private OrganizationMember()
    {
    }

    private OrganizationMember(
        Guid id,
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        DateTimeOffset joinedAt,
        Guid? invitedBy)
        : base(id)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        InvitedBy = invitedBy;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public OrganizationRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public Guid? InvitedBy { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public bool IsActive => RemovedAt is null;

    internal static OrganizationMember CreateOwner(
        Guid organizationId,
        Guid creatorUserId,
        DateTimeOffset joinedAt) =>
        new(Guid.NewGuid(), organizationId, creatorUserId, OrganizationRole.Owner, joinedAt, invitedBy: null);

    public static OrganizationMember CreateFromInvitation(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        DateTimeOffset joinedAt,
        Guid invitedBy)
    {
        ValidateMembershipInput(organizationId, userId, invitedBy);
        return new(Guid.NewGuid(), organizationId, userId, role, joinedAt, invitedBy);
    }

    public void ChangeRole(OrganizationRole role)
    {
        if (!IsActive)
        {
            throw new DomainException("Çıkarılmış bir membership'in rolü değiştirilemez.");
        }

        Role = role;
    }

    public void Remove(DateTimeOffset removedAt)
    {
        if (!IsActive)
        {
            throw new DomainException("Membership zaten çıkarılmış.");
        }

        RemovedAt = removedAt;
    }

    public void Reactivate(
        OrganizationRole role,
        DateTimeOffset joinedAt,
        Guid invitedBy)
    {
        if (IsActive)
        {
            throw new DomainException("Aktif membership yeniden etkinleştirilemez.");
        }

        if (invitedBy == Guid.Empty)
        {
            throw new ArgumentException("Inviter user id cannot be empty.", nameof(invitedBy));
        }

        Role = role;
        JoinedAt = joinedAt;
        InvitedBy = invitedBy;
        RemovedAt = null;
    }

    private static void ValidateMembershipInput(Guid organizationId, Guid userId, Guid invitedBy)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id cannot be empty.", nameof(organizationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (invitedBy == Guid.Empty)
        {
            throw new ArgumentException("Inviter user id cannot be empty.", nameof(invitedBy));
        }
    }
}
