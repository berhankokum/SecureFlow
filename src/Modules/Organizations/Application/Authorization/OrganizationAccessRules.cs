using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Application.Authorization;

/// <summary>
/// FAZ 4 için merkezi ve dar rol sınırı. FAZ 5'te permission/policy sistemine taşınır.
/// </summary>
public static class OrganizationAccessRules
{
    public static bool CanViewMembers(OrganizationRole actorRole) =>
        actorRole is OrganizationRole.Owner or OrganizationRole.Admin or OrganizationRole.Member;

    public static bool CanInvite(OrganizationRole actorRole, OrganizationRole invitedRole) =>
        actorRole == OrganizationRole.Owner
        || (actorRole == OrganizationRole.Admin && invitedRole != OrganizationRole.Owner);

    public static bool CanListOrRevokeInvitations(OrganizationRole actorRole) =>
        actorRole is OrganizationRole.Owner or OrganizationRole.Admin;

    public static bool CanChangeRole(OrganizationRole actorRole) =>
        actorRole == OrganizationRole.Owner;

    public static bool CanRemove(
        Guid actorUserId,
        OrganizationRole actorRole,
        Guid targetUserId,
        OrganizationRole targetRole)
    {
        if (actorUserId == targetUserId)
        {
            return true;
        }

        return actorRole == OrganizationRole.Owner
            || (actorRole == OrganizationRole.Admin && targetRole == OrganizationRole.Member);
    }
}
