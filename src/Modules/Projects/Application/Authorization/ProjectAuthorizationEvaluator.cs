using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Modules.Projects.Application.Authorization;

public sealed record ProjectAccessEvaluation(
    PermissionRole OrganizationRole,
    ProjectRole? ProjectRole,
    bool HasProjectAccess,
    IReadOnlyList<string> Permissions);

public sealed class ProjectAuthorizationEvaluator(IProjectsDbContext dbContext)
{
    public async Task<ProjectAccessEvaluation?> EvaluateAsync(
        Guid projectId,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationRole = await dbContext.GetActiveOrganizationRoleAsync(
            organizationId,
            userId,
            cancellationToken);
        if (organizationRole is null)
        {
            return null;
        }

        var projectRole = await dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

        var hasAccess = organizationRole is PermissionRole.Owner or PermissionRole.Admin
            || projectRole is not null;

        return new ProjectAccessEvaluation(
            organizationRole.Value,
            projectRole,
            hasAccess,
            GetPermissions(organizationRole.Value, projectRole));
    }

    public static IReadOnlyList<string> GetPermissions(
        PermissionRole organizationRole,
        ProjectRole? projectRole)
    {
        var permissions = new HashSet<string>(
            PermissionMatrix.GetPermissions(organizationRole),
            StringComparer.Ordinal);

        if (projectRole is not null)
        {
            permissions.UnionWith(PermissionMatrix.GetPermissions(ToPermissionRole(projectRole.Value)));
        }

        return permissions.Order(StringComparer.Ordinal).ToArray();
    }

    public static PermissionRole ToPermissionRole(ProjectRole role) => role switch
    {
        ProjectRole.Manager => PermissionRole.Manager,
        ProjectRole.Contributor => PermissionRole.Contributor,
        ProjectRole.Viewer => PermissionRole.Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown project role."),
    };
}
