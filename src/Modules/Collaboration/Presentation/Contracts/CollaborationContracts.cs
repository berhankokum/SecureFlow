using FluentValidation;
using SecureFlow.Modules.Collaboration.Application.Models;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Presentation.Contracts;

public sealed record CommentContentRequest(string Content);

public sealed class CommentContentRequestValidator : AbstractValidator<CommentContentRequest>
{
    public CommentContentRequestValidator()
    {
        RuleFor(request => request.Content)
            .NotEmpty()
            .MaximumLength(Comment.ContentMaxLength);
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
    bool CanDelete)
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
        comment.CanDelete);
}

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
