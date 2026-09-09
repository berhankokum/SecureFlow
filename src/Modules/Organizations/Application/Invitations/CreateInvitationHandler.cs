using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SecureFlow.Contracts.Email;
using SecureFlow.Contracts.Identity;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.Organizations.Infrastructure;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Application.Invitations;

public sealed record CreateInvitationCommand(
    Guid OrganizationId,
    Guid CurrentUserId,
    string Email,
    OrganizationRole Role);

public sealed class CreateInvitationHandler(
    IOrganizationsDbContext dbContext,
    IUserDirectory userDirectory,
    IEmailSender emailSender,
    INotificationWriter notificationWriter,
    IRealtimePublisher realtimePublisher,
    IOptions<InvitationOptions> options,
    IClock clock,
    ILogger<CreateInvitationHandler> logger)
{
    public async Task<Result<OrganizationInvitationSummary>> HandleAsync(
        CreateInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await dbContext.OrganizationMembers.SingleOrDefaultAsync(member =>
            member.OrganizationId == command.OrganizationId
            && member.UserId == command.CurrentUserId
            && member.RemovedAt == null,
            cancellationToken);

        if (actor is null)
        {
            return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.NotFound);
        }

        if (!OrganizationAccessRules.CanInvite(actor.Role, command.Role))
        {
            return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.Forbidden);
        }

        var organization = await dbContext.Organizations.SingleOrDefaultAsync(
            item => item.Id == command.OrganizationId,
            cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.NotFound);
        }

        if (organization.IsArchived)
        {
            return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.Archived);
        }

        var normalizedEmail = OrganizationInvitation.NormalizeEmail(command.Email);
        var targetUser = await userDirectory.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (targetUser is not null)
        {
            var isActiveMember = await dbContext.OrganizationMembers.AnyAsync(member =>
                member.OrganizationId == command.OrganizationId
                && member.UserId == targetUser.Id
                && member.RemovedAt == null,
                cancellationToken);

            if (isActiveMember)
            {
                return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.ExistingMember);
            }
        }

        var now = clock.UtcNow;
        var existingInvitations = await dbContext.OrganizationInvitations
            .Where(invitation =>
                invitation.OrganizationId == command.OrganizationId
                && invitation.Email == normalizedEmail
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null
                && invitation.ExpiredAt == null)
            .ToArrayAsync(cancellationToken);

        foreach (var existingInvitation in existingInvitations)
        {
            if (existingInvitation.IsExpired(now))
            {
                existingInvitation.MarkExpired(now);
            }
            else
            {
                existingInvitation.Revoke(now);
            }
        }

        var token = InvitationTokenService.Generate();
        var invitation = OrganizationInvitation.Create(
            command.OrganizationId,
            normalizedEmail,
            command.Role,
            token.Hash,
            now,
            now.AddHours(options.Value.ExpirationHours),
            command.CurrentUserId);

        dbContext.OrganizationInvitations.Add(invitation);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsInvitationUniquenessConflict(exception))
        {
            return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.ConcurrentInvitation);
        }

        var invitationLink =
            $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(token.RawValue)}";

        try
        {
            await emailSender.SendEmailAsync(
                normalizedEmail,
                $"SecureFlow - {organization.Name} daveti",
                $"<p><strong>{HtmlEncoder.Default.Encode(organization.Name)}</strong> organization'ına katılmak için "
                + $"<a href=\"{HtmlEncoder.Default.Encode(invitationLink)}\">daveti kabul edin</a>.</p>"
                + $"<p>Bu bağlantı {options.Value.ExpirationHours} saat geçerlidir ve tek kullanımlıdır.</p>",
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Invitation email delivery failed for invitation {InvitationId} in organization {OrganizationId}.",
                invitation.Id,
                invitation.OrganizationId);

            invitation.Revoke(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<OrganizationInvitationSummary>(OrganizationErrors.EmailDeliveryFailed);
        }

        if (targetUser is not null && targetUser.Id != command.CurrentUserId)
        {
            var notificationId = notificationWriter.Add(new NotificationWriteRequest(
                command.OrganizationId,
                targetUser.Id,
                NotificationTypes.InvitationReceived,
                "Organization daveti",
                $"{organization.Name} organization'ına davet edildiniz.",
                NotificationResourceTypes.OrganizationInvitation,
                invitation.Id,
                invitation.Id,
                now));
            await dbContext.SaveChangesAsync(cancellationToken);
            await realtimePublisher.PublishNotificationCreatedAsync(
                targetUser.Id,
                new NotificationCreatedRealtimeEvent(notificationId, notificationId, now),
                cancellationToken);
        }

        return Result.Success(ToSummary(invitation, "PENDING"));
    }

    private static OrganizationInvitationSummary ToSummary(
        OrganizationInvitation invitation,
        string status) =>
        new(
            invitation.Id,
            invitation.Email,
            invitation.Role.ToString().ToUpperInvariant(),
            invitation.ExpiresAt,
            invitation.CreatedAt,
            status);

    private static bool IsInvitationUniquenessConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_OrganizationInvitations_PendingEmail" or "UX_OrganizationInvitations_TokenHash",
        };
}
