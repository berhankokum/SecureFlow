using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.UnitTests;

public class PermissionMatrixTests
{
    public static TheoryData<PermissionRole, string[]> ExactMatrix() => new()
    {
        { PermissionRole.Owner, [.. Permissions.All] },
        {
            PermissionRole.Admin,
            [.. Permissions.All.Where(permission =>
                permission is not Permissions.OrganizationArchive
                and not Permissions.MemberRoleChange)]
        },
        { PermissionRole.Member, [Permissions.MemberView] },
        {
            PermissionRole.Manager,
            [
                Permissions.TeamCreate,
                Permissions.TeamUpdate,
                Permissions.TeamArchive,
                Permissions.TeamMemberManage,
                Permissions.ProjectUpdate,
                Permissions.ProjectArchive,
                Permissions.WorkItemCreate,
                Permissions.WorkItemUpdate,
                Permissions.WorkItemArchive,
                Permissions.WorkItemAssign,
                Permissions.WorkItemSelfAssign,
                Permissions.WorkItemTransition,
                Permissions.WorkItemApprove,
                Permissions.LabelCreate,
                Permissions.LabelUpdate,
                Permissions.LabelDelete,
                Permissions.LabelAssign,
                Permissions.CommentCreate,
                Permissions.CommentUpdate,
                Permissions.CommentDelete,
                Permissions.AttachmentUpload,
                Permissions.AttachmentDelete,
            ]
        },
        {
            PermissionRole.Contributor,
            [
                Permissions.WorkItemCreate,
                Permissions.WorkItemUpdate,
                Permissions.WorkItemSelfAssign,
                Permissions.WorkItemTransition,
                Permissions.LabelAssign,
                Permissions.CommentCreate,
                Permissions.CommentUpdate,
                Permissions.CommentDelete,
                Permissions.AttachmentUpload,
                Permissions.AttachmentDelete,
            ]
        },
        { PermissionRole.Viewer, [] },
    };

    [Theory]
    [MemberData(nameof(ExactMatrix))]
    public void Every_role_should_have_exactly_the_documented_permissions(
        PermissionRole role,
        string[] expected)
    {
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            PermissionMatrix.GetPermissions(role));
    }

    [Fact]
    public void Contributor_can_self_assign_but_cannot_assign_another_user()
    {
        Assert.True(PermissionMatrix.HasPermission(
            PermissionRole.Contributor,
            Permissions.WorkItemSelfAssign));
        Assert.False(PermissionMatrix.HasPermission(
            PermissionRole.Contributor,
            Permissions.WorkItemAssign));
    }

    [Fact]
    public void Organization_resource_rules_preserve_owner_admin_and_self_removal_boundaries()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        Assert.True(OrganizationAccessRules.CanInvite(OrganizationRole.Owner, OrganizationRole.Owner));
        Assert.False(OrganizationAccessRules.CanInvite(OrganizationRole.Admin, OrganizationRole.Owner));
        Assert.True(OrganizationAccessRules.CanRemove(
            ownerId,
            OrganizationRole.Owner,
            adminId,
            OrganizationRole.Admin));
        Assert.False(OrganizationAccessRules.CanRemove(
            adminId,
            OrganizationRole.Admin,
            ownerId,
            OrganizationRole.Owner));
        Assert.True(OrganizationAccessRules.CanRemove(
            memberId,
            OrganizationRole.Member,
            memberId,
            OrganizationRole.Member));
    }

    [Fact]
    public void Permission_catalog_should_not_contain_duplicates()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
    }
}
