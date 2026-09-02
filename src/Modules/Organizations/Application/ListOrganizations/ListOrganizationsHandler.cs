using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Modules.Organizations.Application.ListOrganizations;

public sealed class ListOrganizationsHandler(
    IOrganizationsDbContext dbContext,
    IEnumerable<IOrganizationPermissionContributor> permissionContributors)
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

        var result = new List<OrganizationSummary>(organizations.Count);
        foreach (var organization in organizations)
        {
            var permissions = new HashSet<string>(
                OrganizationAccessRules.GetPermissions(organization.Role),
                StringComparer.Ordinal);

            foreach (var contributor in permissionContributors)
            {
                permissions.UnionWith(await contributor.GetOrganizationScopedPermissionsAsync(
                    organization.Id,
                    currentUserId,
                    cancellationToken));
            }

            result.Add(new OrganizationSummary(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.LogoUrl,
                organization.Role.ToString().ToUpperInvariant(),
                permissions.Order(StringComparer.Ordinal).ToArray(),
                organization.CreatedAt,
                organization.ArchivedAt));
        }

        return result;
    }
}
