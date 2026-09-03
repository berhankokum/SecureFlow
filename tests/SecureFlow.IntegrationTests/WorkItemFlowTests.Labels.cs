using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Labels_lifecycle_normalizes_derives_scope_and_cascades_only_relationships()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "LABEL");
        var item = await CreateWorkItemAsync(owner.Client, projectId, "Label lifecycle");
        var itemId = item.GetProperty("id").GetGuid();
        var response = await owner.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new
        {
            name = "  Ｓｅｃｕｒｉｔｙ  ", organizationId = Guid.NewGuid(), createdBy = Guid.NewGuid(),
            normalizedName = "forged", projectId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var label = await response.Content.ReadFromJsonAsync<JsonElement>();
        var labelId = label.GetProperty("id").GetGuid();
        Assert.Equal(orgId, label.GetProperty("organizationId").GetGuid());
        Assert.Equal(projectId, label.GetProperty("projectId").GetGuid());
        Assert.Equal(owner.UserId, label.GetProperty("createdBy").GetGuid());
        Assert.Equal("Security", label.GetProperty("name").GetString());
        Assert.False(label.TryGetProperty("normalizedName", out _));
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new { name = "security" })).StatusCode);

        var assignmentUrl = $"/api/v1/work-items/{itemId}/labels/{labelId}";
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync(assignmentUrl, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync(assignmentUrl, null)).StatusCode);
        var detail = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        var assigned = Assert.Single(detail.GetProperty("labels").EnumerateArray());
        Assert.Equal(owner.UserId, assigned.GetProperty("assignedBy").GetGuid());
        var assignedAt = assigned.GetProperty("assignedAt").GetDateTimeOffset();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync(assignmentUrl, null)).StatusCode);
        detail = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Equal(assignedAt, detail.GetProperty("labels")[0].GetProperty("assignedAt").GetDateTimeOffset());

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/labels/{labelId}", new { name = "Urgent", organizationId = Guid.NewGuid() })).StatusCode);
        var list = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}/work-items");
        Assert.Equal("Urgent", list[0].GetProperty("labels")[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(assignmentUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(assignmentUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync(assignmentUrl, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
        detail = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Empty(detail.GetProperty("labels").EnumerateArray());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.False(await db.WorkItemLabels.AnyAsync(x => x.LabelId == labelId));
        Assert.True(await db.WorkItems.AnyAsync(x => x.Id == itemId));
        Assert.True(await db.Projects.AnyAsync(x => x.Id == projectId));
    }

    [Fact]
    public async Task Labels_enforce_manager_contributor_viewer_and_revoked_membership_permissions()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var manager = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var unassigned = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { manager, contributor, viewer, unassigned })
            await AddOrganizationMemberAsync(orgId, user.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "ROLEL");
        await AddProjectMemberAsync(owner.Client, projectId, manager.UserId, "MANAGER");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");
        var labelId = await CreateLabelAsync(manager.Client, projectId, "Managed");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Access")).GetProperty("id").GetGuid();
        var url = $"/api/v1/work-items/{itemId}/labels/{labelId}";
        foreach (var user in new[] { contributor, viewer })
        {
            Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync($"/api/v1/projects/{projectId}/labels")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/labels", new { name = "Unauthorized" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PatchAsJsonAsync(
                $"/api/v1/labels/{labelId}", new { name = "Unauthorized" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await contributor.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await contributor.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unassigned.Client.GetAsync($"/api/v1/projects/{projectId}/labels")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unassigned.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.Client.PatchAsJsonAsync(
            $"/api/v1/labels/{labelId}", new { name = "Renamed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/projects/{projectId}/members/{contributor.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await contributor.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
    }

    [Fact]
    public async Task Labels_hide_cross_project_and_cross_tenant_ids_on_all_endpoints()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        var otherOrg = await CreateOrganizationAsync(outsider.Client);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "FIRST");
        var secondId = await CreateProjectAsync(owner.Client, orgId, "SECOND");
        var foreignId = await CreateProjectAsync(outsider.Client, otherOrg, "FOREIGN");
        var labelId = await CreateLabelAsync(owner.Client, projectId, "Security");
        var secondLabel = await CreateLabelAsync(owner.Client, secondId, "Security");
        var foreignLabel = await CreateLabelAsync(outsider.Client, foreignId, "Security");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Isolated")).GetProperty("id").GetGuid();
        foreach (var invalidLabel in new[] { secondLabel, foreignLabel, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.PostAsync(
                $"/api/v1/work-items/{itemId}/labels/{invalidLabel}", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.DeleteAsync(
                $"/api/v1/work-items/{itemId}/labels/{invalidLabel}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/labels")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/labels", new { name = "Attack" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PatchAsJsonAsync(
            $"/api/v1/labels/{labelId}", new { name = "Attack" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsync(
            $"/api/v1/work-items/{itemId}/labels/{foreignLabel}", null)).StatusCode);
    }

    [Fact]
    public async Task Archived_resources_reject_label_mutations_and_preserve_reads()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "ARCHL");
        var labelId = await CreateLabelAsync(owner.Client, projectId, "Keep");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Archived")).GetProperty("id").GetGuid();
        var url = $"/api/v1/work-items/{itemId}/labels/{labelId}";
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{itemId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.DeleteAsync(url)).StatusCode);
        var activeItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Active")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new { name = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PatchAsJsonAsync($"/api/v1/labels/{labelId}", new { name = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PostAsync($"/api/v1/work-items/{activeItemId}/labels/{labelId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.DeleteAsync($"/api/v1/work-items/{activeItemId}/labels/{labelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.GetAsync($"/api/v1/projects/{projectId}/labels")).StatusCode);
        var detail = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Single(detail.GetProperty("labels").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_label_create_and_assignment_are_unique_and_rename_conflicts_are_atomic()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "RACEL");
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => owner.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/labels", new { name = "same" })));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var created = await responses.Single(response => response.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
        var labelId = created.GetProperty("id").GetGuid();
        var secondLabel = await CreateLabelAsync(owner.Client, projectId, "Other");
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/labels/{secondLabel}", new { name = " SAME " })).StatusCode);
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Race")).GetProperty("id").GetGuid();
        var assignments = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => owner.Client.PostAsync(
            $"/api/v1/work-items/{itemId}/labels/{labelId}", null)));
        Assert.All(assignments, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Equal(1, await db.WorkItemLabels.CountAsync(x => x.WorkItemId == itemId));
        Assert.Equal("Other", (await db.Labels.FindAsync(secondLabel))!.Name);
    }

    [Fact]
    public async Task Labels_validate_names_csrf_and_database_tenant_fk()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        var secondOrg = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "SAFEL");
        foreach (var name in new[] { "", "  ", new string('a', 51), new string('\uFB03', 17) })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/labels", new { name })).StatusCode);
        var labelId = await CreateLabelAsync(owner.Client, projectId, "CSRF");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "CSRF")).GetProperty("id").GetGuid();
        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new { name = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PatchAsJsonAsync($"/api/v1/labels/{labelId}", new { name = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsync($"/api/v1/work-items/{itemId}/labels/{labelId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.DeleteAsync($"/api/v1/work-items/{itemId}/labels/{labelId}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        db.Labels.Add(Label.Create(secondOrg, projectId, "Invalid tenant", owner.UserId, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, Guid projectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/labels", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
