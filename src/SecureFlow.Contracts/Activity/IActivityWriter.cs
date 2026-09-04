namespace SecureFlow.Contracts.Activity;

public static class ActivityTypes
{
    public const string WorkItemCreated = "WORK_ITEM_CREATED";
    public const string WorkItemAssigned = "WORK_ITEM_ASSIGNED";
    public const string WorkItemUnassigned = "WORK_ITEM_UNASSIGNED";
    public const string WorkItemTransitioned = "WORK_ITEM_TRANSITIONED";
    public const string LabelAssigned = "LABEL_ASSIGNED";
    public const string LabelRemoved = "LABEL_REMOVED";
    public const string CommentCreated = "COMMENT_CREATED";
}

public sealed record ActivityWriteRequest(
    Guid OrganizationId,
    Guid? ProjectId,
    Guid? WorkItemId,
    Guid ActorId,
    string Type,
    IReadOnlyDictionary<string, string?> Metadata,
    DateTimeOffset CreatedAt);

/// <summary>
/// Modüllerin aynı scoped DbContext transaction'ına ActivityEvent eklemesini sağlar.
/// Persistence ve Collaboration entity tipleri producer modüllere sızdırılmaz.
/// </summary>
public interface IActivityWriter
{
    Guid Add(ActivityWriteRequest request);
}
