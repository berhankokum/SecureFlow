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
    public async Task Organization_provisions_default_workflow_and_new_item_starts_in_backlog()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "FLOW");

        var workflow = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/projects/{projectId}/workflow");
        Assert.Equal(organizationId, workflow.GetProperty("organizationId").GetGuid());
        Assert.Equal("DEFAULT", workflow.GetProperty("name").GetString());
        Assert.Equal(
            ["BACKLOG", "TODO", "IN_PROGRESS", "IN_REVIEW", "DONE"],
            workflow.GetProperty("states").EnumerateArray()
                .Select(state => state.GetProperty("name").GetString()!).ToArray());
        Assert.Equal(4, workflow.GetProperty("transitions").GetArrayLength());

        var item = await CreateWorkItemAsync(owner.Client, projectId, "Initial state");
        var backlog = workflow.GetProperty("states")[0];
        Assert.Equal(backlog.GetProperty("id").GetGuid(), item.GetProperty("workflowStateId").GetGuid());
        Assert.Equal("BACKLOG", item.GetProperty("workflowStateName").GetString());
        Assert.Equal("TODO", item.GetProperty("workflowStateCategory").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Equal(1, await dbContext.Workflows.CountAsync(value => value.OrganizationId == organizationId));
        Assert.Equal(5, await dbContext.WorkflowStates.CountAsync(
            value => value.WorkflowId == workflow.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Defined_transitions_succeed_invalid_transition_is_conflict_and_state_is_not_mass_assignable()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "MOVE");
        var workflow = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/projects/{projectId}/workflow");
        var stateIds = workflow.GetProperty("states").EnumerateArray()
            .ToDictionary(
                state => state.GetProperty("name").GetString()!,
                state => state.GetProperty("id").GetGuid());
        var item = await CreateWorkItemAsync(owner.Client, projectId, "Move me");
        var workItemId = item.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition",
                new { toStateId = stateIds["DONE"] })).StatusCode);

        foreach (var state in new[] { "TODO", "IN_PROGRESS", "IN_REVIEW", "DONE" })
        {
            Assert.Equal(HttpStatusCode.NoContent,
                (await owner.Client.PostAsJsonAsync(
                    $"/api/v1/work-items/{workItemId}/transition",
                    new { toStateId = stateIds[state] })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PatchAsJsonAsync(
                $"/api/v1/work-items/{workItemId}",
                new
                {
                    type = "TASK",
                    title = "Mass assignment ignored",
                    description = "Workflow state must stay server-owned",
                    priority = "HIGH",
                    dueDate = (DateOnly?)null,
                    workflowStateId = stateIds["BACKLOG"],
                })).StatusCode);
        var current = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/work-items/{workItemId}");
        Assert.Equal(stateIds["DONE"], current.GetProperty("workflowStateId").GetGuid());
        Assert.Equal("DONE", current.GetProperty("workflowStateName").GetString());
    }

    [Fact]
    public async Task Contributor_can_transition_but_cannot_approve_and_viewer_cannot_transition()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, contributor.UserId, owner.UserId);
        await AddOrganizationMemberAsync(organizationId, viewer.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "PERM");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");
        var workflow = await contributor.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/projects/{projectId}/workflow");
        var ids = workflow.GetProperty("states").EnumerateArray()
            .ToDictionary(
                state => state.GetProperty("name").GetString()!,
                state => state.GetProperty("id").GetGuid());
        var item = await CreateWorkItemAsync(contributor.Client, projectId, "Permission flow");
        var workItemId = item.GetProperty("id").GetGuid();

        foreach (var state in new[] { "TODO", "IN_PROGRESS", "IN_REVIEW" })
        {
            Assert.Equal(HttpStatusCode.NoContent,
                (await contributor.Client.PostAsJsonAsync(
                    $"/api/v1/work-items/{workItemId}/transition",
                    new { toStateId = ids[state] })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden,
            (await contributor.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition",
                new { toStateId = ids["DONE"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition",
                new { toStateId = ids["DONE"] })).StatusCode);
    }

    [Fact]
    public async Task Workflow_enforces_tenant_archive_csrf_and_concurrent_transition_boundaries()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "SAFEFLOW");
        var workflow = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/projects/{projectId}/workflow");
        var todoId = workflow.GetProperty("states")[1].GetProperty("id").GetGuid();
        var item = await CreateWorkItemAsync(owner.Client, projectId, "Concurrent transition");
        var workItemId = item.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/workflow")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition",
                new { toStateId = todoId })).StatusCode);

        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition",
                new { toStateId = todoId })).StatusCode);
        owner.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(owner.Client));

        var responses = await Task.WhenAll(
            owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition", new { toStateId = todoId }),
            owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition", new { toStateId = todoId }));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, responses.Count(response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed));

        var second = await CreateWorkItemAsync(owner.Client, projectId, "Archived item");
        var secondId = second.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{secondId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{secondId}/transition",
                new { toStateId = todoId })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.Client.GetAsync($"/api/v1/projects/{projectId}/workflow")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/transition",
                new { toStateId = todoId })).StatusCode);
    }
}
