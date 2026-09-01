using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Application.Abstractions;

public interface IOrganizationsDbContext
{
    DbSet<Organization> Organizations { get; }

    DbSet<OrganizationMember> OrganizationMembers { get; }

    DbSet<OrganizationInvitation> OrganizationInvitations { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
