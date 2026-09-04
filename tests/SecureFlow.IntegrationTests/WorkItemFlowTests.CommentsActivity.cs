using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Contracts.Activity;
using SecureFlow.Modules.Collaboration.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Comments_enforce_ownership_moderation_and_soft_delete_projection()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { contributor, viewer })
        {
            await AddOrganizationMemberAsync(organizationId, user.UserId, owner.UserId);
        }

        var projectId = await CreateProjectAsync(owner.Client, organizationId, "COMMENT");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Review comments"))
            .GetProperty("id").GetGuid();

        var createResponse = await contributor.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = "  Yetki zinciri kontrol edildi.  " });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetGuid();
        Assert.Equal("Yetki zinciri kontrol edildi.", created.GetProperty("content").GetString());
        Assert.Equal(contributor.UserId, created.GetProperty("authorId").GetGuid());
        Assert.Equal(contributor.Email, created.GetProperty("authorEmail").GetString());
        Assert.True(created.GetProperty("canUpdate").GetBoolean());
        Assert.True(created.GetProperty("canDelete").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/comments/{commentId}", new { content = "Moderation edit kabul edilmez" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await contributor.Client.PatchAsJsonAsync(
            $"/api/v1/comments/{commentId}", new { content = "Güncellendi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments", new { content = "Viewer" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await viewer.Client.GetAsync($"/api/v1/work-items/{workItemId}/comments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await viewer.Client.GetAsync($"/api/v1/work-items/{workItemId}/activity")).StatusCode);

        var ownerList = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/work-items/{workItemId}/comments");
        Assert.True(ownerList[0].GetProperty("canDelete").GetBoolean());
        Assert.False(ownerList[0].GetProperty("canUpdate").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/comments/{commentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/comments/{commentId}")).StatusCode);

        var deleted = (await contributor.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/work-items/{workItemId}/comments"))[0];
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("content").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, deleted.GetProperty("deletedAt").ValueKind);
        Assert.False(deleted.GetProperty("canUpdate").GetBoolean());
        Assert.False(deleted.GetProperty("canDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await contributor.Client.PatchAsJsonAsync(
            $"/api/v1/comments/{commentId}", new { content = "Silinmiş" })).StatusCode);
    }

    [Fact]
    public async Task Comments_hide_tenants_validate_input_csrf_and_archived_resources()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await CreateOrganizationAsync(outsider.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "CSAFE");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Comment security"))
            .GetProperty("id").GetGuid();

        foreach (var content in new[] { "", "   ", new string('x', 5_001) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync(
                $"/api/v1/work-items/{workItemId}/comments", new { content })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/work-items/{workItemId}/comments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/work-items/{workItemId}/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments", new { content = "IDOR" })).StatusCode);

        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments", new { content = "CSRF" })).StatusCode);
        owner.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(owner.Client));

        var protectedComment = await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = "Protected" });
        Assert.Equal(HttpStatusCode.Created, protectedComment.StatusCode);
        var commentId = (await protectedComment.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PatchAsJsonAsync(
            $"/api/v1/comments/{commentId}", new { content = "IDOR" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.DeleteAsync($"/api/v1/comments/{commentId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{workItemId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments", new { content = "Archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.Client.GetAsync($"/api/v1/work-items/{workItemId}/comments")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        dbContext.Comments.Add(Comment.Create(
            Guid.NewGuid(),
            workItemId,
            owner.UserId,
            "Mismatched tenant",
            DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Activity_captures_deferred_work_item_assignment_transition_label_and_comment_events()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "ACT");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "CONTRIBUTOR");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Activity source"))
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = target.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = target.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/work-items/{workItemId}/assignment")).StatusCode);
        var workflow = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/projects/{projectId}/workflow");
        var todoId = workflow.GetProperty("states")[1].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await target.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/transition", new { toStateId = todoId })).StatusCode);
        var labelId = await CreateLabelAsync(owner.Client, projectId, "Security");
        Assert.Equal(HttpStatusCode.NoContent, (await target.Client.PostAsync(
            $"/api/v1/work-items/{workItemId}/labels/{labelId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await target.Client.PostAsync(
            $"/api/v1/work-items/{workItemId}/labels/{labelId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await target.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments", new { content = "Hazır" })).StatusCode);

        var activities = await owner.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/work-items/{workItemId}/activity");
        var types = activities.EnumerateArray()
            .Select(activity => activity.GetProperty("type").GetString()!)
            .ToArray();
        Assert.Equal(1, types.Count(type => type == ActivityTypes.WorkItemCreated));
        Assert.Equal(1, types.Count(type => type == ActivityTypes.WorkItemAssigned));
        Assert.Equal(1, types.Count(type => type == ActivityTypes.WorkItemUnassigned));
        Assert.Equal(1, types.Count(type => type == ActivityTypes.WorkItemTransitioned));
        Assert.Equal(1, types.Count(type => type == ActivityTypes.LabelAssigned));
        Assert.Equal(1, types.Count(type => type == ActivityTypes.CommentCreated));
        var transition = activities.EnumerateArray().Single(activity =>
            activity.GetProperty("type").GetString() == ActivityTypes.WorkItemTransitioned);
        Assert.Equal("BACKLOG", transition.GetProperty("metadata").GetProperty("fromState").GetString());
        Assert.Equal("TODO", transition.GetProperty("metadata").GetProperty("toState").GetString());
        Assert.Equal(target.Email, transition.GetProperty("actorEmail").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Equal(types.Length, await dbContext.ActivityEvents.CountAsync(
            activity => activity.WorkItemId == workItemId));
    }
}
