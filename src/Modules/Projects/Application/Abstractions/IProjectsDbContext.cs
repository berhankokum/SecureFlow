using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Modules.Projects.Application.Abstractions;

public interface IProjectsDbContext
{
    DbSet<Project> Projects { get; }

    DbSet<ProjectMember> ProjectMembers { get; }

    DatabaseFacade Database { get; }

    Task<PermissionRole?> GetActiveOrganizationRoleAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsOrganizationArchivedAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<bool> IsActiveOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
