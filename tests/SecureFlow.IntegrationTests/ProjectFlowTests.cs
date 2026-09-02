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
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class ProjectFlowTests(AuthWebApplicationFactory factory)
{
    private sealed record AuthenticatedUser(HttpClient Client, Guid UserId, string Email);

    [Fact]
    public async Task Project_lifecycle_enforces_unique_immutable_key_and_archived_state()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var otherOwner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var otherOrganizationId = await CreateOrganizationAsync(otherOwner.Client);

        var create = await owner.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/projects",
            new { key = " sf ", name = "  SecureFlow  ", description = "  Delivery  " });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var projectId = createBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("SF", createBody.RootElement.GetProperty("key").GetString());
        Assert.Equal("MANAGER", createBody.RootElement.GetProperty("projectRole").GetString());
        Assert.Contains("project.update", createBody.RootElement.GetProperty("permissions")
            .EnumerateArray().Select(item => item.GetString()));

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/projects",
                new { key = "SF", name = "Duplicate", description = "" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await otherOwner.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{otherOrganizationId}/projects",
                new { key = "SF", name = "Other Tenant", description = "" })).StatusCode);

        var update = await owner.Client.PatchAsJsonAsync(
            $"/api/v1/projects/{projectId}",
            new { key = "HACK", name = "SecureFlow Platform", description = "Updated" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var details = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}");
        Assert.Equal("SF", details.GetProperty("key").GetString());
        Assert.Equal("SecureFlow Platform", details.GetProperty("name").GetString());
        Assert.Single(details.GetProperty("members").EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}",
                new { name = "No", description = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/members",
                new { userId = otherOwner.UserId, role = "VIEWER" })).StatusCode);
    }

    [Fact]
    public async Task Project_roles_control_resource_access_and_manager_contributes_team_permissions()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var manager = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var unassigned = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { manager, contributor, viewer, unassigned })
        {
            await AddOrganizationMemberAsync(organizationId, user.UserId, owner.UserId);
        }

        var projectId = await CreateProjectAsync(owner.Client, organizationId, "ROLE");
        await AddProjectMemberAsync(owner.Client, projectId, manager.UserId, "MANAGER");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");

        var managerDetails = await manager.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{projectId}");
        Assert.Equal("MANAGER", managerDetails.GetProperty("projectRole").GetString());
        Assert.Contains("project.update", managerDetails.GetProperty("permissions")
            .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.Client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}",
                new { name = "Manager Updated", description = "" })).StatusCode);

        var managerOrganizations = await manager.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/organizations");
        Assert.NotNull(managerOrganizations);
        var permissions = Assert.Single(managerOrganizations!)
            .GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("team.create", permissions);
        Assert.Equal(HttpStatusCode.Created,
            (await manager.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/teams",
                new { name = "Managed Team", description = "" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await contributor.Client.GetAsync($"/api/v1/projects/{projectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await contributor.Client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}",
                new { name = "Forbidden", description = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.Client.GetAsync($"/api/v1/projects/{projectId}")).StatusCode);
        Assert.Empty(await unassigned.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/organizations/{organizationId}/projects") ?? []);
        Assert.Equal(HttpStatusCode.NotFound,
            (await unassigned.Client.GetAsync($"/api/v1/projects/{projectId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await manager.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await manager.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/teams",
                new { name = "No Longer Managed", description = "" })).StatusCode);
    }

    [Fact]
    public async Task Project_membership_enforces_same_tenant_validation_and_idor_boundaries()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var outsiderOrganizationId = await CreateOrganizationAsync(outsider.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "BOUND");
        var outsiderProjectId = await CreateProjectAsync(outsider.Client, outsiderOrganizationId, "BOUND");

        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/members",
                new { userId = outsider.UserId, role = "VIEWER" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/members",
                new { userId = owner.UserId, role = "MANAGER" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/members",
                new { userId = Guid.NewGuid(), role = "SUPERADMIN" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/projects/{outsiderProjectId}")).StatusCode);

        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/projects",
                new { key = "CSRF", name = "No CSRF", description = "" })).StatusCode);
        owner.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(owner.Client));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/organizations/{organizationId}/projects",
                new { key = "1-invalid", name = "", description = new string('x', 2001) })).StatusCode);
    }

    [Fact]
    public async Task Organization_removal_cleans_active_project_memberships_but_preserves_archived_history()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(organizationId, member.UserId, owner.UserId);
        var activeProjectId = await CreateProjectAsync(owner.Client, organizationId, "ACTIVE");
        var archivedProjectId = await CreateProjectAsync(owner.Client, organizationId, "HISTORY");
        await AddProjectMemberAsync(owner.Client, activeProjectId, member.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, archivedProjectId, member.UserId, "VIEWER");
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{archivedProjectId}/archive", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync(
                $"/api/v1/organizations/{organizationId}/members/{member.UserId}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        Assert.False(await dbContext.ProjectMembers.AnyAsync(item =>
            item.ProjectId == activeProjectId && item.UserId == member.UserId));
        Assert.True(await dbContext.ProjectMembers.AnyAsync(item =>
            item.ProjectId == archivedProjectId && item.UserId == member.UserId));
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.Client.GetAsync($"/api/v1/projects/{archivedProjectId}")).StatusCode);
    }

    private async Task<AuthenticatedUser> CreateAuthenticatedUserAsync()
    {
        var client = factory.CreateSecureClient();
        var email = $"project-itest-{Guid.NewGuid():N}@example.com";
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
            new { name = $"Project Tenant {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, Guid organizationId, string key)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/projects",
            new { key, name = $"Project {key}", description = "" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AddProjectMemberAsync(
        HttpClient client,
        Guid projectId,
        Guid userId,
        string role)
    {
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/members",
                new { userId, role })).StatusCode);
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
