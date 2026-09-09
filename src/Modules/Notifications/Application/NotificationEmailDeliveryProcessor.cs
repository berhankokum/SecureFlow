using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SecureFlow.Contracts.Email;
using SecureFlow.Contracts.Identity;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.Modules.Notifications.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Application;

public sealed class NotificationEmailDeliveryProcessor(
    INotificationsDbContext dbContext,
    IUserDirectory userDirectory,
    IEmailSender emailSender,
    IClock clock,
    ILogger<NotificationEmailDeliveryProcessor> logger)
{
    public async Task<int> DiscoverAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var notificationIds = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => !dbContext.NotificationEmailDeliveries
                .Any(delivery => delivery.NotificationId == notification.Id))
            .OrderBy(notification => notification.CreatedAt)
            .ThenBy(notification => notification.Id)
            .Select(notification => notification.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        var discoveredCount = 0;

        foreach (var notificationId in notificationIds)
        {
            var userId = await dbContext.Notifications
                .Where(notification => notification.Id == notificationId)
                .Select(notification => notification.UserId)
                .SingleAsync(cancellationToken);
            var emailEnabled = await dbContext.NotificationPreferences
                .Where(preference => preference.UserId == userId)
                .Select(preference => (bool?)preference.EmailEnabled)
                .SingleOrDefaultAsync(cancellationToken) ?? false;
            var delivery = NotificationEmailDelivery.Create(notificationId, emailEnabled, clock.UtcNow);
            dbContext.NotificationEmailDeliveries.Add(delivery);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                discoveredCount++;
            }
            catch (DbUpdateException exception) when (IsDuplicateDelivery(exception))
            {
                dbContext.NotificationEmailDeliveries.Entry(delivery).State = EntityState.Detached;
            }
        }

        return discoveredCount;
    }

    public async Task<int> DeliverAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var processedCount = 0;

        for (var index = 0; index < batchSize; index++)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = clock.UtcNow;
            var delivery = await dbContext.NotificationEmailDeliveries
                .FromSqlInterpolated($"""
                    SELECT * FROM "NotificationEmailDeliveries"
                    WHERE "Status" = 'Pending'
                      AND "NextAttemptAt" <= {now}
                    ORDER BY "NextAttemptAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            if (delivery is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                break;
            }

            var notification = await dbContext.Notifications
                .AsNoTracking()
                .SingleAsync(item => item.Id == delivery.NotificationId, cancellationToken);
            var user = await userDirectory.FindByIdAsync(notification.UserId, cancellationToken);

            try
            {
                if (user is null)
                {
                    throw new InvalidOperationException("Notification recipient no longer exists.");
                }

                var subject = SanitizeSubject(notification.Title);
                var htmlBody = $"<h2>{HtmlEncoder.Default.Encode(notification.Title)}</h2>"
                    + $"<p>{HtmlEncoder.Default.Encode(notification.Message)}</p>";
                await emailSender.SendEmailAsync(user.Email, subject, htmlBody, cancellationToken);
                delivery.MarkSent(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var errorCode = exception.GetType().Name;
                delivery.RecordFailure(errorCode, clock.UtcNow);
                logger.LogWarning(
                    "Notification email delivery {DeliveryId} failed with {ErrorCode} on attempt {AttemptCount}.",
                    delivery.Id,
                    errorCode,
                    delivery.AttemptCount);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            processedCount++;
        }

        return processedCount;
    }

    private static string SanitizeSubject(string subject) =>
        subject.Replace('\r', ' ').Replace('\n', ' ');

    private static bool IsDuplicateDelivery(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "PK_NotificationEmailDeliveries" or "UX_NotificationEmailDeliveries_NotificationId",
        };
}
