using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Application.Authorization;

namespace SecureFlow.Modules.Organizations.Application.ListOrganizations;

public sealed class ListOrganizationsHandler(IOrganizationsDbContext dbContext)
{
    public async Task<IReadOnlyList<OrganizationSummary>> HandleAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var organizations = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.UserId == currentUserId && member.RemovedAt == null)
            .Join(
                dbContext.Organizations.AsNoTracking(),
                member => member.OrganizationId,
                organization => organization.Id,
                (member, organization) => new
                {
                    organization.Id,
                    organization.Name,
                    organization.Slug,
                    organization.LogoUrl,
                    member.Role,
                    organization.CreatedAt,
                    organization.ArchivedAt,
                })
            .OrderBy(organization => organization.Name)
            .ToListAsync(cancellationToken);

        return organizations
            .Select(organization => new OrganizationSummary(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.LogoUrl,
                organization.Role.ToString().ToUpperInvariant(),
                OrganizationAccessRules.GetPermissions(organization.Role),
                organization.CreatedAt,
                organization.ArchivedAt))
            .ToArray();
    }
}
