using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Contracts.Notifications;
using SecureFlow.Modules.Notifications.Application;
using SecureFlow.Modules.Notifications.Domain;
using SecureFlow.Modules.Organizations.Application.Invitations;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Due_reminder_is_idempotent_and_targets_accessible_reporter()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "DUE");
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var response = await owner.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new
            {
                type = "TASK",
                title = "Due reminder target",
                description = "Background processing",
                priority = "MEDIUM",
                dueDate,
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var workItemId = body.GetProperty("id").GetGuid();

        var archivedResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new { type = "TASK", title = "Archived due item", description = "Excluded", priority = "LOW", dueDate });
        Assert.Equal(HttpStatusCode.Created, archivedResponse.StatusCode);
        var archivedId = (await archivedResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{archivedId}/archive", null)).StatusCode);

        var doneResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new { type = "TASK", title = "Done due item", description = "Excluded", priority = "LOW", dueDate });
        Assert.Equal(HttpStatusCode.Created, doneResponse.StatusCode);
        var doneId = (await doneResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var workflow = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}/workflow");
        var stateIds = workflow.GetProperty("states").EnumerateArray()
            .ToDictionary(state => state.GetProperty("name").GetString()!, state => state.GetProperty("id").GetGuid());
        foreach (var state in new[] { "TODO", "IN_PROGRESS", "IN_REVIEW", "DONE" })
        {
            Assert.Equal(HttpStatusCode.NoContent,
                (await owner.Client.PostAsJsonAsync(
                    $"/api/v1/work-items/{doneId}/transition",
                    new { toStateId = stateIds[state] })).StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<DeadlineReminderProcessor>();
            await processor.ProcessAsync(1000);
            await processor.ProcessAsync(1000);
        }

        using var assertionScope = factory.Services.CreateScope();
        var dbContext = assertionScope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var sourceEventId = ReminderEventId.Create(workItemId, dueDate);
        var reminders = await dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == owner.UserId
                && notification.Type == NotificationTypes.DueDateApproaching
                && notification.SourceEventId == sourceEventId)
            .ToArrayAsync();
        var reminder = Assert.Single(reminders);
        Assert.Equal(workItemId, reminder.ResourceId);
        Assert.DoesNotContain("<", reminder.Message, StringComparison.Ordinal);
        Assert.False(await dbContext.Notifications.AnyAsync(notification =>
            notification.SourceEventId == ReminderEventId.Create(archivedId, dueDate)
            || notification.SourceEventId == ReminderEventId.Create(doneId, dueDate)));
    }

    [Fact]
    public async Task Email_delivery_respects_opt_in_persists_success_and_encodes_html()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var notificationId = Guid.Empty;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            dbContext.NotificationPreferences.Add(NotificationPreference.Create(
                owner.UserId,
                true,
                DateTimeOffset.UtcNow));
            var notification = Notification.Create(
                organizationId,
                owner.UserId,
                NotificationTypes.CommentAdded,
                "Safe subject\r\nInjected",
                "<script>alert('x')</script>",
                NotificationResourceTypes.WorkItem,
                null,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);
            notificationId = notification.Id;
            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<NotificationEmailDeliveryProcessor>();
            await processor.DiscoverAsync(1000);
            await processor.DeliverAsync(1000);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var delivery = await dbContext.NotificationEmailDeliveries.AsNoTracking()
                .SingleAsync(item => item.NotificationId == notificationId);
            Assert.Equal(NotificationEmailDeliveryStatus.Sent, delivery.Status);
            Assert.Equal(1, delivery.AttemptCount);
            Assert.NotNull(delivery.SentAt);
        }

        var emailSender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        (string To, string Subject, string HtmlBody) message;
        lock (emailSender.Messages)
        {
            message = emailSender.Messages.Last(item => item.To == owner.Email && item.Subject.StartsWith("Safe subject"));
        }

        Assert.DoesNotContain('\r', message.Subject);
        Assert.DoesNotContain('\n', message.Subject);
        Assert.Contains("&lt;script&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_invitation_cleanup_soft_marks_history_and_releases_pending_uniqueness()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var now = DateTimeOffset.UtcNow;
        var email = $"expired-{Guid.NewGuid():N}@example.com";
        Guid expiredInvitationId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var invitation = OrganizationInvitation.Create(
                organizationId,
                email,
                OrganizationRole.Member,
                InvitationTokenService.Generate().Hash,
                now.AddDays(-2),
                now.AddDays(-1),
                owner.UserId);
            expiredInvitationId = invitation.Id;
            dbContext.OrganizationInvitations.Add(invitation);
            await dbContext.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<ExpiredInvitationCleanupProcessor>();
            Assert.True(await processor.ProcessAsync(1000) >= 1);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var expired = await dbContext.OrganizationInvitations
                .SingleAsync(item => item.Id == expiredInvitationId);
            Assert.NotNull(expired.ExpiredAt);
            Assert.Null(expired.RevokedAt);
            Assert.Null(expired.AcceptedAt);

            dbContext.OrganizationInvitations.Add(OrganizationInvitation.Create(
                organizationId,
                email,
                OrganizationRole.Member,
                InvitationTokenService.Generate().Hash,
                now,
                now.AddDays(1),
                owner.UserId));
            await dbContext.SaveChangesAsync();
        }
    }
}
