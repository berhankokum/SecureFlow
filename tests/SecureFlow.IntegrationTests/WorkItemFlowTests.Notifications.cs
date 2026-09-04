using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Modules.Notifications.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Assignment_notification_is_deduplicated_user_owned_and_read_endpoints_are_idempotent()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "NOTIFY");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "CONTRIBUTOR");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Notification source"))
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = target.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = target.UserId })).StatusCode);

        var targetList = await target.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        var assignmentNotifications = targetList.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == NotificationTypes.WorkItemAssigned)
            .ToArray();
        Assert.Single(assignmentNotifications);
        Assert.Equal(1, targetList.GetProperty("unreadCount").GetInt32());
        var notificationId = assignmentNotifications[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.PostAsync($"/api/v1/notifications/{notificationId}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await target.Client.PostAsync($"/api/v1/notifications/{notificationId}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await target.Client.PostAsync($"/api/v1/notifications/{notificationId}/read", null)).StatusCode);
        targetList = await target.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Equal(0, targetList.GetProperty("unreadCount").GetInt32());

        var preference = await target.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/preferences");
        Assert.True(preference.GetProperty("inAppEnabled").GetBoolean());
        Assert.False(preference.GetProperty("emailEnabled").GetBoolean());
        var updatedPreference = await target.Client.PutAsJsonAsync(
            "/api/v1/notifications/preferences", new { emailEnabled = true });
        Assert.Equal(HttpStatusCode.OK, updatedPreference.StatusCode);
        Assert.True((await updatedPreference.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("emailEnabled").GetBoolean());

        target.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await target.Client.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var duplicateSource = Guid.NewGuid();
        db.Notifications.AddRange(
            Notification.Create(organizationId, target.UserId, NotificationTypes.Mentioned,
                "Tekil", "Tekil kaynak", "WORK_ITEM", workItemId, duplicateSource, DateTimeOffset.UtcNow),
            Notification.Create(organizationId, target.UserId, NotificationTypes.Mentioned,
                "Tekil", "Tekil kaynak", "WORK_ITEM", workItemId, duplicateSource, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Mentions_require_matching_project_access_and_token_and_preserve_notification_history()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "MENTION");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "VIEWER");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Mention source"))
            .GetProperty("id").GetGuid();

        var invalid = await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = $"@{outsider.Email} bakar mısın?", mentionedUserIds = new[] { outsider.UserId } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        invalid = await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = $"@{target.Email} bakar mısın?", mentionedUserIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var createdResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = $"@{target.Email} bakar mısın?", mentionedUserIds = new[] { target.UserId } });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var commentId = created.GetProperty("id").GetGuid();
        Assert.Equal(target.UserId, created.GetProperty("mentions")[0].GetProperty("userId").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/comments/{commentId}",
            new { content = $"Güncel: @{target.Email}", mentionedUserIds = new[] { target.UserId } })).StatusCode);
        var targetNotifications = await target.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Single(targetNotifications.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("type").GetString() == NotificationTypes.Mentioned);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/comments/{commentId}")).StatusCode);
        targetNotifications = await target.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Single(targetNotifications.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("type").GetString() == NotificationTypes.Mentioned);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Single(await db.Mentions.Where(mention => mention.CommentId == commentId).ToArrayAsync());
        Assert.False(await db.Comments.AnyAsync(comment => comment.WorkItemId == workItemId
            && comment.Content.Contains(outsider.Email)));
    }

    [Fact]
    public async Task Status_and_comment_notifications_follow_recipient_matrix_and_suppress_actor_and_duplicates()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var manager = await CreateAuthenticatedUserAsync();
        var assignee = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { manager, assignee })
        {
            await AddOrganizationMemberAsync(organizationId, user.UserId, owner.UserId);
        }

        var projectId = await CreateProjectAsync(owner.Client, organizationId, "MATRIX");
        await AddProjectMemberAsync(owner.Client, projectId, manager.UserId, "MANAGER");
        await AddProjectMemberAsync(owner.Client, projectId, assignee.UserId, "CONTRIBUTOR");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Recipient matrix"))
            .GetProperty("id").GetGuid();
        await AssignAsync(owner.Client, workItemId, assignee.UserId);
        var workflow = await manager.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}/workflow");
        var todoId = workflow.GetProperty("states")[1].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await manager.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/transition", new { toStateId = todoId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await manager.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = $"@{assignee.Email} durum güncellendi", mentionedUserIds = new[] { assignee.UserId } })).StatusCode);

        var ownerItems = (await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(ownerItems, item => item.GetProperty("type").GetString() == NotificationTypes.WorkItemStatusChanged);
        Assert.Single(ownerItems, item => item.GetProperty("type").GetString() == NotificationTypes.CommentAdded);
        var assigneeItems = (await assignee.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(assigneeItems, item => item.GetProperty("type").GetString() == NotificationTypes.WorkItemStatusChanged);
        Assert.Single(assigneeItems, item => item.GetProperty("type").GetString() == NotificationTypes.Mentioned);
        Assert.DoesNotContain(assigneeItems, item => item.GetProperty("type").GetString() == NotificationTypes.CommentAdded);
        Assert.Empty((await manager.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications"))
            .GetProperty("items").EnumerateArray());
    }
}
