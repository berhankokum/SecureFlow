using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureFlow.Api.Persistence;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Modules.Outbox.Domain;
using SecureFlow.Modules.Outbox.Infrastructure;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Mutations_persist_allow_listed_outbox_events_without_sensitive_payloads()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var assignee = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, assignee.UserId, owner.UserId);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "OUTBOX");
        await AddProjectMemberAsync(owner.Client, projectId, assignee.UserId, "CONTRIBUTOR");
        var workItem = await CreateWorkItemAsync(owner.Client, projectId, "Sensitive title must not enter outbox");
        var workItemId = workItem.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/assign", new { assigneeId = assignee.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{workItemId}/comments", new { content = "Sensitive comment must not enter outbox" })).StatusCode);
        var upload = await UploadAsync(owner.Client, workItemId, "evidence.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF"));
        var attachmentId = (await upload.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var events = await db.OutboxMessages.AsNoTracking().Where(x =>
            x.Type == OutboxEventTypes.WorkItemCreated ||
            x.Type == OutboxEventTypes.WorkItemAssigned ||
            x.Type == OutboxEventTypes.CommentCreated ||
            x.Type == OutboxEventTypes.NotificationCreated ||
            x.Type == OutboxEventTypes.AttachmentObjectDeleteRequested).ToArrayAsync();

        Assert.Contains(events, x => x.Type == OutboxEventTypes.WorkItemCreated);
        Assert.Contains(events, x => x.Type == OutboxEventTypes.WorkItemAssigned);
        Assert.Contains(events, x => x.Type == OutboxEventTypes.CommentCreated);
        Assert.Contains(events, x => x.Type == OutboxEventTypes.NotificationCreated && x.Id != Guid.Empty);
        Assert.Contains(events, x => x.Type == OutboxEventTypes.AttachmentObjectDeleteRequested);
        Assert.All(events, x => { Assert.Equal(1, x.SchemaVersion); Assert.Equal(OutboxStatus.Pending, x.Status); Assert.DoesNotContain("Sensitive", x.Payload, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(owner.Email, x.Payload, StringComparison.OrdinalIgnoreCase); });

        var processor = scope.ServiceProvider.GetServices<IHostedService>().OfType<OutboxProcessor>().Single();
        while (await processor.ProcessBatchAsync(CancellationToken.None) > 0) { }
        var eventIds = events.Select(x => x.Id).ToArray();
        var processed = await db.OutboxMessages.AsNoTracking().Where(x => eventIds.Contains(x.Id)).ToArrayAsync();
        Assert.All(processed, x => Assert.Equal(OutboxStatus.Processed, x.Status));
    }
}
