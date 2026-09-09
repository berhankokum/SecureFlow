using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed record RevokeInvitationCommand(
    Guid OrganizationId,
    Guid InvitationId,
    Guid CurrentUserId);

public sealed class RevokeInvitationHandler(
    IOrganizationsDbContext dbContext,
    IClock clock)
{
    public async Task<Result> HandleAsync(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await dbContext.OrganizationMembers.SingleOrDefaultAsync(member =>
            member.OrganizationId == command.OrganizationId
            && member.UserId == command.CurrentUserId
            && member.RemovedAt == null,
            cancellationToken);

        if (actor is null)
        {
            return Result.Failure(OrganizationErrors.NotFound);
        }

        if (!OrganizationAccessRules.CanListOrRevokeInvitations(actor.Role))
        {
            return Result.Failure(OrganizationErrors.Forbidden);
        }

        var invitation = await dbContext.OrganizationInvitations.SingleOrDefaultAsync(item =>
            item.Id == command.InvitationId
            && item.OrganizationId == command.OrganizationId,
            cancellationToken);

        if (invitation is null)
        {
            return Result.Failure(OrganizationErrors.InvitationNotFound);
        }

        if (invitation.IsAccepted)
        {
            return Result.Failure(OrganizationErrors.InvitationAlreadyUsed);
        }

        if (invitation.IsExpired(clock.UtcNow))
        {
            return Result.Failure(OrganizationErrors.InvitationExpired);
        }

        invitation.Revoke(clock.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(OrganizationErrors.InvitationAlreadyUsed);
        }

        return Result.Success();
    }
}
