using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureFlow.Api.Caching;
using SecureFlow.Contracts.Caching;
using SecureFlow.SharedKernel;
using StackExchange.Redis;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Organizations.Application.Dashboard;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Dashboard_pages_tasks_bounds_activity_and_invalidates_workitem_mutations()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "DPAGE");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var state = await db.WorkflowStates.FirstAsync(s => s.Category == WorkflowCategory.Todo
            && db.Workflows.Any(w => w.Id == s.WorkflowId && w.OrganizationId == org));
        var now = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 21; i++)
        {
            var item = WorkItem.Create(org, project, 100 + i, WorkItemType.Bug, "Paged " + i, null,
                WorkItemPriority.Medium, state.Id, owner.UserId, null, DateOnly.FromDateTime(now.UtcDateTime), now);
            item.Assign(owner.UserId, now);
            db.WorkItems.Add(item);
            db.ActivityEvents.Add(SecureFlow.Modules.Collaboration.Domain.ActivityEvent.Create(org, project,
                item.Id, owner.UserId, "WORK_ITEM_CREATED", new Dictionary<string, string?>(), now.AddSeconds(i)));
        }
        await db.SaveChangesAsync();
        var url = $"/api/v1/organizations/{org}/dashboard";
        var first = (await owner.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        var second = (await owner.Client.GetFromJsonAsync<DashboardResponse>(url + "?myTasksPage=2"))!;
        Assert.Equal(21, first.MyWorkItems);
        Assert.Equal(20, first.MyTasks.Count);
        Assert.Single(second.MyTasks);
        Assert.Equal(21, first.MyTasks.Concat(second.MyTasks).Select(x => x.Id).Distinct().Count());
        Assert.Equal(20, first.RecentActivity.Count);
        Assert.Equal("DPAGE-121", first.RecentActivity[0].WorkItemKey);
        var id = first.MyTasks[0].Id;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync($"/api/v1/work-items/{id}", ValidWorkItem("Changed dashboard title"))).StatusCode);
        var updated = (await owner.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        Assert.Equal("Changed dashboard title", Assert.Single(updated.MyTasks, t => t.Id == id).Title);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/work-items/{id}/assignment")).StatusCode);
        var unassigned = (await owner.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        Assert.Equal(20, unassigned.MyWorkItems);
        Assert.DoesNotContain(unassigned.MyTasks, t => t.Id == id);
        Assert.Empty((await owner.Client.GetFromJsonAsync<DashboardResponse>(url + "?myTasksPage=2"))!.MyTasks);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/archive", null)).StatusCode);
        var archived = (await owner.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        Assert.Equal(20, archived.TasksByState.Sum(s => s.Count));
        Assert.DoesNotContain(archived.RecentActivity, a => a.WorkItemKey == first.MyTasks[0].Key);
    }

    private sealed class DashboardClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private sealed class DashboardRecordingCache(IVersionedCache inner) : IVersionedCache
    {
        public List<string> Keys { get; } = [];
        public Func<Task>? AfterRead { get; set; }
        public async Task<T?> GetOrCreateAsync<T>(CacheAddress address, string version, Func<CancellationToken, Task<T?>> load,
            CancellationToken cancellationToken = default) where T : class
        {
            Keys.Add(address.Key(version));
            var value = await inner.GetOrCreateAsync(address, version, load, cancellationToken);
            if (AfterRead is { } action) { AfterRead = null; await action(); }
            return value;
        }
    }

    [Fact]
    public async Task Dashboard_cache_ttl_malformed_data_utc_rollover_and_rollback_are_safe()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "DCACHE");
        var id = (await CreateWorkItemAsync(owner.Client, project, "Committed title")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/self-assign", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var raw = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        var recording = new DashboardRecordingCache(scope.ServiceProvider.GetRequiredService<IVersionedCache>());
        var clock = new DashboardClock(new DateTimeOffset(2030, 1, 1, 23, 59, 59, TimeSpan.Zero));
        var reader = new OrganizationDashboardReader(db, recording, clock);
        var counter = factory.Services.GetRequiredService<CacheQueryCounter>();
        Assert.Equal(1, (await reader.ReadAsync(org, owner.UserId, 1, default))!.MyWorkItems);
        var firstKey = recording.Keys[^1];
        using var redis = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync(firstKey);
        Assert.InRange(ttl!.Value.TotalSeconds, 1, 30);
        var loads = counter.DashboardLoads;
        await reader.ReadAsync(org, owner.UserId, 1, default);
        Assert.Equal(loads, counter.DashboardLoads);
        await raw.SetAsync(firstKey, System.Text.Encoding.UTF8.GetBytes("broken-json"));
        Assert.Equal(1, (await reader.ReadAsync(org, owner.UserId, 1, default))!.MyWorkItems);
        Assert.True(counter.DashboardLoads > loads);
        await raw.SetAsync(firstKey, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { key = firstKey, value = new { today = "2030-01-01" } }));
        Assert.Equal(1, (await reader.ReadAsync(org, owner.UserId, 1, default))!.MyWorkItems);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "WorkItems" SET "Title" = {"Uncommitted title"} WHERE "Id" = {id}""");
            var calls = recording.Keys.Count;
            Assert.Equal("Uncommitted title", (await reader.ReadAsync(org, owner.UserId, 1, default))!.MyTasks[0].Title);
            Assert.Equal(calls, recording.Keys.Count);
            await transaction.RollbackAsync();
        }
        Assert.Equal("Committed title", (await reader.ReadAsync(org, owner.UserId, 1, default))!.MyTasks[0].Title);
        Assert.Equal(firstKey, recording.Keys[^1]);
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        Assert.Equal(new DateOnly(2030, 1, 2), (await reader.ReadAsync(org, owner.UserId, 1, default))!.Today);
        Assert.NotEqual(firstKey, recording.Keys[^1]);
        var shortCache = new RedisVersionedCache(raw, Options.Create(new CacheOptions { DashboardTtlSeconds = 1 }), NullLogger<RedisVersionedCache>.Instance);
        var expiring = new OrganizationDashboardReader(db, shortCache, clock);
        await expiring.ReadAsync(org, owner.UserId, 2, default);
        loads = counter.DashboardLoads;
        await expiring.ReadAsync(org, owner.UserId, 2, default);
        Assert.Equal(loads, counter.DashboardLoads);
        await Task.Delay(1200);
        await expiring.ReadAsync(org, owner.UserId, 2, default);
        Assert.True(counter.DashboardLoads > loads);
    }

    [Fact]
    public async Task Dashboard_discards_cached_results_when_access_changes_during_read()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(org, member.UserId, owner.UserId);
        var project = await CreateProjectAsync(owner.Client, org, "DRACE");
        await AddProjectMemberAsync(owner.Client, project, member.UserId, "VIEWER");
        await CreateWorkItemAsync(owner.Client, project, "Soon forbidden");
        using var scope = factory.Services.CreateScope();
        var cache = new DashboardRecordingCache(scope.ServiceProvider.GetRequiredService<IVersionedCache>());
        var reader = new OrganizationDashboardReader(scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>(), cache, new SystemClock());
        Assert.Equal(1, (await reader.ReadAsync(org, member.UserId, 1, default))!.ActiveProjects);
        cache.AfterRead = async () => Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/projects/{project}/members/{member.UserId}")).StatusCode);
        var result = (await reader.ReadAsync(org, member.UserId, 1, default))!;
        Assert.Equal(0, result.ActiveProjects);
        Assert.Empty(result.RecentActivity);
        Assert.NotEqual(cache.Keys[0], cache.Keys[^1]);
    }

    [Fact]
    public async Task Dashboard_counts_dates_done_archives_and_my_tasks_correctly()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "DASH");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var states = await db.WorkflowStates.Where(s => db.Workflows.Any(w => w.Id == s.WorkflowId && w.OrganizationId == org)).ToArrayAsync();
        var open = states.First(s => s.Category == WorkflowCategory.Todo);
        var done = states.First(s => s.Category == WorkflowCategory.Done);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        WorkItem Item(long number, DateOnly? due, bool finished = false, bool archived = false)
        {
            var item = WorkItem.Create(org, project, number, WorkItemType.Task, "Dashboard task " + number,
                null, WorkItemPriority.Critical, finished ? done.Id : open.Id, owner.UserId,
                due, today.AddDays(-10), DateTimeOffset.UtcNow);
            item.Assign(owner.UserId, DateTimeOffset.UtcNow);
            if (archived) item.Archive(DateTimeOffset.UtcNow);
            return item;
        }
        db.WorkItems.AddRange(Item(101, today.AddDays(-1)), Item(102, today), Item(103, today.AddDays(6)),
            Item(104, today.AddDays(7)), Item(105, null), Item(106, today.AddDays(-1), true), Item(107, today, false, true));
        await db.SaveChangesAsync();
        var url = $"/api/v1/organizations/{org}/dashboard";
        var response = await owner.Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var data = (await response.Content.ReadFromJsonAsync<DashboardResponse>())!;
        Assert.Equal(1, data.ActiveProjects);
        Assert.Equal(5, data.MyWorkItems);
        Assert.Equal(1, data.Overdue);
        Assert.Equal(2, data.DueSoon);
        Assert.Equal(5, data.Critical);
        Assert.Equal(6, data.TasksByState.Sum(s => s.Count));
        Assert.Equal(1, Assert.Single(data.TasksByState, s => s.StateId == done.Id).Count);
        Assert.Equal("DASH-101", data.MyTasks[0].Key);
        Assert.Equal("DASH-105", data.MyTasks[^1].Key);
        Assert.Empty((await owner.Client.GetFromJsonAsync<DashboardResponse>(url + "?myTasksPage=2"))!.MyTasks);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.GetAsync(url + "?myTasksPage=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/projects/{project}/archive", null)).StatusCode);
        var archivedData = (await owner.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        Assert.Equal(0, archivedData.ActiveProjects);
        Assert.Empty(archivedData.TasksByState);
        Assert.Empty(archivedData.MyTasks);
    }

    [Fact]
    public async Task Dashboard_cache_hits_and_live_access_changes_never_expose_hidden_projects()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(org, member.UserId, owner.UserId);
        var visible = await CreateProjectAsync(owner.Client, org, "VISIBLE");
        var hidden = await CreateProjectAsync(owner.Client, org, "HIDDEN");
        await AddProjectMemberAsync(owner.Client, visible, member.UserId, "VIEWER");
        await CreateWorkItemAsync(owner.Client, visible, "Visible title");
        await CreateWorkItemAsync(owner.Client, hidden, "Secret hidden title");
        var url = $"/api/v1/organizations/{org}/dashboard";
        var counter = factory.Services.GetRequiredService<CacheQueryCounter>();
        var first = (await member.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        var loads = counter.DashboardLoads;
        Assert.Equal(1, first.ActiveProjects);
        Assert.Single(first.RecentActivity);
        Assert.Equal("Visible title", first.RecentActivity[0].WorkItemTitle);
        await member.Client.GetAsync(url);
        Assert.Equal(loads, counter.DashboardLoads);
        Assert.Equal(2, (await owner.Client.GetFromJsonAsync<DashboardResponse>(url))!.ActiveProjects);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync(url)).StatusCode);
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/projects/{visible}/members/{member.UserId}")).StatusCode);
        var removed = (await member.Client.GetFromJsonAsync<DashboardResponse>(url))!;
        Assert.Equal(0, removed.ActiveProjects);
        Assert.Empty(removed.RecentActivity);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync($"/api/v1/organizations/{org}/members/{member.UserId}", new { role = "ADMIN" })).StatusCode);
        Assert.Equal(2, (await member.Client.GetFromJsonAsync<DashboardResponse>(url))!.ActiveProjects);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync($"/api/v1/organizations/{org}/members/{member.UserId}", new { role = "MEMBER" })).StatusCode);
        Assert.Equal(0, (await member.Client.GetFromJsonAsync<DashboardResponse>(url))!.ActiveProjects);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/organizations/{org}/members/{member.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync(url)).StatusCode);
    }
}
