using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed class ListInvitationsHandler(
    IOrganizationsDbContext dbContext,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<OrganizationInvitationSummary>>> HandleAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await dbContext.OrganizationMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(member =>
                member.OrganizationId == organizationId
                && member.UserId == currentUserId
                && member.RemovedAt == null,
                cancellationToken);

        if (actor is null)
        {
            return Result.Failure<IReadOnlyList<OrganizationInvitationSummary>>(OrganizationErrors.NotFound);
        }

        if (!OrganizationAccessRules.CanListOrRevokeInvitations(actor.Role))
        {
            return Result.Failure<IReadOnlyList<OrganizationInvitationSummary>>(OrganizationErrors.Forbidden);
        }

        var now = clock.UtcNow;
        var invitations = await dbContext.OrganizationInvitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.OrganizationId == organizationId
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null
                && invitation.ExpiresAt > now)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var result = invitations.Select(invitation => new OrganizationInvitationSummary(
                invitation.Id,
                invitation.Email,
                invitation.Role.ToString().ToUpperInvariant(),
                invitation.ExpiresAt,
                invitation.CreatedAt,
                "PENDING"))
            .ToArray();

        return Result.Success<IReadOnlyList<OrganizationInvitationSummary>>(result);
    }
}
