namespace SecureFlow.Contracts.Authorization;

public static class Permissions
{
    public const string OrganizationManage = "organization.manage";
    public const string OrganizationArchive = "organization.archive";

    public const string MemberView = "member.view";
    public const string MemberInvite = "member.invite";
    public const string MemberRemove = "member.remove";
    public const string MemberRoleChange = "member.role.change";

    public const string TeamCreate = "team.create";
    public const string TeamUpdate = "team.update";
    public const string TeamArchive = "team.archive";
    public const string TeamMemberManage = "team.member.manage";

    public const string ProjectCreate = "project.create";
    public const string ProjectUpdate = "project.update";
    public const string ProjectArchive = "project.archive";

    public const string WorkItemCreate = "workitem.create";
    public const string WorkItemUpdate = "workitem.update";
    public const string WorkItemArchive = "workitem.archive";
    public const string WorkItemAssign = "workitem.assign";
    public const string WorkItemSelfAssign = "workitem.self_assign";
    public const string WorkItemTransition = "workitem.transition";
    public const string WorkItemApprove = "workitem.approve";

    public const string LabelCreate = "label.create";
    public const string LabelUpdate = "label.update";
    public const string LabelDelete = "label.delete";
    public const string LabelAssign = "label.assign";

    public const string CommentCreate = "comment.create";
    public const string CommentUpdate = "comment.update";
    public const string CommentDelete = "comment.delete";

    public const string AttachmentUpload = "attachment.upload";
    public const string AttachmentDelete = "attachment.delete";

    public const string AuditView = "audit.view";

    public static IReadOnlyList<string> All { get; } =
    [
        OrganizationManage,
        OrganizationArchive,
        MemberView,
        MemberInvite,
        MemberRemove,
        MemberRoleChange,
        TeamCreate,
        TeamUpdate,
        TeamArchive,
        TeamMemberManage,
        ProjectCreate,
        ProjectUpdate,
        ProjectArchive,
        WorkItemCreate,
        WorkItemUpdate,
        WorkItemArchive,
        WorkItemAssign,
        WorkItemSelfAssign,
        WorkItemTransition,
        WorkItemApprove,
        LabelCreate,
        LabelUpdate,
        LabelDelete,
        LabelAssign,
        CommentCreate,
        CommentUpdate,
        CommentDelete,
        AttachmentUpload,
        AttachmentDelete,
        AuditView,
    ];
}
