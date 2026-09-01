using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;

namespace SecureFlow.Modules.Organizations.Application.GetOrganization;

public sealed class GetOrganizationHandler(IOrganizationsDbContext dbContext)
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

        return organization is null
            ? null
            : new OrganizationSummary(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.LogoUrl,
                organization.Role.ToString().ToUpperInvariant(),
                organization.CreatedAt,
                organization.ArchivedAt);
    }
}
