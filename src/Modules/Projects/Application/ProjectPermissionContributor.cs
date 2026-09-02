using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Modules.Projects.Application;

public sealed class ProjectPermissionContributor(IProjectsDbContext dbContext)
    : IOrganizationPermissionContributor
{
    private static readonly string[] ManagerOrganizationPermissions =
    [
        Permissions.TeamCreate,
        Permissions.TeamUpdate,
        Permissions.TeamArchive,
        Permissions.TeamMemberManage,
    ];

    public async Task<IReadOnlyCollection<string>> GetOrganizationScopedPermissionsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var isManager = await dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(member => member.UserId == userId
                && member.Role == ProjectRole.Manager
                && dbContext.Projects.Any(project =>
                    project.Id == member.ProjectId
                    && project.OrganizationId == organizationId
                    && project.ArchivedAt == null),
                cancellationToken);

        return isManager ? ManagerOrganizationPermissions : [];
    }
}
