using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed class GetInvitationHandler(
    IOrganizationsDbContext dbContext,
    IClock clock)
{
    public async Task<Result<OrganizationInvitationDetails>> HandleAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = InvitationTokenService.Hash(rawToken);

        var invitation = await dbContext.OrganizationInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<OrganizationInvitationDetails>(OrganizationErrors.InvitationNotFound);
        }

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(item => item.Id == invitation.OrganizationId, cancellationToken);

        return Result.Success(new OrganizationInvitationDetails(
            invitation.Id,
            organization.Id,
            organization.Name,
            organization.Slug,
            invitation.Email,
            invitation.Role.ToString().ToUpperInvariant(),
            invitation.ExpiresAt,
            GetStatus(invitation, clock.UtcNow)));
    }

    private static string GetStatus(Domain.OrganizationInvitation invitation, DateTimeOffset now)
    {
        if (invitation.IsAccepted)
        {
            return "ACCEPTED";
        }

        if (invitation.IsRevoked)
        {
            return "REVOKED";
        }

        return invitation.IsExpired(now) ? "EXPIRED" : "PENDING";
    }
}
