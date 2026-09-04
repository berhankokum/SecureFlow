using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Contracts.Activity;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Assignment_lifecycle_returns_assignee_and_keeps_duplicate_requests_idempotent()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var manager = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var admin = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { manager, contributor, viewer })
            await AddOrganizationMemberAsync(orgId, user.UserId, owner.UserId);
        await AddOrganizationMemberAsync(orgId, admin.UserId, owner.UserId, OrganizationRole.Admin);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "ASSIGN");
        await AddProjectMemberAsync(owner.Client, projectId, manager.UserId, "MANAGER");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Assignment lifecycle"))
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await manager.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = contributor.UserId })).StatusCode);
        var assigned = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Equal(contributor.UserId, assigned.GetProperty("assigneeId").GetGuid());
        Assert.Equal(contributor.Email, assigned.GetProperty("assigneeEmail").GetString());
        var assignedUpdatedAt = assigned.GetProperty("updatedAt").GetDateTimeOffset();
        Assert.Equal(HttpStatusCode.NoContent, (await manager.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = contributor.UserId })).StatusCode);
        assigned = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Equal(assignedUpdatedAt, assigned.GetProperty("updatedAt").GetDateTimeOffset());

        Assert.Equal(HttpStatusCode.NoContent, (await manager.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = viewer.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = admin.UserId })).StatusCode);
        assigned = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}/work-items");
        Assert.Equal(admin.UserId, assigned[0].GetProperty("assigneeId").GetGuid());
        Assert.Equal(admin.Email, assigned[0].GetProperty("assigneeEmail").GetString());

        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);
        assigned = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Equal(JsonValueKind.Null, assigned.GetProperty("assigneeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, assigned.GetProperty("assigneeEmail").ValueKind);
    }

    [Fact]
    public async Task Contributor_can_self_assign_and_unassign_but_cannot_manage_another_assignee()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var other = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { contributor, other, viewer })
            await AddOrganizationMemberAsync(orgId, user.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "SELF");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, other.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Self assignment"))
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await contributor.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = other.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await contributor.Client.PostAsync($"/api/v1/work-items/{itemId}/self-assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await contributor.Client.PostAsync($"/api/v1/work-items/{itemId}/self-assign", null)).StatusCode);
        var detail = await contributor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Equal(contributor.UserId, detail.GetProperty("assigneeId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent,
            (await contributor.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = other.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await contributor.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await contributor.Client.PostAsync($"/api/v1/work-items/{itemId}/self-assign", null)).StatusCode);
        detail = await contributor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Equal(contributor.UserId, detail.GetProperty("assigneeId").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.Client.PostAsync($"/api/v1/work-items/{itemId}/self-assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);
    }

    [Fact]
    public async Task Assignment_rejects_cross_tenant_and_users_without_project_access()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationMember = await CreateAuthenticatedUserAsync();
        var otherProjectMember = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        var outsiderOrg = await CreateOrganizationAsync(outsider.Client);
        foreach (var user in new[] { organizationMember, otherProjectMember })
            await AddOrganizationMemberAsync(orgId, user.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "BOUND");
        var otherProject = await CreateProjectAsync(owner.Client, orgId, "OTHERA");
        await AddProjectMemberAsync(owner.Client, otherProject, otherProjectMember.UserId, "CONTRIBUTOR");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Boundary"))
            .GetProperty("id").GetGuid();

        foreach (var userId in new[] { organizationMember.UserId, otherProjectMember.UserId, outsider.UserId, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{itemId}/assign", new { assigneeId = userId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = outsider.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.PostAsync($"/api/v1/work-items/{itemId}/self-assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.True(await db.Organizations.AnyAsync(item => item.Id == outsiderOrg));
        Assert.Null((await db.WorkItems.FindAsync(itemId))!.AssigneeId);
    }

    [Fact]
    public async Task Archived_project_and_work_item_reject_all_assignment_mutations()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(orgId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "ARCHA");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "CONTRIBUTOR");
        var archivedItem = (await CreateWorkItemAsync(owner.Client, projectId, "Archived item"))
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{archivedItem}/assign", new { assigneeId = target.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{archivedItem}/archive", null)).StatusCode);
        await AssertAllAssignmentMutationsReturnAsync(owner.Client, archivedItem, target.UserId, HttpStatusCode.Conflict);
        var activeItem = (await CreateWorkItemAsync(owner.Client, projectId, "Archived project"))
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        await AssertAllAssignmentMutationsReturnAsync(owner.Client, activeItem, target.UserId, HttpStatusCode.Conflict);
        var detail = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{archivedItem}");
        Assert.Equal(target.UserId, detail.GetProperty("assigneeId").GetGuid());
    }

    [Fact]
    public async Task Membership_removal_clears_active_assignments_and_preserves_archived_history()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var projectMember = await CreateAuthenticatedUserAsync();
        var organizationMember = await CreateAuthenticatedUserAsync();
        var organizationAdmin = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { projectMember, organizationMember })
            await AddOrganizationMemberAsync(orgId, user.UserId, owner.UserId);
        await AddOrganizationMemberAsync(
            orgId,
            organizationAdmin.UserId,
            owner.UserId,
            OrganizationRole.Admin);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "CLEAN");
        await AddProjectMemberAsync(owner.Client, projectId, projectMember.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, organizationMember.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, organizationAdmin.UserId, "VIEWER");
        var projectCleanupItem = (await CreateWorkItemAsync(owner.Client, projectId, "Project cleanup"))
            .GetProperty("id").GetGuid();
        var activeOrganizationItem = (await CreateWorkItemAsync(owner.Client, projectId, "Organization cleanup"))
            .GetProperty("id").GetGuid();
        var archivedOrganizationItem = (await CreateWorkItemAsync(owner.Client, projectId, "History"))
            .GetProperty("id").GetGuid();
        var organizationAdminItem = (await CreateWorkItemAsync(owner.Client, projectId, "Admin access"))
            .GetProperty("id").GetGuid();
        await AssignAsync(owner.Client, projectCleanupItem, projectMember.UserId);
        await AssignAsync(owner.Client, activeOrganizationItem, organizationMember.UserId);
        await AssignAsync(owner.Client, archivedOrganizationItem, organizationMember.UserId);
        await AssignAsync(owner.Client, organizationAdminItem, organizationAdmin.UserId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{archivedOrganizationItem}/archive", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/projects/{projectId}/members/{projectMember.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/projects/{projectId}/members/{organizationAdmin.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/organizations/{orgId}/members/{organizationMember.UserId}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Null((await db.WorkItems.FindAsync(projectCleanupItem))!.AssigneeId);
        Assert.Null((await db.WorkItems.FindAsync(activeOrganizationItem))!.AssigneeId);
        Assert.Equal(organizationMember.UserId,
            (await db.WorkItems.FindAsync(archivedOrganizationItem))!.AssigneeId);
        Assert.Equal(organizationAdmin.UserId,
            (await db.WorkItems.FindAsync(organizationAdminItem))!.AssigneeId);
        Assert.True(await db.ActivityEvents.AnyAsync(activity =>
            activity.WorkItemId == projectCleanupItem
            && activity.ActorId == owner.UserId
            && activity.Type == ActivityTypes.WorkItemUnassigned));
        Assert.True(await db.ActivityEvents.AnyAsync(activity =>
            activity.WorkItemId == activeOrganizationItem
            && activity.ActorId == owner.UserId
            && activity.Type == ActivityTypes.WorkItemUnassigned));
        Assert.False(await db.ActivityEvents.AnyAsync(activity =>
            activity.WorkItemId == archivedOrganizationItem
            && activity.Type == ActivityTypes.WorkItemUnassigned));
    }

    [Fact]
    public async Task Assignment_enforces_csrf_mass_assignment_and_user_foreign_key()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(orgId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "SAFEA");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "CONTRIBUTOR");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Safe assignment"))
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/work-items/{itemId}", new
            {
                type = "TASK",
                title = "Core only",
                description = "",
                priority = "LOW",
                dueDate = (DateOnly?)null,
                assigneeId = target.UserId,
            })).StatusCode);
        Assert.Null((await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}"))
            .GetProperty("assigneeId").GetString());
        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign", new { assigneeId = target.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsync($"/api/v1/work-items/{itemId}/self-assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.DeleteAsync($"/api/v1/work-items/{itemId}/assignment")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var item = await db.WorkItems.FindAsync(itemId);
        item!.Assign(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_reassignment_requests_are_serialized_and_leave_one_valid_assignee()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var first = await CreateAuthenticatedUserAsync();
        var second = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { first, second })
            await AddOrganizationMemberAsync(orgId, user.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "RACEA");
        await AddProjectMemberAsync(owner.Client, projectId, first.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, second.UserId, "CONTRIBUTOR");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Race"))
            .GetProperty("id").GetGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            owner.Client.PostAsJsonAsync($"/api/v1/work-items/{itemId}/assign",
                new { assigneeId = index % 2 == 0 ? first.UserId : second.UserId })));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        var detail = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{itemId}");
        Assert.Contains(detail.GetProperty("assigneeId").GetGuid(), new[] { first.UserId, second.UserId });
    }

    [Fact]
    public async Task Concurrent_assignment_and_membership_removal_cannot_leave_an_inactive_assignee()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var orgId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(orgId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, orgId, "MEMRACE");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "CONTRIBUTOR");
        var itemId = (await CreateWorkItemAsync(owner.Client, projectId, "Membership race"))
            .GetProperty("id").GetGuid();

        var assignmentTask = owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/assign",
            new { assigneeId = target.UserId });
        var removalTask = owner.Client.DeleteAsync(
            $"/api/v1/organizations/{orgId}/members/{target.UserId}");
        var responses = await Task.WhenAll(assignmentTask, removalTask);
        var assignmentResponse = responses[0];
        var removalResponse = responses[1];
        Assert.Contains(assignmentResponse.StatusCode,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.BadRequest });
        Assert.Contains(removalResponse.StatusCode,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });
        if (removalResponse.StatusCode == HttpStatusCode.Conflict)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
                $"/api/v1/organizations/{orgId}/members/{target.UserId}")).StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Null((await db.WorkItems.FindAsync(itemId))!.AssigneeId);
        Assert.False(await db.OrganizationMembers.AnyAsync(member =>
            member.OrganizationId == orgId
            && member.UserId == target.UserId
            && member.RemovedAt == null));
    }

    private static async Task AssignAsync(HttpClient client, Guid workItemId, Guid userId) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = userId })).StatusCode);

    private static async Task AssertAllAssignmentMutationsReturnAsync(
        HttpClient client,
        Guid workItemId,
        Guid userId,
        HttpStatusCode statusCode)
    {
        Assert.Equal(statusCode, (await client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = userId })).StatusCode);
        Assert.Equal(statusCode,
            (await client.PostAsync($"/api/v1/work-items/{workItemId}/self-assign", null)).StatusCode);
        Assert.Equal(statusCode,
            (await client.DeleteAsync($"/api/v1/work-items/{workItemId}/assignment")).StatusCode);
    }
}
