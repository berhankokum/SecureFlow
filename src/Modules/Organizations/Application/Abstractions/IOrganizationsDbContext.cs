using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Application.Abstractions;

public interface IOrganizationsDbContext
{
    DbSet<Organization> Organizations { get; }

    DbSet<OrganizationMember> OrganizationMembers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
