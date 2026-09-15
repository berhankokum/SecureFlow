namespace SecureFlow.Modules.Organizations.Application.Dashboard;

public sealed record DashboardState(Guid StateId, string Name, int Count);
public sealed record DashboardTask(Guid Id, string Key, string Title, DateOnly? DueDate, string State);
public sealed record DashboardActivity(Guid Id, Guid ActorId, string Type, DateTimeOffset CreatedAt,
    string WorkItemKey, string WorkItemTitle);
public sealed record DashboardResponse(DateOnly Today, int ActiveProjects, int MyWorkItems, int Overdue,
    int DueSoon, int Critical, IReadOnlyList<DashboardState> TasksByState,
    IReadOnlyList<DashboardTask> MyTasks, int MyTasksPage, int MyTasksPageSize,
    IReadOnlyList<DashboardActivity> RecentActivity);

public interface IDashboardReader
{
    Task<DashboardResponse?> ReadAsync(Guid organizationId, Guid userId, int myTasksPage, CancellationToken cancellationToken);
}
