using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Caching;
using SecureFlow.Modules.Organizations.Application.Dashboard;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Api.Persistence;

public sealed class OrganizationDashboardReader(SecureFlowDbContext db, IVersionedCache cache, IClock clock) : IDashboardReader
{
    public async Task<DashboardResponse?> ReadAsync(Guid organizationId, Guid userId, int myTasksPage, CancellationToken cancellationToken)
    {
        if (myTasksPage is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(myTasksPage));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
            var before = await FingerprintAsync(organizationId, userId, cancellationToken);
            if (before is null) return null;
            async Task<DashboardResponse?> Load(CancellationToken ct)
            {
                var result = await ProjectAsync(organizationId, userId, today, myTasksPage, ct);
                return before == await FingerprintAsync(organizationId, userId, ct) ? result : null;
            }
            var data = db.Database.CurrentTransaction is not null
                ? await Load(cancellationToken)
                : await cache.GetOrCreateAsync(new CacheAddress(CacheCategory.Dashboard, organizationId, userId),
                    $"dashboard-1:{today:yyyy-MM-dd}:{myTasksPage}:{before}", Load, cancellationToken);
            if (data is not null && (data.Today != today || data.MyTasksPage != myTasksPage || data.MyTasksPageSize != 20
                || data.TasksByState is null || data.MyTasks is null || data.RecentActivity is null
                || data.MyTasks.Count > 20 || data.RecentActivity.Count > 20
                || data.ActiveProjects < 0 || data.MyWorkItems < 0 || data.Overdue < 0 || data.DueSoon < 0 || data.Critical < 0))
                data = await Load(cancellationToken);
            if (data is not null && today == DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)
                && before == await FingerprintAsync(organizationId, userId, cancellationToken)) return data;
        }
        // Fail closed rather than return a projection from an inconsistent access generation.
        throw new InvalidOperationException("Dashboard changed during reading; retry the request.");
    }

    private Task<string?> FingerprintAsync(Guid org, Guid user, CancellationToken ct) => db.Database.SqlQuery<string>($"""
        SELECT encode(sha256(convert_to(string_agg(v.token, ',' ORDER BY v.token), 'UTF8')), 'hex') AS "Value"
        FROM (
          SELECT 'o:' || o."Id"::text || ':' || o.xmin::text AS token FROM "Organizations" o WHERE o."Id" = {org}
          UNION ALL SELECT 'm:' || m."Id"::text || ':' || m.xmin::text FROM "OrganizationMembers" m WHERE m."OrganizationId" = {org}
          UNION ALL SELECT 'p:' || p."Id"::text || ':' || p.xmin::text FROM "Projects" p WHERE p."OrganizationId" = {org}
          UNION ALL SELECT 'pm:' || m."ProjectId"::text || ':' || m."UserId"::text || ':' || m.xmin::text FROM "ProjectMembers" m JOIN "Projects" p ON p."Id" = m."ProjectId" WHERE p."OrganizationId" = {org}
          UNION ALL SELECT 'w:' || w."Id"::text || ':' || w.xmin::text FROM "WorkItems" w WHERE w."OrganizationId" = {org}
          UNION ALL SELECT 's:' || s."Id"::text || ':' || s.xmin::text FROM "WorkflowStates" s JOIN "Workflows" w ON w."Id" = s."WorkflowId" WHERE w."OrganizationId" = {org}
          UNION ALL SELECT 'a:' || a."Id"::text || ':' || a.xmin::text FROM "ActivityEvents" a WHERE a."OrganizationId" = {org}
        ) v
        WHERE EXISTS (SELECT 1 FROM "OrganizationMembers" m JOIN "Organizations" o ON o."Id" = m."OrganizationId"
          WHERE m."OrganizationId" = {org} AND m."UserId" = {user} AND m."RemovedAt" IS NULL AND o."ArchivedAt" IS NULL)
        HAVING count(*) > 0
        """).SingleOrDefaultAsync(ct);

    private async Task<DashboardResponse> ProjectAsync(Guid org, Guid user, DateOnly today, int page, CancellationToken ct)
    {
        var privileged = db.OrganizationMembers.Where(m => m.OrganizationId == org && m.UserId == user && m.RemovedAt == null
            && (m.Role == OrganizationRole.Owner || m.Role == OrganizationRole.Admin));
        var projects = db.Projects.AsNoTracking().Where(p => p.OrganizationId == org && p.ArchivedAt == null
            && (privileged.Any() || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == user)));
        var items = from w in db.WorkItems.AsNoTracking().TagWith("Dashboard projection")
                    join p in projects on w.ProjectId equals p.Id
                    join s in db.WorkflowStates on w.WorkflowStateId equals s.Id
                    where w.OrganizationId == org && w.ArchivedAt == null
                    select new { Item = w, ProjectKey = p.Key, State = s };
        var end = today.AddDays(6);
        var totals = await items.GroupBy(_ => 1).Select(g => new
        {
            Mine = g.Count(x => x.Item.AssigneeId == user && x.State.Category != WorkflowCategory.Done),
            Overdue = g.Count(x => x.Item.DueDate < today && x.State.Category != WorkflowCategory.Done),
            Soon = g.Count(x => x.Item.DueDate >= today && x.Item.DueDate <= end && x.State.Category != WorkflowCategory.Done),
            Critical = g.Count(x => x.Item.Priority == WorkItemPriority.Critical && x.State.Category != WorkflowCategory.Done),
        }).SingleOrDefaultAsync(ct);
        var states = await items.GroupBy(x => new { x.State.Id, x.State.Name, x.State.Position })
            .OrderBy(g => g.Key.Position).ThenBy(g => g.Key.Id)
            .Select(g => new DashboardState(g.Key.Id, g.Key.Name, g.Count())).ToArrayAsync(ct);
        var mine = await items.Where(x => x.Item.AssigneeId == user && x.State.Category != WorkflowCategory.Done)
            .OrderBy(x => x.Item.DueDate == null).ThenBy(x => x.Item.DueDate).ThenBy(x => x.Item.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(x => new DashboardTask(x.Item.Id, x.ProjectKey + "-" + x.Item.Number, x.Item.Title, x.Item.DueDate, x.State.Name)).ToArrayAsync(ct);
        var activity = await (from a in db.ActivityEvents.AsNoTracking()
                              join x in items on a.WorkItemId equals x.Item.Id
                              where a.OrganizationId == org && a.ProjectId == x.Item.ProjectId
                              orderby a.CreatedAt descending, a.Id descending
                              select new DashboardActivity(a.Id, a.ActorId, a.Type, a.CreatedAt,
                                  x.ProjectKey + "-" + x.Item.Number, x.Item.Title)).Take(20).ToArrayAsync(ct);
        return new DashboardResponse(today, await projects.CountAsync(ct), totals?.Mine ?? 0, totals?.Overdue ?? 0,
            totals?.Soon ?? 0, totals?.Critical ?? 0, states, mine, page, 20, activity);
    }
}
