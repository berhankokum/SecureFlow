using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed record AcceptInvitationCommand(string RawToken, Guid CurrentUserId);

public sealed class AcceptInvitationHandler(
    IOrganizationsDbContext dbContext,
    IUserDirectory userDirectory,
    IClock clock)
{
    public async Task<Result<AcceptedInvitationSummary>> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = InvitationTokenService.Hash(command.RawToken);
        var invitation = await dbContext.OrganizationInvitations.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash,
            cancellationToken);

        return await AcceptAsync(invitation, command.CurrentUserId, cancellationToken);
    }

    public async Task<Result<AcceptedInvitationSummary>> HandleReceivedAsync(
        Guid invitationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userDirectory.FindByIdAsync(currentUserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationNotFound);
        }

        var email = OrganizationInvitation.NormalizeEmail(user.Email);
        var invitation = await dbContext.OrganizationInvitations.SingleOrDefaultAsync(
            item => item.Id == invitationId && item.Email == email, cancellationToken);
        return await AcceptAsync(invitation, currentUserId, cancellationToken);
    }

    private async Task<Result<AcceptedInvitationSummary>> AcceptAsync(
        OrganizationInvitation? invitation,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (invitation is null)
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationNotFound);
        }

        var now = clock.UtcNow;
        if (invitation.IsAccepted)
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationAlreadyUsed);
        }

        if (invitation.IsRevoked)
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationRevoked);
        }

        if (invitation.IsExpired(now))
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationExpired);
        }

        var currentUser = await userDirectory.FindByIdAsync(currentUserId, cancellationToken);
        if (currentUser is null
            || !string.Equals(
                OrganizationInvitation.NormalizeEmail(currentUser.Email),
                invitation.Email,
                StringComparison.Ordinal))
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationEmailMismatch);
        }

        var organization = await dbContext.Organizations.SingleAsync(
            item => item.Id == invitation.OrganizationId,
            cancellationToken);

        if (organization.IsArchived)
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.Archived);
        }

        var membership = await dbContext.OrganizationMembers.SingleOrDefaultAsync(member =>
            member.OrganizationId == invitation.OrganizationId
            && member.UserId == currentUserId,
            cancellationToken);

        if (membership is { IsActive: true })
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.ExistingMember);
        }

        invitation.Accept(now);

        if (membership is null)
        {
            membership = OrganizationMember.CreateFromInvitation(
                invitation.OrganizationId,
                currentUserId,
                invitation.Role,
                now,
                invitation.CreatedBy);
            dbContext.OrganizationMembers.Add(membership);
        }
        else
        {
            membership.Reactivate(invitation.Role, now, invitation.CreatedBy);
        }

        try
        {
            // Invitation accept + membership create/reactivate tek transaction.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.InvitationAlreadyUsed);
        }
        catch (DbUpdateException exception) when (IsDuplicateMembership(exception))
        {
            return Result.Failure<AcceptedInvitationSummary>(OrganizationErrors.ExistingMember);
        }

        return Result.Success(new AcceptedInvitationSummary(
            organization.Id,
            organization.Name,
            organization.Slug,
            membership.Role.ToString().ToUpperInvariant()));
    }

    private static bool IsDuplicateMembership(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_OrganizationMembers_OrganizationId_UserId",
        };
}
