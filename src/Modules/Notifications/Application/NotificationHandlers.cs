using System.Data;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.Modules.Notifications.Application.Models;
using SecureFlow.Modules.Notifications.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Notifications.Application;

public sealed class NotificationHandlers(INotificationsDbContext dbContext, IClock clock)
{
    private const int ListLimit = 100;

    public async Task<NotificationListSummary> ListAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var unreadCount = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == currentUserId && !notification.IsRead, cancellationToken);
        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == currentUserId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(ListLimit)
            .ToArrayAsync(cancellationToken);
        return new NotificationListSummary(notifications.Select(ToSummary).ToArray(), unreadCount);
    }

    public async Task<Result> MarkReadAsync(
        Guid notificationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == currentUserId,
            cancellationToken);
        if (notification is null)
        {
            return Result.Failure(NotificationErrors.NotFound);
        }

        if (notification.MarkRead(clock.UtcNow))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task MarkAllReadAsync(Guid currentUserId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await dbContext.Notifications
            .Where(notification => notification.UserId == currentUserId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true)
                .SetProperty(notification => notification.ReadAt, now), cancellationToken);
    }

    public async Task<NotificationPreferenceSummary> GetPreferenceAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var preference = await dbContext.NotificationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == currentUserId, cancellationToken);
        return preference is null
            ? new NotificationPreferenceSummary(true, false, null)
            : ToSummary(preference);
    }

    public async Task<NotificationPreferenceSummary> UpdatePreferenceAsync(
        Guid currentUserId,
        bool emailEnabled,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await dbContext.LockUserAsync(currentUserId, cancellationToken);
        var preference = await dbContext.NotificationPreferences.SingleOrDefaultAsync(
            item => item.UserId == currentUserId,
            cancellationToken);
        var now = clock.UtcNow;
        if (preference is null)
        {
            preference = NotificationPreference.Create(currentUserId, emailEnabled, now);
            dbContext.NotificationPreferences.Add(preference);
        }
        else
        {
            preference.Update(emailEnabled, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToSummary(preference);
    }

    private static NotificationSummary ToSummary(Notification notification) => new(
        notification.Id,
        notification.OrganizationId,
        notification.Type,
        notification.Title,
        notification.Message,
        notification.ResourceType,
        notification.ResourceId,
        notification.IsRead,
        notification.CreatedAt,
        notification.ReadAt);

    private static NotificationPreferenceSummary ToSummary(NotificationPreference preference) =>
        new(true, preference.EmailEnabled, preference.UpdatedAt);
}
