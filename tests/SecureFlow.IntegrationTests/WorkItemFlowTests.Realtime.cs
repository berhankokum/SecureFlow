using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SecureFlow.Contracts.Realtime;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Realtime_groups_enforce_server_authorization_and_deliver_committed_events()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var target = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, target.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "LIVE");
        await AddProjectMemberAsync(owner.Client, projectId, target.UserId, "CONTRIBUTOR");

        await using var anonymousConnection = CreateRealtimeConnection(authCookie: null);
        await Assert.ThrowsAnyAsync<Exception>(() => anonymousConnection.StartAsync());

        await using var ownerConnection = CreateRealtimeConnection(owner.AuthCookie);
        await using var targetConnection = CreateRealtimeConnection(target.AuthCookie);
        await using var outsiderConnection = CreateRealtimeConnection(outsider.AuthCookie);
        await Task.WhenAll(
            ownerConnection.StartAsync(),
            targetConnection.StartAsync(),
            outsiderConnection.StartAsync());

        await ownerConnection.InvokeAsync("SubscribeOrganization", organizationId);
        await ownerConnection.InvokeAsync("SubscribeProject", projectId);
        await targetConnection.InvokeAsync("SubscribeOrganization", organizationId);
        await targetConnection.InvokeAsync("SubscribeProject", projectId);

        var organizationDenial = await Assert.ThrowsAsync<HubException>(() =>
            outsiderConnection.InvokeAsync("SubscribeOrganization", organizationId));
        var projectDenial = await Assert.ThrowsAsync<HubException>(() =>
            outsiderConnection.InvokeAsync("SubscribeProject", projectId));
        Assert.DoesNotContain(organizationId.ToString(), organizationDenial.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(projectId.ToString(), projectDenial.Message, StringComparison.OrdinalIgnoreCase);

        var workItemSignal = NewSignal<WorkItemUpdatedRealtimeEvent>(
            ownerConnection,
            RealtimeEventNames.WorkItemUpdated);
        var workItem = await CreateWorkItemAsync(owner.Client, projectId, "Live board update");
        var workItemId = workItem.GetProperty("id").GetGuid();
        var workItemEvent = await workItemSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(Guid.Empty, workItemEvent.EventId);
        Assert.Equal(organizationId, workItemEvent.OrganizationId);
        Assert.Equal(projectId, workItemEvent.ProjectId);
        Assert.Equal(workItemId, workItemEvent.WorkItemId);

        var notificationSignal = NewSignal<NotificationCreatedRealtimeEvent>(
            targetConnection,
            RealtimeEventNames.NotificationCreated);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign",
            new { assigneeId = target.UserId })).StatusCode);
        var notificationEvent = await notificationSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(Guid.Empty, notificationEvent.EventId);
        Assert.Equal(notificationEvent.NotificationId, notificationEvent.EventId);

        var commentSignal = NewSignal<CommentCreatedRealtimeEvent>(
            ownerConnection,
            RealtimeEventNames.CommentCreated);
        var commentResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments",
            new { content = "Realtime comment" });
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);
        var commentEvent = await commentSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(Guid.Empty, commentEvent.EventId);
        Assert.Equal(organizationId, commentEvent.OrganizationId);
        Assert.Equal(projectId, commentEvent.ProjectId);
        Assert.Equal(workItemId, commentEvent.WorkItemId);
        Assert.NotEqual(Guid.Empty, commentEvent.CommentId);

        var cleanupSignal = NewSignal<WorkItemUpdatedRealtimeEvent>(
            ownerConnection,
            RealtimeEventNames.WorkItemUpdated);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/projects/{projectId}/members/{target.UserId}")).StatusCode);
        var cleanupEvent = await cleanupSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(workItemId, cleanupEvent.WorkItemId);
        Assert.NotEqual(Guid.Empty, cleanupEvent.EventId);
    }

    private HubConnection CreateRealtimeConnection(string? authCookie) =>
        new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/realtime", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (authCookie is not null)
                {
                    options.Headers.Add("Cookie", authCookie);
                }
            })
            .Build();

    private static TaskCompletionSource<TMessage> NewSignal<TMessage>(
        HubConnection connection,
        string eventName)
    {
        var signal = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<TMessage>(eventName, message => signal.TrySetResult(message));
        return signal;
    }
}
