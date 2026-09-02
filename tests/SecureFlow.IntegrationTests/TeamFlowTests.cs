using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class TeamFlowTests(AuthWebApplicationFactory factory)
{
    private sealed record AuthenticatedUser(HttpClient Client, Guid UserId, string Email);

    [Fact]
    public async Task Owner_can_manage_team_members_and_archive_preserves_membership_history()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, member.UserId, owner.UserId);

        var create = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/teams",
            new { name = "  Platform  ", description = "  Core services  " });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var teamId = createBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Platform", createBody.RootElement.GetProperty("name").GetString());

        var add = await owner.Client.PostAsync($"/api/v1/teams/{teamId}/members/{member.UserId}", null);
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await owner.Client.PostAsync($"/api/v1/teams/{teamId}/members/{member.UserId}", null)).StatusCode);

        var details = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{teamId}");
        var listedMember = Assert.Single(details.GetProperty("members").EnumerateArray());
        Assert.Equal(member.Email, listedMember.GetProperty("email").GetString());

        var update = await owner.Client.PatchAsJsonAsync(
            $"/api/v1/teams/{teamId}",
            new { name = "Platform Engineering", description = "Updated" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/teams/{teamId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/teams/{teamId}/archive", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await owner.Client.DeleteAsync($"/api/v1/teams/{teamId}/members/{member.UserId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await owner.Client.PatchAsJsonAsync($"/api/v1/teams/{teamId}", new { name = "No", description = "No" })).StatusCode);

        var activeTeamId = await CreateTeamAsync(owner.Client, organizationId);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/teams/{activeTeamId}/members/{member.UserId}", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync(
                $"/api/v1/organizations/{organizationId}/members/{member.UserId}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.True(await dbContext.TeamMembers.AnyAsync(item => item.TeamId == teamId && item.UserId == member.UserId));
        Assert.False(await dbContext.TeamMembers.AnyAsync(item => item.TeamId == activeTeamId && item.UserId == member.UserId));
    }

    [Fact]
    public async Task Team_enforces_same_organization_membership_and_cross_tenant_concealment()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var outsiderOrganizationId = await CreateOrganizationAsync(outsider.Client);
        var teamId = await CreateTeamAsync(owner.Client, organizationId);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await owner.Client.PostAsync($"/api/v1/teams/{teamId}/members/{outsider.UserId}", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/teams/{teamId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/organizations/{organizationId}/teams")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/organizations/{outsiderOrganizationId}/teams")).StatusCode);
    }

    [Fact]
    public async Task Organization_member_can_read_teams_but_cannot_mutate_them()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, member.UserId, owner.UserId);
        var teamId = await CreateTeamAsync(owner.Client, organizationId);

        Assert.Equal(
            HttpStatusCode.OK,
            (await member.Client.GetAsync($"/api/v1/organizations/{organizationId}/teams")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.Client.GetAsync($"/api/v1/teams/{teamId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/teams",
                new { name = "Forbidden", description = "" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.Client.PostAsync($"/api/v1/teams/{teamId}/archive", null)).StatusCode);
    }

    [Fact]
    public async Task Team_mutations_require_csrf_valid_input_and_active_organization()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);

        var invalid = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/teams",
            new { name = "", description = new string('x', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var noCsrf = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/teams",
            new { name = "No CSRF", description = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        owner.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(owner.Client));

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            await dbContext.Organizations
                .Where(item => item.Id == organizationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.ArchivedAt,
                    DateTimeOffset.UtcNow));
        }

        var archived = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/teams",
            new { name = "Archived", description = "" });
        Assert.Equal(HttpStatusCode.Conflict, archived.StatusCode);
    }

    private async Task<AuthenticatedUser> CreateAuthenticatedUserAsync()
    {
        var client = factory.CreateSecureClient();
        var email = $"team-itest-{Guid.NewGuid():N}@example.com";
        const string password = "Sup3rSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        Assert.Equal(HttpStatusCode.NoContent,
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
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, code })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NoContent,
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

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = $"Team Tenant {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTeamAsync(HttpClient client, Guid organizationId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/teams",
            new { name = $"Team {Guid.NewGuid():N}", description = "" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task AddOrganizationMemberAsync(Guid organizationId, Guid userId, Guid addedBy)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        dbContext.OrganizationMembers.Add(OrganizationMember.CreateFromInvitation(
            organizationId,
            userId,
            OrganizationRole.Member,
            DateTimeOffset.UtcNow,
            addedBy));
        await dbContext.SaveChangesAsync();
    }
}
