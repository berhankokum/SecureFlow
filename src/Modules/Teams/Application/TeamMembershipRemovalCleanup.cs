using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Organizations;
using SecureFlow.Modules.Teams.Application.Abstractions;

namespace SecureFlow.Modules.Teams.Application;

public sealed class TeamMembershipRemovalCleanup(ITeamsDbContext dbContext)
    : IOrganizationMembershipRemovalCleanup
{
    public async Task RemoveActiveRelationshipsAsync(
        Guid organizationId,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await dbContext.TeamMembers
            .Where(member => member.UserId == userId
                && dbContext.Teams.Any(team =>
                    team.Id == member.TeamId
                    && team.OrganizationId == organizationId
                    && team.ArchivedAt == null))
            .ToArrayAsync(cancellationToken);

        dbContext.TeamMembers.RemoveRange(memberships);
    }
}
