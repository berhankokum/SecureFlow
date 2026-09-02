using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Modules.Organizations.Application.GetOrganization;

public sealed class GetOrganizationHandler(
    IOrganizationsDbContext dbContext,
    IEnumerable<IOrganizationPermissionContributor> permissionContributors)
{
    public async Task<OrganizationSummary?> HandleAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member =>
                member.OrganizationId == organizationId
                && member.UserId == currentUserId
                && member.RemovedAt == null)
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
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return null;
        }

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

        return new OrganizationSummary(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.LogoUrl,
                organization.Role.ToString().ToUpperInvariant(),
                permissions.Order(StringComparer.Ordinal).ToArray(),
                organization.CreatedAt,
                organization.ArchivedAt);
    }
}
