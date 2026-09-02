using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Teams.Domain;

namespace SecureFlow.Modules.Teams.Application.Abstractions;

public interface ITeamsDbContext
{
    DbSet<Team> Teams { get; }

    DbSet<TeamMember> TeamMembers { get; }

    DatabaseFacade Database { get; }

    Task<bool> IsOrganizationArchivedAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<bool> IsActiveOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
