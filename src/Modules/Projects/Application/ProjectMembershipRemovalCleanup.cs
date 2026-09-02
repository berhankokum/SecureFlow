using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Organizations;
using SecureFlow.Modules.Projects.Application.Abstractions;

namespace SecureFlow.Modules.Projects.Application;

public sealed class ProjectMembershipRemovalCleanup(IProjectsDbContext dbContext)
    : IOrganizationMembershipRemovalCleanup
{
    public async Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await dbContext.ProjectMembers
            .Where(member => member.UserId == userId
                && dbContext.Projects.Any(project =>
                    project.Id == member.ProjectId
                    && project.OrganizationId == organizationId
                    && project.ArchivedAt == null))
            .ToArrayAsync(cancellationToken);

        dbContext.ProjectMembers.RemoveRange(memberships);
    }
}
