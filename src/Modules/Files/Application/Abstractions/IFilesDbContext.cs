using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Files.Domain;

namespace SecureFlow.Modules.Files.Application.Abstractions;

public sealed record FileResourceScope(Guid WorkItemId, Guid ProjectId, Guid OrganizationId);

public sealed record FileAccessContext(
    Guid WorkItemId,
    Guid ProjectId,
    Guid OrganizationId,
    bool IsWorkItemArchived,
    bool IsProjectArchived,
    bool HasProjectAccess,
    bool CanDeleteAnyAttachment,
    IReadOnlyList<string> Permissions);

public interface IFilesDbContext
{
    DbSet<Attachment> Attachments { get; }

    DatabaseFacade Database { get; }

    Task<bool> LockProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<bool> LockWorkItemAsync(Guid workItemId, CancellationToken cancellationToken);

    Task<FileResourceScope?> FindWorkItemFileScopeAsync(
        Guid workItemId,
        CancellationToken cancellationToken);

    Task<FileAccessContext?> GetFileAccessAsync(
        Guid workItemId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
