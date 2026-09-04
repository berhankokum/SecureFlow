using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.Modules.Collaboration.Application.Abstractions;

public sealed record CollaborationResourceScope(
    Guid WorkItemId,
    Guid ProjectId,
    Guid OrganizationId);

public sealed record CollaborationAccessContext(
    Guid WorkItemId,
    Guid ProjectId,
    Guid OrganizationId,
    Guid ReporterId,
    Guid? AssigneeId,
    bool IsWorkItemArchived,
    bool IsProjectArchived,
    bool HasProjectAccess,
    bool CanModerateComments,
    IReadOnlyList<string> Permissions);

public interface ICollaborationDbContext
{
    DbSet<Comment> Comments { get; }

    DbSet<ActivityEvent> ActivityEvents { get; }

    DbSet<Mention> Mentions { get; }

    DatabaseFacade Database { get; }

    Task<bool> LockProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> LockWorkItemAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<CollaborationResourceScope?> FindWorkItemScopeAsync(
        Guid workItemId,
        CancellationToken cancellationToken);

    Task<CollaborationAccessContext?> GetCollaborationAccessAsync(
        Guid workItemId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetMentionCandidateUserIdsAsync(
        Guid projectId,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
