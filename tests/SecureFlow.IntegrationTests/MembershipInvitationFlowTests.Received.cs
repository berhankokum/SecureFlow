using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;

namespace SecureFlow.IntegrationTests;

public partial class MembershipInvitationFlowTests
{
    [Fact]
    public async Task Received_invitation_is_recipient_only_and_preserves_csrf_single_use_and_audit()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var stranger = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var token = await InviteAsync(owner.Client, organizationId, invited.Email, "MEMBER");
        var notifications = await invited.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        var invitationId = notifications.GetProperty("items")[0].GetProperty("resourceId").GetGuid();
        var path = $"/api/v1/invitations/received/{invitationId}";

        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"{path}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.PostAsync($"{path}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await invited.Client.GetAsync($"/api/v1/invitations/received/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await invited.Client.PostAsync($"/api/v1/invitations/received/{Guid.NewGuid()}/accept", null)).StatusCode);

        var detailsResponse = await invited.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var detailsText = await detailsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, detailsText, StringComparison.Ordinal);
        Assert.DoesNotContain("tokenHash", detailsText, StringComparison.OrdinalIgnoreCase);
        using var details = JsonDocument.Parse(detailsText);
        Assert.Equal("PENDING", details.RootElement.GetProperty("status").GetString());

        invited.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await invited.Client.PostAsync($"{path}/accept", null)).StatusCode);
        invited.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(invited.Client));
        var accept = await invited.Client.PostAsync($"{path}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await invited.Client.PostAsync($"{path}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await invited.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
        var acceptedDetails = await invited.Client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("ACCEPTED", acceptedDetails.GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Single(await db.OrganizationMembers.Where(member => member.OrganizationId == organizationId && member.UserId == invited.UserId).ToArrayAsync());
        var audit = await db.AuditLogs.Where(log => log.OrganizationId == organizationId && log.Action == "INVITATION_ACCEPTED").ToArrayAsync();
        Assert.Equal(invited.UserId, Assert.Single(audit).ActorUserId);
    }

    [Theory]
    [InlineData("REVOKED")]
    [InlineData("EXPIRED")]
    [InlineData("ARCHIVED")]
    public async Task Received_invitation_rejects_terminal_or_archived_state(string state)
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var stranger = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await InviteAsync(owner.Client, organizationId, invited.Email, "MEMBER");
        Guid invitationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var invitation = await db.OrganizationInvitations.SingleAsync(item => item.OrganizationId == organizationId);
            invitationId = invitation.Id;
            if (state == "REVOKED")
            {
                invitation.Revoke(DateTimeOffset.UtcNow);
                await db.SaveChangesAsync();
            }
            else if (state == "EXPIRED")
            {
                await db.OrganizationInvitations.Where(item => item.Id == invitationId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            }
            else
            {
                await db.Organizations.Where(item => item.Id == organizationId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ArchivedAt, DateTimeOffset.UtcNow));
            }
        }

        var path = $"/api/v1/invitations/received/{invitationId}";
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Client.PostAsync($"{path}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await invited.Client.PostAsync($"{path}/accept", null)).StatusCode);
        if (state != "ARCHIVED")
        {
            var details = await invited.Client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal(state, details.GetProperty("status").GetString());
        }
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.False(await verificationDb.OrganizationMembers.AnyAsync(item => item.OrganizationId == organizationId && item.UserId == invited.UserId));
    }

    [Fact]
    public async Task Concurrent_email_and_received_accept_create_exactly_one_membership()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var token = await InviteAsync(owner.Client, organizationId, invited.Email, "MEMBER");
        var notifications = await invited.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        var invitationId = notifications.GetProperty("items")[0].GetProperty("resourceId").GetGuid();
        var responses = await Task.WhenAll(
            invited.Client.PostAsync($"/api/v1/invitations/received/{invitationId}/accept", null),
            invited.Client.PostAsync($"/api/v1/invitations/{token}/accept", null));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Single(await db.OrganizationMembers.Where(item => item.OrganizationId == organizationId && item.UserId == invited.UserId).ToArrayAsync());
    }
}
