namespace SecureFlow.Modules.Collaboration.Application.Models;

public sealed record CommentSummary(
    Guid Id,
    Guid OrganizationId,
    Guid WorkItemId,
    Guid AuthorId,
    string AuthorEmail,
    string? Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    bool CanUpdate,
    bool CanDelete,
    IReadOnlyList<MentionSummary> Mentions);

public sealed record MentionSummary(Guid UserId, string Email);

public sealed record ActivitySummary(
    Guid Id,
    Guid OrganizationId,
    Guid? ProjectId,
    Guid? WorkItemId,
    Guid ActorId,
    string ActorEmail,
    string Type,
    IReadOnlyDictionary<string, string?> Metadata,
    DateTimeOffset CreatedAt);
