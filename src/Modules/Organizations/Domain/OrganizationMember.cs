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
}
