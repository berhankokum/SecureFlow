namespace SecureFlow.Contracts.Audit;

public interface IAuditRequestContext
{
    Guid? ActorUserId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface IAuditWriter
{
    Task LoginFailureAsync(CancellationToken cancellationToken);
    void StagePasswordChanged(Guid userId);
    void StageAttachmentDownloaded(Guid organizationId, Guid attachmentId);
}

public static class AuditActions
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "LOGIN_FAILURE", "PASSWORD_CHANGED", "MEMBER_INVITED", "INVITATION_ACCEPTED",
        "INVITATION_REVOKED", "MEMBER_ADDED", "MEMBER_REMOVED", "ROLE_CHANGED",
        "PROJECT_MEMBER_ADDED", "PROJECT_MEMBER_REMOVED", "PROJECT_ARCHIVED",
        "WORK_ITEM_CREATED", "WORK_ITEM_UPDATED", "WORK_ITEM_ARCHIVED", "WORK_ITEM_ASSIGNED",
        "WORK_ITEM_UNASSIGNED", "WORK_ITEM_TRANSITIONED", "ATTACHMENT_UPLOADED",
        "ATTACHMENT_DELETED", "ATTACHMENT_DOWNLOADED",
    };
}
