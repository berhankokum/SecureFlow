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
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public partial class WorkItemFlowTests(AuthWebApplicationFactory factory)
{
    private sealed record AuthenticatedUser(
        HttpClient Client,
        Guid UserId,
        string Email,
        string AuthCookie);

    [Fact]
    public async Task Lifecycle_derives_server_owned_fields_and_never_reuses_numbers()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "CORE");

        var firstResponse = await owner.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            new
            {
                type = "BUG",
                title = "  Tenant boundary  ",
                description = "  Reject IDOR  ",
                priority = "CRITICAL",
                dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
                organizationId = Guid.NewGuid(),
                reporterId = Guid.NewGuid(),
                number = 999,
            });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = first.GetProperty("id").GetGuid();
        Assert.Equal(organizationId, first.GetProperty("organizationId").GetGuid());
        Assert.Equal(owner.UserId, first.GetProperty("reporterId").GetGuid());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("assigneeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("assigneeEmail").ValueKind);
        Assert.Equal(owner.Email, first.GetProperty("reporterEmail").GetString());
        Assert.Equal(1, first.GetProperty("number").GetInt64());
        Assert.Equal("CORE-1", first.GetProperty("key").GetString());
        Assert.Equal("Tenant boundary", first.GetProperty("title").GetString());
        Assert.Contains("workitem.update", first.GetProperty("permissions")
            .EnumerateArray().Select(item => item.GetString()));

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PatchAsJsonAsync(
                $"/api/v1/work-items/{firstId}",
                new
                {
                    type = "STORY",
                    title = "Updated title",
                    description = "Updated",
                    priority = "HIGH",
                    dueDate = (DateOnly?)null,
                    reporterId = Guid.NewGuid(),
                    number = 55,
                })).StatusCode);
        var updated = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/work-items/{firstId}");
        Assert.Equal(owner.UserId, updated.GetProperty("reporterId").GetGuid());
        Assert.Equal(1, updated.GetProperty("number").GetInt64());
        Assert.Equal("STORY", updated.GetProperty("type").GetString());

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{firstId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/work-items/{firstId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PatchAsJsonAsync(
                $"/api/v1/work-items/{firstId}",
                ValidWorkItem("Cannot update"))).StatusCode);

        var second = await CreateWorkItemAsync(owner.Client, projectId, "Second");
        Assert.Equal(2, second.GetProperty("number").GetInt64());
        Assert.Equal("CORE-2", second.GetProperty("key").GetString());
    }

    [Fact]
    public async Task Project_roles_enforce_create_update_archive_and_read_boundaries()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var contributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var unassigned = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var outsiderOrganizationId = await CreateOrganizationAsync(outsider.Client);
        foreach (var user in new[] { contributor, viewer, unassigned })
        {
            await AddOrganizationMemberAsync(organizationId, user.UserId, owner.UserId);
        }

        var projectId = await CreateProjectAsync(owner.Client, organizationId, "ROLE");
        var outsiderProjectId = await CreateProjectAsync(outsider.Client, outsiderOrganizationId, "OTHER");
        await AddProjectMemberAsync(owner.Client, projectId, contributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");

        var created = await CreateWorkItemAsync(contributor.Client, projectId, "Contributor item");
        var workItemId = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await contributor.Client.PatchAsJsonAsync(
                $"/api/v1/work-items/{workItemId}",
                ValidWorkItem("Contributor updated"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await contributor.Client.PostAsync($"/api/v1/work-items/{workItemId}/archive", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await viewer.Client.GetAsync($"/api/v1/projects/{projectId}/work-items")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await viewer.Client.GetAsync($"/api/v1/work-items/{workItemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/work-items",
                ValidWorkItem("Viewer create"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.Client.PatchAsJsonAsync(
                $"/api/v1/work-items/{workItemId}",
                ValidWorkItem("Viewer update"))).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await unassigned.Client.GetAsync($"/api/v1/work-items/{workItemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/work-items/{workItemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/projects/{outsiderProjectId}/work-items")).StatusCode);
    }

    [Fact]
    public async Task Archived_project_blocks_all_work_item_mutations_but_preserves_reads()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "ARCH");
        var workItem = await CreateWorkItemAsync(owner.Client, projectId, "Before archive");
        var workItemId = workItem.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.Client.GetAsync($"/api/v1/work-items/{workItemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/work-items",
                ValidWorkItem("After archive"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PatchAsJsonAsync(
                $"/api/v1/work-items/{workItemId}",
                ValidWorkItem("After archive"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.PostAsync($"/api/v1/work-items/{workItemId}/archive", null)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_creates_receive_distinct_project_numbers()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "RACE");

        var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/work-items",
                ValidWorkItem($"Concurrent {index}"))));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (long)value),
            bodies.Select(body => body.GetProperty("number").GetInt64()).Order());
    }

    [Fact]
    public async Task Validation_csrf_and_database_tenant_constraint_are_enforced()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var otherOwner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var otherOrganizationId = await CreateOrganizationAsync(otherOwner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "SAFE");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/work-items",
                new { type = "EPIC", title = "", description = new string('a', 10_001), priority = "URGENT", dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) })).StatusCode);

        owner.Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/work-items",
                ValidWorkItem("No CSRF"))).StatusCode);
        owner.Client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(owner.Client));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        dbContext.WorkItems.Add(WorkItem.Create(
            otherOrganizationId,
            projectId,
            500,
            WorkItemType.Task,
            "Mismatched tenant",
            null,
            WorkItemPriority.Low,
            Guid.NewGuid(),
            owner.UserId,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private async Task<AuthenticatedUser> CreateAuthenticatedUserAsync()
    {
        var client = factory.CreateSecureClient();
        var email = $"workitem-itest-{Guid.NewGuid():N}@example.com";
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

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);
        var authCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .First(value => value.StartsWith("SecureFlow.Auth=", StringComparison.Ordinal))
            .Split(';', 2)[0];
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        return new AuthenticatedUser(client, userId, email, authCookie);
    }

    private static object ValidWorkItem(string title) => new
    {
        type = "TASK",
        title,
        description = "Core work",
        priority = "MEDIUM",
        dueDate = (DateOnly?)null,
    };

    private static async Task<JsonElement> CreateWorkItemAsync(
        HttpClient client,
        Guid projectId,
        string title)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-items",
            ValidWorkItem(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
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
            new { name = $"WorkItem Tenant {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, Guid organizationId, string key)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{organizationId}/projects",
            new { key, name = $"Project {key}", description = "" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
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

    private async Task AddOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        Guid addedBy,
        OrganizationRole role = OrganizationRole.Member)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        dbContext.OrganizationMembers.Add(OrganizationMember.CreateFromInvitation(
            organizationId,
            userId,
            role,
            DateTimeOffset.UtcNow,
            addedBy));
        await dbContext.SaveChangesAsync();
    }
}
