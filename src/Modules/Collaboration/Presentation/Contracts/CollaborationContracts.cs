using FluentValidation;
using SecureFlow.Modules.Collaboration.Application.Models;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Presentation.Contracts;

public sealed record CommentContentRequest(
    string Content,
    IReadOnlyList<Guid>? MentionedUserIds = null);

public sealed class CommentContentRequestValidator : AbstractValidator<CommentContentRequest>
{
    public CommentContentRequestValidator()
    {
        RuleFor(request => request.Content)
            .NotEmpty()
            .MaximumLength(Comment.ContentMaxLength);
        RuleFor(request => request.MentionedUserIds)
            .Must(userIds => userIds is null || userIds.Count <= 20)
            .WithMessage("Bir yorum en fazla 20 kullanıcı mention edebilir.");
    }
}

public sealed record CommentResponse(
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
    IReadOnlyList<MentionResponse> Mentions)
{
    public static CommentResponse From(CommentSummary comment) => new(
        comment.Id,
        comment.OrganizationId,
        comment.WorkItemId,
        comment.AuthorId,
        comment.AuthorEmail,
        comment.Content,
        comment.CreatedAt,
        comment.UpdatedAt,
        comment.DeletedAt,
        comment.CanUpdate,
        comment.CanDelete,
        comment.Mentions.Select(mention => new MentionResponse(mention.UserId, mention.Email)).ToArray());
}

public sealed record MentionResponse(Guid UserId, string Email);

public sealed record ActivityResponse(
    Guid Id,
    Guid OrganizationId,
    Guid? ProjectId,
    Guid? WorkItemId,
    Guid ActorId,
    string ActorEmail,
    string Type,
    IReadOnlyDictionary<string, string?> Metadata,
    DateTimeOffset CreatedAt)
{
    public static ActivityResponse From(ActivitySummary activity) => new(
        activity.Id,
        activity.OrganizationId,
        activity.ProjectId,
        activity.WorkItemId,
        activity.ActorId,
        activity.ActorEmail,
        activity.Type,
        activity.Metadata,
        activity.CreatedAt);
}
