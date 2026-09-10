using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Application.Authorization;

/// <summary>Organization role and resource rules backed by the central permission matrix.</summary>
public static class OrganizationAccessRules
{
    public static bool CanViewMembers(OrganizationRole actorRole) =>
        HasPermission(actorRole, Permissions.MemberView);

    public static bool CanInvite(OrganizationRole actorRole, OrganizationRole invitedRole) =>
        HasPermission(actorRole, Permissions.MemberInvite)
        && (actorRole == OrganizationRole.Owner || invitedRole != OrganizationRole.Owner);

    public static bool CanListOrRevokeInvitations(OrganizationRole actorRole) =>
        HasPermission(actorRole, Permissions.MemberInvite);

    public static bool CanChangeRole(OrganizationRole actorRole) =>
        HasPermission(actorRole, Permissions.MemberRoleChange);

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

        return HasPermission(actorRole, Permissions.MemberRemove)
            && (actorRole == OrganizationRole.Owner || targetRole == OrganizationRole.Member);
    }

    public static bool HasPermission(OrganizationRole role, string permission) =>
        PermissionMatrix.HasPermission(ToPermissionRole(role), permission);

    public static IReadOnlyList<string> GetPermissions(OrganizationRole role) =>
        PermissionMatrix.GetPermissions(ToPermissionRole(role));

    public static PermissionRole ToPermissionRole(OrganizationRole role) => role switch
    {
        OrganizationRole.Owner => PermissionRole.Owner,
        OrganizationRole.Admin => PermissionRole.Admin,
        OrganizationRole.Member => PermissionRole.Member,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown organization role."),
    };
}
