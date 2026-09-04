using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.Modules.Notifications.Application.Abstractions;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }

    DbSet<NotificationPreference> NotificationPreferences { get; }

    DatabaseFacade Database { get; }

    Task<bool> LockUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
