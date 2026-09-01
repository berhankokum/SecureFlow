using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Application.Invitations;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public partial class MembershipInvitationFlowTests(AuthWebApplicationFactory factory)
{
    private sealed record AuthenticatedUser(HttpClient Client, Guid UserId, string Email);

    [Fact]
    public async Task Invitation_email_contains_raw_token_while_database_stores_only_hash_and_accept_is_single_use()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var emailSender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        emailSender.Messages.Clear();

        var create = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = invited.Email, role = "MEMBER" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var token = ExtractInvitationToken(emailSender, invited.Email);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var invitation = await dbContext.OrganizationInvitations.SingleAsync(item =>
                item.OrganizationId == organizationId && item.Email == invited.Email.ToLowerInvariant());

            Assert.Equal(64, invitation.TokenHash.Length);
            Assert.DoesNotContain(token, invitation.TokenHash, StringComparison.Ordinal);
            Assert.Null(invitation.AcceptedAt);
            Assert.Null(invitation.RevokedAt);
        }

        var pending = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/organizations/{organizationId}/invitations");
        Assert.NotNull(pending);
        Assert.Single(pending);

        var details = await factory.CreateSecureClient().GetAsync($"/api/v1/invitations/{token}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using (var detailsBody = JsonDocument.Parse(await details.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PENDING", detailsBody.RootElement.GetProperty("status").GetString());
        }

        var accept = await invited.Client.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var secondAccept = await invited.Client.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, secondAccept.StatusCode);

        var members = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/organizations/{organizationId}/members");
        Assert.NotNull(members);
        Assert.Contains(members, member => member.GetProperty("userId").GetGuid() == invited.UserId);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var membership = await verificationDb.OrganizationMembers.SingleAsync(member =>
            member.OrganizationId == organizationId && member.UserId == invited.UserId);
        Assert.Equal(owner.UserId, membership.InvitedBy);
    }

    [Fact]
    public async Task Wrong_email_revoked_and_expired_invitations_are_rejected()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var wrongUser = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var emailSender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        emailSender.Messages.Clear();

        var create = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = invited.Email, role = "MEMBER" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var token = ExtractInvitationToken(emailSender, invited.Email);

        var wrongAccept = await wrongUser.Client.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, wrongAccept.StatusCode);

        using var createdBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var invitationId = createdBody.RootElement.GetProperty("id").GetGuid();
        var revoke = await owner.Client.DeleteAsync(
            $"/api/v1/organizations/{organizationId}/invitations/{invitationId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var revokedAccept = await invited.Client.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.Conflict, revokedAccept.StatusCode);

        emailSender.Messages.Clear();
        var expiringCreate = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = invited.Email, role = "MEMBER" });
        Assert.Equal(HttpStatusCode.Created, expiringCreate.StatusCode);
        var expiredToken = ExtractInvitationToken(emailSender, invited.Email);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var hash = InvitationTokenService.Hash(expiredToken);
            await dbContext.OrganizationInvitations
                .Where(invitation => invitation.TokenHash == hash)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    invitation => invitation.ExpiresAt,
                    DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var expiredAccept = await invited.Client.PostAsync(
            $"/api/v1/invitations/{expiredToken}/accept",
            null);
        Assert.Equal(HttpStatusCode.Conflict, expiredAccept.StatusCode);
    }

    [Fact]
    public async Task Resending_invitation_revokes_the_previous_token_and_keeps_one_pending_invitation()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);

        var firstToken = await InviteAsync(owner.Client, organizationId, invited.Email, "MEMBER");
        var secondToken = await InviteAsync(owner.Client, organizationId, invited.Email, "ADMIN");

        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await invited.Client.PostAsync($"/api/v1/invitations/{firstToken}/accept", null)).StatusCode);

        var pending = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/organizations/{organizationId}/invitations");
        Assert.NotNull(pending);
        Assert.Single(pending);
        Assert.Equal("ADMIN", pending[0].GetProperty("role").GetString());

        Assert.Equal(
            HttpStatusCode.OK,
            (await invited.Client.PostAsync($"/api/v1/invitations/{secondToken}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task Role_changes_member_removal_and_last_owner_rule_are_enforced_server_side()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var invited = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var token = await InviteAsync(owner.Client, organizationId, invited.Email, "MEMBER");
        Assert.Equal(
            HttpStatusCode.OK,
            (await invited.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);

        var makeAdmin = await owner.Client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/members/{invited.UserId}",
            new { role = "ADMIN" });
        Assert.Equal(HttpStatusCode.NoContent, makeAdmin.StatusCode);

        var adminCannotInviteOwner = await invited.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = $"future-owner-{Guid.NewGuid():N}@example.com", role = "OWNER" });
        Assert.Equal(HttpStatusCode.Forbidden, adminCannotInviteOwner.StatusCode);

        var existingMemberInvite = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = invited.Email, role = "MEMBER" });
        Assert.Equal(HttpStatusCode.Conflict, existingMemberInvite.StatusCode);

        var adminCannotChangeOwner = await invited.Client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/members/{owner.UserId}",
            new { role = "MEMBER" });
        Assert.Equal(HttpStatusCode.Forbidden, adminCannotChangeOwner.StatusCode);

        var adminCannotRemoveOwner = await invited.Client.DeleteAsync(
            $"/api/v1/organizations/{organizationId}/members/{owner.UserId}");
        Assert.Equal(HttpStatusCode.Forbidden, adminCannotRemoveOwner.StatusCode);

        var lastOwnerDemotion = await owner.Client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/members/{owner.UserId}",
            new { role = "MEMBER" });
        Assert.Equal(HttpStatusCode.Conflict, lastOwnerDemotion.StatusCode);

        var lastOwnerRemoval = await owner.Client.DeleteAsync(
            $"/api/v1/organizations/{organizationId}/members/{owner.UserId}");
        Assert.Equal(HttpStatusCode.Conflict, lastOwnerRemoval.StatusCode);

        var promoteSecondOwner = await owner.Client.PatchAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/members/{invited.UserId}",
            new { role = "OWNER" });
        Assert.Equal(HttpStatusCode.NoContent, promoteSecondOwner.StatusCode);

        var ownerLeaves = await owner.Client.DeleteAsync(
            $"/api/v1/organizations/{organizationId}/members/{owner.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, ownerLeaves.StatusCode);

        var invitedMembers = await invited.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/organizations/{organizationId}/members");
        Assert.NotNull(invitedMembers);
        Assert.Single(invitedMembers);
        Assert.Equal("OWNER", invitedMembers[0].GetProperty("role").GetString());

        var removedOwnerAccess = await owner.Client.GetAsync(
            $"/api/v1/organizations/{organizationId}/members");
        Assert.Equal(HttpStatusCode.NotFound, removedOwnerAccess.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Membership_endpoints_enforce_tenant_role_csrf_and_validation_boundaries()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var otherOwner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var otherOrganizationId = await CreateOrganizationAsync(otherOwner.Client);
        var token = await InviteAsync(owner.Client, organizationId, member.Email, "MEMBER");
        Assert.Equal(
            HttpStatusCode.OK,
            (await member.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/organizations/{otherOrganizationId}/members")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/organizations/{otherOrganizationId}/invitations")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.GetAsync($"/api/v1/organizations/{organizationId}/invitations")).StatusCode);

        var memberOrganizations = await member.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/organizations");
        Assert.NotNull(memberOrganizations);
        var memberOrganization = Assert.Single(
            memberOrganizations,
            organization => organization.GetProperty("id").GetGuid() == organizationId);
        var memberPermissions = memberOrganization.GetProperty("permissions")
            .EnumerateArray()
            .Select(permission => permission.GetString()!)
            .ToArray();
        Assert.Equal(["member.view"], memberPermissions);

        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var missingCsrf = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = "new@example.com", role = "MEMBER" });
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        owner.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(owner.Client));
        var invalid = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = "not-an-email", role = "SUPERADMIN" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Concurrent_owner_departures_never_leave_the_organization_without_an_owner()
    {
        var firstOwner = await CreateAuthenticatedUserAsync();
        var secondOwner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(firstOwner.Client);
        var token = await InviteAsync(firstOwner.Client, organizationId, secondOwner.Email, "OWNER");
        Assert.Equal(
            HttpStatusCode.OK,
            (await secondOwner.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);

        var responses = await Task.WhenAll(
            firstOwner.Client.DeleteAsync($"/api/v1/organizations/{organizationId}/members/{firstOwner.UserId}"),
            secondOwner.Client.DeleteAsync($"/api/v1/organizations/{organizationId}/members/{secondOwner.UserId}"));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.Equal(
            1,
            await dbContext.OrganizationMembers.CountAsync(member =>
                member.OrganizationId == organizationId
                && member.Role == OrganizationRole.Owner
                && member.RemovedAt == null));
    }

    [Fact]
    public async Task Archived_organization_and_email_delivery_failure_never_leave_an_active_invitation()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var emailSender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        emailSender.Messages.Clear();
        emailSender.ThrowOnSend = true;

        try
        {
            var deliveryFailure = await owner.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/invitations",
                new { email = $"smtp-failure-{Guid.NewGuid():N}@example.com", role = "MEMBER" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, deliveryFailure.StatusCode);
        }
        finally
        {
            emailSender.ThrowOnSend = false;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            Assert.DoesNotContain(
                await dbContext.OrganizationInvitations
                    .Where(invitation => invitation.OrganizationId == organizationId)
                    .ToArrayAsync(),
                invitation => invitation.AcceptedAt is null && invitation.RevokedAt is null);

            await dbContext.Organizations
                .Where(organization => organization.Id == organizationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    organization => organization.ArchivedAt,
                    DateTimeOffset.UtcNow));
        }

        var archivedInvite = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email = $"archived-{Guid.NewGuid():N}@example.com", role = "MEMBER" });
        Assert.Equal(HttpStatusCode.Conflict, archivedInvite.StatusCode);
    }

    private async Task<AuthenticatedUser> CreateAuthenticatedUserAsync()
    {
        var client = factory.CreateSecureClient();
        var email = $"member-itest-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3rSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password })).StatusCode);

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            userId = user!.Id;
            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(await userManager.GenerateEmailConfirmationTokenAsync(user)));
            Assert.Equal(
                HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, code })).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        return new AuthenticatedUser(client, userId, email);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/antiforgery/token");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .First(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';')];
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient ownerClient)
    {
        var response = await ownerClient.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = $"Membership Tenant {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<string> InviteAsync(
        HttpClient ownerClient,
        Guid organizationId,
        string email,
        string role)
    {
        var emailSender = factory.Services.GetRequiredService<TestIdentityEmailSender>();
        emailSender.Messages.Clear();
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/invitations",
            new { email, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ExtractInvitationToken(emailSender, email);
    }

    private static string ExtractInvitationToken(TestIdentityEmailSender emailSender, string email)
    {
        var message = Assert.Single(
            emailSender.Messages,
            item => string.Equals(item.To, email, StringComparison.OrdinalIgnoreCase));
        var match = InvitationTokenRegex().Match(message.HtmlBody);
        Assert.True(match.Success, "Invitation email did not contain a token link.");
        return match.Groups["token"].Value;
    }

    [GeneratedRegex(@"/invitations/(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex InvitationTokenRegex();
}
