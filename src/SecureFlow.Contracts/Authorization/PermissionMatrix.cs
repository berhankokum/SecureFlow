namespace SecureFlow.Contracts.Authorization;

public static class PermissionMatrix
{
    private static readonly IReadOnlyDictionary<PermissionRole, IReadOnlySet<string>> Matrix =
        new Dictionary<PermissionRole, IReadOnlySet<string>>
        {
            [PermissionRole.Owner] = Set(Permissions.All),
            [PermissionRole.Admin] = Set(
                Permissions.OrganizationManage,
                Permissions.MemberView,
                Permissions.MemberInvite,
                Permissions.MemberRemove,
                Permissions.TeamCreate,
                Permissions.TeamUpdate,
                Permissions.TeamArchive,
                Permissions.TeamMemberManage,
                Permissions.ProjectCreate,
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
                Permissions.AuditView),
            [PermissionRole.Member] = Set(Permissions.MemberView),
            [PermissionRole.Manager] = Set(
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
                Permissions.AttachmentDelete),
            [PermissionRole.Contributor] = Set(
                Permissions.WorkItemCreate,
                Permissions.WorkItemUpdate,
                Permissions.WorkItemSelfAssign,
                Permissions.WorkItemTransition,
                Permissions.LabelAssign,
                Permissions.CommentCreate,
                Permissions.CommentUpdate,
                Permissions.CommentDelete,
                Permissions.AttachmentUpload,
                Permissions.AttachmentDelete),
            [PermissionRole.Viewer] = Set(),
        };

    public static bool HasPermission(PermissionRole role, string permission) =>
        Matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static IReadOnlyList<string> GetPermissions(PermissionRole role) =>
        Matrix.TryGetValue(role, out var permissions)
            ? permissions.Order(StringComparer.Ordinal).ToArray()
            : [];

    private static IReadOnlySet<string> Set(params IEnumerable<string> permissions) =>
        new HashSet<string>(permissions, StringComparer.Ordinal);
}
