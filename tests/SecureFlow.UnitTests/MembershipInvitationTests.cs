using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Invitations;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.UnitTests;

public class MembershipInvitationTests
{
    [Fact]
    public void Invitation_token_should_be_cryptographically_random_and_only_hash_should_be_persisted()
    {
        var first = InvitationTokenService.Generate();
        var second = InvitationTokenService.Generate();

        Assert.NotEqual(first.RawValue, second.RawValue);
        Assert.Equal(43, first.RawValue.Length);
        Assert.Equal(64, first.Hash.Length);
        Assert.Equal(first.Hash, InvitationTokenService.Hash(first.RawValue));
        Assert.DoesNotContain(first.RawValue, first.Hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Invitation_should_be_single_use()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = CreateInvitation(now, now.AddHours(72));

        invitation.Accept(now.AddMinutes(1));

        Assert.True(invitation.IsAccepted);
        Assert.Throws<DomainException>(() => invitation.Accept(now.AddMinutes(2)));
    }

    [Fact]
    public void Expired_or_revoked_invitation_cannot_be_accepted()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = CreateInvitation(now, now.AddMinutes(1));
        var revoked = CreateInvitation(now, now.AddHours(72));
        revoked.Revoke(now.AddMinutes(1));

        Assert.Throws<DomainException>(() => expired.Accept(now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => revoked.Accept(now.AddMinutes(2)));
    }

    [Fact]
    public void Removed_membership_can_be_reactivated_without_losing_row_identity()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var membership = OrganizationMember.CreateFromInvitation(
            organizationId,
            userId,
            OrganizationRole.Member,
            now,
            inviterId);
        var membershipId = membership.Id;

        membership.Remove(now.AddMinutes(1));
        membership.Reactivate(OrganizationRole.Admin, now.AddMinutes(2), inviterId);

        Assert.Equal(membershipId, membership.Id);
        Assert.True(membership.IsActive);
        Assert.Equal(OrganizationRole.Admin, membership.Role);
        Assert.Null(membership.RemovedAt);
    }

    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner, true)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Owner, false)]
    [InlineData(OrganizationRole.Admin, OrganizationRole.Member, true)]
    [InlineData(OrganizationRole.Member, OrganizationRole.Member, false)]
    public void Invitation_role_boundary_should_be_conservative_until_phase_5(
        OrganizationRole actorRole,
        OrganizationRole invitedRole,
        bool expected)
    {
        Assert.Equal(expected, OrganizationAccessRules.CanInvite(actorRole, invitedRole));
    }

    private static OrganizationInvitation CreateInvitation(
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        OrganizationInvitation.Create(
            Guid.NewGuid(),
            "INVITED@Example.com",
            OrganizationRole.Member,
            InvitationTokenService.Generate().Hash,
            createdAt,
            expiresAt,
            Guid.NewGuid());
}
