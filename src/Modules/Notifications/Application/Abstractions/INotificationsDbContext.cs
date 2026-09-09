using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.Modules.Notifications.Application.Abstractions;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }

    DbSet<NotificationPreference> NotificationPreferences { get; }

    DbSet<NotificationEmailDelivery> NotificationEmailDeliveries { get; }

    DatabaseFacade Database { get; }

    Task<bool> LockUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DueReminderCandidate>> GetDueReminderCandidatesAsync(
        DateOnly dueDate,
        int offset,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> CanUserAccessProjectAsync(
        Guid projectId,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record DueReminderCandidate(
    Guid WorkItemId,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    long WorkItemNumber,
    string WorkItemTitle,
    Guid ReporterId,
    Guid? AssigneeId,
    DateOnly DueDate);
