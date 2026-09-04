namespace SecureFlow.Contracts.Notifications;

public static class NotificationTypes
{
    public const string WorkItemAssigned = "WORK_ITEM_ASSIGNED";
    public const string WorkItemStatusChanged = "WORK_ITEM_STATUS_CHANGED";
    public const string Mentioned = "MENTIONED";
    public const string CommentAdded = "COMMENT_ADDED";
    public const string DueDateApproaching = "DUE_DATE_APPROACHING";
    public const string InvitationReceived = "INVITATION_RECEIVED";
}

public static class NotificationResourceTypes
{
    public const string WorkItem = "WORK_ITEM";
    public const string OrganizationInvitation = "ORGANIZATION_INVITATION";
}

public sealed record NotificationWriteRequest(
    Guid OrganizationId,
    Guid UserId,
    string Type,
    string Title,
    string Message,
    string ResourceType,
    Guid? ResourceId,
    Guid SourceEventId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Producer modüllerin aynı scoped DbContext unit of work'üne Notification eklemesini sağlar.
/// Notification entity ve persistence tipleri producer modüllere sızdırılmaz.
/// </summary>
public interface INotificationWriter
{
    Guid Add(NotificationWriteRequest request);
}
