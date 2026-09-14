using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Default_page_is_bounded_and_number_resolves_items_outside_first_page()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "LOOKUP");
        var first = await CreateWorkItemAsync(owner.Client, project, "Oldest record");
        for (var index = 0; index < 25; index++)
            await CreateWorkItemAsync(owner.Client, project, $"Newer record {index}");
        var url = $"/api/v1/projects/{project}/work-items";
        var page = (await owner.Client.GetFromJsonAsync<JsonElement[]>(url))!;
        Assert.Equal(25, page.Length);
        Assert.DoesNotContain(page, item => item.GetProperty("number").GetInt64() == 1);
        var exact = (await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?number=1"))!;
        Assert.Equal(first.GetProperty("id").GetGuid(), Assert.Single(exact).GetProperty("id").GetGuid());
        Assert.Single((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?page=2"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.GetAsync($"{url}?number=0")).StatusCode);
    }

    [Fact]
    public async Task Search_filters_compose_and_generated_vector_tracks_updates()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "SEARCH");
        var item = await CreateWorkItemAsync(owner.Client, project, "Tenant isolation");
        var id = item.GetProperty("id").GetGuid();
        var state = item.GetProperty("workflowStateId").GetGuid();
        await CreateWorkItemAsync(owner.Client, project, "Unrelated");
        var labelResponse = await owner.Client.PostAsJsonAsync($"/api/v1/projects/{project}/labels", new { name = "Security" });
        labelResponse.EnsureSuccessStatusCode();
        var label = (await labelResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/labels/{label}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/self-assign", null)).StatusCode);
        var url = $"/api/v1/projects/{project}/work-items";
        var result = await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search=TENANT%20isolation&status={state}&priority=MEDIUM&type=TASK&assignee={owner.UserId}&reporter={owner.UserId}&label={label}");
        Assert.Equal(id, Assert.Single(result!).GetProperty("id").GetGuid());
        Assert.Single(result![0].GetProperty("labels").EnumerateArray());
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search=Ten"))!);
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search=tenant&priority=CRITICAL"))!);
        foreach (var name in new[] { "label", "assignee", "reporter", "status" })
            Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?{name}={Guid.NewGuid()}"))!);
        Assert.Equal(2, (await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search=core"))!.Length);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync($"/api/v1/work-items/{id}", new
        {
            type = "BUG",
            title = "Replacement",
            description = (string?)null,
            priority = "HIGH",
            dueDate = (DateOnly?)null,
        })).StatusCode);
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search=tenant"))!);
        Assert.Single((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search=replacement&type=BUG"))!);
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?search={Uri.EscapeDataString("'; DROP TABLE WorkItems; --")}"))!);
        Assert.Equal(2, (await owner.Client.GetFromJsonAsync<JsonElement[]>(url))!.Length);
    }

    [Fact]
    public async Task Search_pagination_sorting_dates_and_validation_are_bounded()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "PAGING");
        var ids = new List<Guid>();
        for (var index = 0; index < 4; index++)
            ids.Add((await CreateWorkItemAsync(owner.Client, project, $"Page {index}")).GetProperty("id").GetGuid());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "WorkItems" SET "CreatedAt" = '2026-09-01T00:00:00Z', "UpdatedAt" = '2026-09-02T23:59:59Z',
                    "Priority" = CASE "Number" WHEN 1 THEN 'LOW' WHEN 2 THEN 'CRITICAL' WHEN 3 THEN 'HIGH' ELSE 'MEDIUM' END,
                    "DueDate" = CASE "Number" WHEN 1 THEN NULL ELSE '2026-09-15'::date END
                WHERE "ProjectId" = {project}
                """);
        }
        var url = $"/api/v1/projects/{project}/work-items";
        async Task<Guid[]> Read(string query) => (await owner.Client.GetFromJsonAsync<JsonElement[]>($"{url}?{query}"))!
            .Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(ids.Take(2), await Read("pageSize=2&direction=asc"));
        Assert.Equal(ids.Skip(2), await Read("pageSize=2&page=2&direction=asc"));
        Assert.Empty(await Read("pageSize=2&page=3"));
        Assert.Equal(ids.AsEnumerable().Reverse(), await Read("sort=updatedAt&direction=desc"));
        Assert.Equal(new[] { ids[0], ids[3], ids[2], ids[1] }, await Read("sort=priority&direction=asc"));
        Assert.Equal(new[] { ids[1], ids[2], ids[3], ids[0] }, await Read("sort=priority&direction=desc"));
        Assert.Equal(new[] { ids[1], ids[2], ids[3], ids[0] }, await Read("sort=dueDate&direction=asc"));
        Assert.Equal(new[] { ids[3], ids[2], ids[1], ids[0] }, await Read("sort=dueDate&direction=desc"));
        Assert.Equal(4, (await Read("createdFrom=2026-09-01&createdTo=2026-09-01")).Length);
        Assert.Empty(await Read("createdTo=2026-08-31"));
        Assert.Empty(await Read("createdFrom=2026-09-02"));
        Assert.Equal(3, (await Read("dueFrom=2026-09-15&dueTo=2026-09-15")).Length);
        Assert.Empty(await Read("dueTo=2026-09-14"));
        foreach (var invalid in new[] { "page=0", "page=100001", "pageSize=0", "pageSize=101", "page=2147483647", "sort=Title", "direction=sideways", "type=BOGUS", "priority=1", "status=invalid", "assignee=invalid", "createdFrom=invalid", "createdFrom=2026-09-02&createdTo=2026-09-01", "dueFrom=2026-09-02&dueTo=2026-09-01", "search=" + new string('a', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.GetAsync($"{url}?{invalid}")).StatusCode);
    }

    [Fact]
    public async Task Search_never_crosses_project_tenant_or_revoked_membership_boundaries()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var otherOrg = await CreateOrganizationAsync(outsider.Client);
        var project = await CreateProjectAsync(owner.Client, org, "SCOPED");
        var otherProject = await CreateProjectAsync(outsider.Client, otherOrg, "SCOPED");
        var hiddenProject = await CreateProjectAsync(owner.Client, org, "HIDDEN");
        await CreateWorkItemAsync(owner.Client, project, "Shared needle");
        await CreateWorkItemAsync(outsider.Client, otherProject, "Shared needle");
        await CreateWorkItemAsync(owner.Client, hiddenProject, "Shared needle");
        await AddOrganizationMemberAsync(org, viewer.UserId, owner.UserId);
        await AddProjectMemberAsync(owner.Client, project, viewer.UserId, "VIEWER");
        var url = $"/api/v1/projects/{project}/work-items?search=needle&pageSize=100";
        var results = await viewer.Client.GetFromJsonAsync<JsonElement[]>(url);
        Assert.Equal(org, Assert.Single(results!).GetProperty("organizationId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.Client.GetAsync($"/api/v1/projects/{hiddenProject}/work-items?search=needle")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/projects/{project}/members/{viewer.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.Client.GetAsync(url)).StatusCode);
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
    }
}
