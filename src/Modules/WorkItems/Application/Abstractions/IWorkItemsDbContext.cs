using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Application.Abstractions;

public sealed record WorkItemProjectContext(
    Guid ProjectId,
    Guid OrganizationId,
    string ProjectKey,
    bool IsArchived,
    bool HasProjectAccess,
    IReadOnlyList<string> Permissions);

public interface IWorkItemsDbContext
{
    DbSet<WorkItem> WorkItems { get; }

    DbSet<Label> Labels { get; }

    DbSet<WorkItemLabel> WorkItemLabels { get; }

    DatabaseFacade Database { get; }

    Task<bool> LockProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<Guid?> FindProjectOrganizationIdAsync(Guid projectId, CancellationToken cancellationToken);

    Task<WorkItemProjectContext?> GetWorkItemProjectContextAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
