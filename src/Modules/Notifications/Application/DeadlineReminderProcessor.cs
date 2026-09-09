using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.Modules.Notifications.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Application;

public sealed class DeadlineReminderProcessor(
    INotificationsDbContext dbContext,
    IRealtimePublisher realtimePublisher,
    IClock clock,
    ILogger<DeadlineReminderProcessor> logger)
{
    public async Task<int> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var dueDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);
        var createdCount = 0;
        var offset = 0;

        while (true)
        {
            var candidates = await dbContext.GetDueReminderCandidatesAsync(
                dueDate,
                offset,
                batchSize,
                cancellationToken);
            foreach (var candidate in candidates)
            {
                var sourceEventId = ReminderEventId.Create(candidate.WorkItemId, candidate.DueDate);
                var recipients = new[] { candidate.ReporterId, candidate.AssigneeId }
                    .Where(userId => userId.HasValue)
                    .Select(userId => userId!.Value)
                    .Distinct();

                foreach (var recipientUserId in recipients)
                {
                    if (!await dbContext.CanUserAccessProjectAsync(
                            candidate.ProjectId,
                            candidate.OrganizationId,
                            recipientUserId,
                            cancellationToken))
                    {
                        continue;
                    }

                    var exists = await dbContext.Notifications.AnyAsync(notification =>
                        notification.UserId == recipientUserId
                        && notification.Type == NotificationTypes.DueDateApproaching
                        && notification.SourceEventId == sourceEventId,
                        cancellationToken);
                    if (exists)
                    {
                        continue;
                    }

                    var notification = Notification.Create(
                        candidate.OrganizationId,
                        recipientUserId,
                        NotificationTypes.DueDateApproaching,
                        $"{candidate.ProjectKey}-{candidate.WorkItemNumber} son tarihi yaklaşıyor",
                        $"{candidate.WorkItemTitle} yarın tamamlanmalı.",
                        NotificationResourceTypes.WorkItem,
                        candidate.WorkItemId,
                        sourceEventId,
                        now);
                    dbContext.Notifications.Add(notification);

                    try
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException exception) when (IsDuplicateNotification(exception))
                    {
                        dbContext.Notifications.Entry(notification).State = EntityState.Detached;
                        continue;
                    }

                    createdCount++;
                    try
                    {
                        await realtimePublisher.PublishNotificationCreatedAsync(
                            recipientUserId,
                            new NotificationCreatedRealtimeEvent(notification.Id, notification.Id, now),
                            cancellationToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogWarning(
                            "Due reminder realtime publication failed for notification {NotificationId} with {ErrorCode}.",
                            notification.Id,
                            exception.GetType().Name);
                    }
                }
            }

            offset += candidates.Count;
            if (candidates.Count < batchSize)
            {
                break;
            }
        }

        return createdCount;
    }

    private static bool IsDuplicateNotification(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_Notifications_UserId_Type_SourceEventId",
        };
}
