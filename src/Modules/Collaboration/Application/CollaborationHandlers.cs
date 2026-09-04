using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Activity;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Collaboration.Application.Abstractions;
using SecureFlow.Modules.Collaboration.Application.Models;
using SecureFlow.Modules.Collaboration.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration.Application;

public sealed record CreateCommentCommand(Guid WorkItemId, Guid AuthorId, string Content);

public sealed record UpdateCommentCommand(Guid CommentId, Guid CurrentUserId, string Content);

public sealed class CollaborationScopeHandler(ICollaborationDbContext dbContext)
{
    public Task<CollaborationResourceScope?> FindWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken) =>
        dbContext.FindWorkItemScopeAsync(workItemId, cancellationToken);

    public async Task<CollaborationResourceScope?> FindCommentAsync(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var workItemId = await dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.Id == commentId)
            .Select(comment => (Guid?)comment.WorkItemId)
            .SingleOrDefaultAsync(cancellationToken);
        return workItemId is null
            ? null
            : await dbContext.FindWorkItemScopeAsync(workItemId.Value, cancellationToken);
    }
}

public sealed class CommentHandlers(
    ICollaborationDbContext dbContext,
    IUserDirectory userDirectory,
    IActivityWriter activityWriter,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<CommentSummary>>> ListAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var access = await dbContext.GetCollaborationAccessAsync(
            workItemId,
            currentUserId,
            cancellationToken);
        if (access?.HasProjectAccess != true)
        {
            return Result.Failure<IReadOnlyList<CommentSummary>>(CollaborationErrors.NotFound);
        }

        var comments = await dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.WorkItemId == workItemId
                && comment.OrganizationId == access.OrganizationId)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .ToArrayAsync(cancellationToken);
        var authorEmails = await userDirectory.GetEmailsByUserIdsAsync(
            comments.Select(comment => comment.AuthorId).Distinct().ToArray(),
            cancellationToken);
        return Result.Success<IReadOnlyList<CommentSummary>>(
            comments.Select(comment => ToSummary(comment, access, currentUserId, authorEmails)).ToArray());
    }

    public async Task<Result<CommentSummary>> CreateAsync(
        CreateCommentCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var accessResult = await RequireMutableAccessAsync(
            command.WorkItemId,
            command.AuthorId,
            Permissions.CommentCreate,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result.Failure<CommentSummary>(accessResult.Error);
        }

        var now = clock.UtcNow;
        var comment = Comment.Create(
            accessResult.Value.OrganizationId,
            command.WorkItemId,
            command.AuthorId,
            command.Content,
            now);
        dbContext.Comments.Add(comment);
        activityWriter.Add(new ActivityWriteRequest(
            accessResult.Value.OrganizationId,
            accessResult.Value.ProjectId,
            command.WorkItemId,
            command.AuthorId,
            ActivityTypes.CommentCreated,
            new Dictionary<string, string?> { ["commentId"] = comment.Id.ToString() },
            now));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var author = await userDirectory.FindByIdAsync(command.AuthorId, cancellationToken);
        return Result.Success(ToSummary(
            comment,
            accessResult.Value,
            command.AuthorId,
            new Dictionary<Guid, string>
            {
                [command.AuthorId] = author?.Email ?? "Bilinmeyen kullanıcı",
            }));
    }

    public async Task<Result> UpdateAsync(
        UpdateCommentCommand command,
        CancellationToken cancellationToken)
    {
        var scope = await FindCommentForMutationAsync(command.CommentId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure(CollaborationErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var accessResult = await RequireMutableAccessAsync(
            scope.Value.WorkItemId,
            command.CurrentUserId,
            Permissions.CommentUpdate,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result.Failure(accessResult.Error);
        }

        var comment = await LoadScopedCommentAsync(
            command.CommentId,
            accessResult.Value,
            cancellationToken);
        if (comment is null)
        {
            return Result.Failure(CollaborationErrors.NotFound);
        }

        if (comment.IsDeleted)
        {
            return Result.Failure(CollaborationErrors.CommentDeleted);
        }

        // Moderation başka kullanıcının içeriğini değiştirmez; yalnız soft-delete yapabilir.
        if (comment.AuthorId != command.CurrentUserId)
        {
            return Result.Failure(CollaborationErrors.Forbidden);
        }

        comment.Update(command.Content, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(
        Guid commentId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var scope = await FindCommentForMutationAsync(commentId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure(CollaborationErrors.NotFound);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var accessResult = await RequireMutableAccessAsync(
            scope.Value.WorkItemId,
            currentUserId,
            Permissions.CommentDelete,
            cancellationToken);
        if (accessResult.IsFailure)
        {
            return Result.Failure(accessResult.Error);
        }

        var comment = await LoadScopedCommentAsync(commentId, accessResult.Value, cancellationToken);
        if (comment is null)
        {
            return Result.Failure(CollaborationErrors.NotFound);
        }

        if (comment.AuthorId != currentUserId && !accessResult.Value.CanModerateComments)
        {
            return Result.Failure(CollaborationErrors.Forbidden);
        }

        comment.Delete(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<CollaborationAccessContext>> RequireMutableAccessAsync(
        Guid workItemId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken)
    {
        var resource = await dbContext.FindWorkItemScopeAsync(workItemId, cancellationToken);
        if (resource is null || !await dbContext.LockProjectAsync(resource.ProjectId, cancellationToken))
        {
            return Result.Failure<CollaborationAccessContext>(CollaborationErrors.NotFound);
        }

        if (!await dbContext.LockWorkItemAsync(workItemId, cancellationToken))
        {
            return Result.Failure<CollaborationAccessContext>(CollaborationErrors.NotFound);
        }

        var access = await dbContext.GetCollaborationAccessAsync(workItemId, userId, cancellationToken);
        if (access?.HasProjectAccess != true)
        {
            return Result.Failure<CollaborationAccessContext>(CollaborationErrors.NotFound);
        }

        if (!access.Permissions.Contains(permission, StringComparer.Ordinal))
        {
            return Result.Failure<CollaborationAccessContext>(CollaborationErrors.Forbidden);
        }

        if (access.IsProjectArchived)
        {
            return Result.Failure<CollaborationAccessContext>(CollaborationErrors.ProjectArchived);
        }

        return access.IsWorkItemArchived
            ? Result.Failure<CollaborationAccessContext>(CollaborationErrors.WorkItemArchived)
            : Result.Success(access);
    }

    private async Task<(Guid WorkItemId, Guid OrganizationId)?> FindCommentForMutationAsync(
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = await dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.Id == commentId)
            .Select(comment => new { comment.WorkItemId, comment.OrganizationId })
            .SingleOrDefaultAsync(cancellationToken);
        return comment is null
            ? null
            : (comment.WorkItemId, comment.OrganizationId);
    }

    private Task<Comment?> LoadScopedCommentAsync(
        Guid commentId,
        CollaborationAccessContext access,
        CancellationToken cancellationToken) =>
        dbContext.Comments.SingleOrDefaultAsync(
            comment => comment.Id == commentId
                && comment.WorkItemId == access.WorkItemId
                && comment.OrganizationId == access.OrganizationId,
            cancellationToken);

    private static CommentSummary ToSummary(
        Comment comment,
        CollaborationAccessContext access,
        Guid currentUserId,
        IReadOnlyDictionary<Guid, string> authorEmails)
    {
        var mutable = !access.IsProjectArchived && !access.IsWorkItemArchived && !comment.IsDeleted;
        var canUpdate = mutable
            && comment.AuthorId == currentUserId
            && access.Permissions.Contains(Permissions.CommentUpdate, StringComparer.Ordinal);
        var canDelete = mutable
            && access.Permissions.Contains(Permissions.CommentDelete, StringComparer.Ordinal)
            && (comment.AuthorId == currentUserId || access.CanModerateComments);
        return new CommentSummary(
            comment.Id,
            comment.OrganizationId,
            comment.WorkItemId,
            comment.AuthorId,
            authorEmails.GetValueOrDefault(comment.AuthorId, "Bilinmeyen kullanıcı"),
            comment.IsDeleted ? null : comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.DeletedAt,
            canUpdate,
            canDelete);
    }
}

public sealed class ActivityHandlers(
    ICollaborationDbContext dbContext,
    IUserDirectory userDirectory)
{
    public async Task<Result<IReadOnlyList<ActivitySummary>>> ListAsync(
        Guid workItemId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var access = await dbContext.GetCollaborationAccessAsync(
            workItemId,
            currentUserId,
            cancellationToken);
        if (access?.HasProjectAccess != true)
        {
            return Result.Failure<IReadOnlyList<ActivitySummary>>(CollaborationErrors.NotFound);
        }

        var activities = await dbContext.ActivityEvents
            .AsNoTracking()
            .Where(activity => activity.WorkItemId == workItemId
                && activity.OrganizationId == access.OrganizationId)
            .OrderByDescending(activity => activity.CreatedAt)
            .ThenByDescending(activity => activity.Id)
            .ToArrayAsync(cancellationToken);
        var rows = activities
            .Select(activity => (Activity: activity, Metadata: DeserializeMetadata(activity.Metadata)))
            .ToArray();
        var referencedUserIds = rows.SelectMany(row => new[]
            {
                ReadUserId(row.Metadata, "assigneeId"),
                ReadUserId(row.Metadata, "previousAssigneeId"),
            })
            .OfType<Guid>();
        var actorEmails = await userDirectory.GetEmailsByUserIdsAsync(
            activities.Select(activity => activity.ActorId)
                .Concat(referencedUserIds)
                .Distinct()
                .ToArray(),
            cancellationToken);
        return Result.Success<IReadOnlyList<ActivitySummary>>(rows.Select(row => new ActivitySummary(
            row.Activity.Id,
            row.Activity.OrganizationId,
            row.Activity.ProjectId,
            row.Activity.WorkItemId,
            row.Activity.ActorId,
            actorEmails.GetValueOrDefault(row.Activity.ActorId, "Bilinmeyen kullanıcı"),
            row.Activity.Type,
            EnrichUserMetadata(row.Metadata, actorEmails),
            row.Activity.CreatedAt)).ToArray());
    }

    private static IReadOnlyDictionary<string, string?> DeserializeMetadata(string metadata) =>
        JsonSerializer.Deserialize<Dictionary<string, string?>>(metadata)
        ?? new Dictionary<string, string?>();

    private static Guid? ReadUserId(IReadOnlyDictionary<string, string?> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && Guid.TryParse(value, out var id) ? id : null;

    private static IReadOnlyDictionary<string, string?> EnrichUserMetadata(
        IReadOnlyDictionary<string, string?> metadata,
        IReadOnlyDictionary<Guid, string> emails)
    {
        var enriched = new Dictionary<string, string?>(metadata, StringComparer.Ordinal);
        if (ReadUserId(metadata, "assigneeId") is { } assigneeId)
        {
            enriched["assigneeEmail"] = emails.GetValueOrDefault(assigneeId, "Bilinmeyen kullanıcı");
        }

        if (ReadUserId(metadata, "previousAssigneeId") is { } previousAssigneeId)
        {
            enriched["previousAssigneeEmail"] = emails.GetValueOrDefault(
                previousAssigneeId,
                "Bilinmeyen kullanıcı");
        }

        return enriched;
    }
}
