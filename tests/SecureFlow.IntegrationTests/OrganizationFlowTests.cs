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
public class OrganizationFlowTests(AuthWebApplicationFactory factory)
{
    private static string UniqueEmail() => $"org-itest-{Guid.NewGuid():N}@example.com";

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/antiforgery/token");
        response.EnsureSuccessStatusCode();

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .First(cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

        return setCookie["XSRF-TOKEN=".Length..setCookie.IndexOf(';')];
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateSecureClient();
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirm = await client.PostAsJsonAsync(
                "/api/v1/auth/confirm-email",
                new { userId = user!.Id, code = encodedToken });
            Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        return client;
    }

    [Fact]
    public async Task Create_organization_persists_creator_as_owner_and_lists_the_tenant()
    {
        var client = await CreateAuthenticatedClientAsync();
        var name = $"Güvenli Akış {Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name, logoUrl = "https://example.com/logo.png" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdBody = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var organizationId = createdBody.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("OWNER", createdBody.RootElement.GetProperty("role").GetString());
        var effectivePermissions = createdBody.RootElement.GetProperty("permissions")
            .EnumerateArray()
            .Select(permission => permission.GetString()!)
            .ToArray();
        Assert.Contains("organization.manage", effectivePermissions);
        Assert.Contains("member.role.change", effectivePermissions);
        Assert.Contains("audit.view", effectivePermissions);
        Assert.StartsWith("guvenli-akis-", createdBody.RootElement.GetProperty("slug").GetString());

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var organization = await dbContext.Organizations.SingleAsync(item => item.Id == organizationId);
            var membership = await dbContext.OrganizationMembers
                .SingleAsync(item => item.OrganizationId == organizationId);

            Assert.Equal(organization.CreatedBy, membership.UserId);
            Assert.Equal(OrganizationRole.Owner, membership.Role);
            Assert.Null(membership.RemovedAt);
        }

        var list = await client.GetAsync("/api/v1/organizations");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(
            listBody.RootElement.EnumerateArray(),
            organization => organization.GetProperty("id").GetGuid() == organizationId);
    }

    [Fact]
    public async Task Duplicate_generated_slug_returns_conflict_without_creating_an_orphan_membership()
    {
        var firstClient = await CreateAuthenticatedClientAsync();
        var secondClient = await CreateAuthenticatedClientAsync();
        var name = $"Duplicate Tenant {Guid.NewGuid():N}";

        var first = await firstClient.PostAsJsonAsync("/api/v1/organizations", new { name });
        var second = await secondClient.PostAsJsonAsync("/api/v1/organizations", new { name });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var slug = firstBody.RootElement.GetProperty("slug").GetString();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var organizationIds = await dbContext.Organizations
            .Where(organization => organization.Slug == slug)
            .Select(organization => organization.Id)
            .ToArrayAsync();

        Assert.Single(organizationIds);
        Assert.Equal(
            1,
            await dbContext.OrganizationMembers.CountAsync(
                member => member.OrganizationId == organizationIds[0]));
    }

    [Fact]
    public async Task User_cannot_read_another_users_organization_by_id_or_list()
    {
        var ownerClient = await CreateAuthenticatedClientAsync();
        var otherClient = await CreateAuthenticatedClientAsync();

        var create = await ownerClient.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = $"Private Tenant {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var body = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var organizationId = body.RootElement.GetProperty("id").GetGuid();

        var crossTenantGet = await otherClient.GetAsync($"/api/v1/organizations/{organizationId}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantGet.StatusCode);

        var list = await otherClient.GetFromJsonAsync<JsonElement[]>("/api/v1/organizations");
        Assert.NotNull(list);
        Assert.DoesNotContain(list, organization => organization.GetProperty("id").GetGuid() == organizationId);
    }

    [Fact]
    public async Task Create_requires_authentication_csrf_and_valid_input()
    {
        var anonymousClient = factory.CreateSecureClient();
        anonymousClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(anonymousClient));
        var anonymous = await anonymousClient.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = "Anonymous" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var authenticatedClient = await CreateAuthenticatedClientAsync();
        authenticatedClient.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var missingCsrf = await authenticatedClient.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = "Missing CSRF" });
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        authenticatedClient.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN",
            await GetCsrfTokenAsync(authenticatedClient));
        var invalid = await authenticatedClient.PostAsJsonAsync(
            "/api/v1/organizations",
            new { name = "", logoUrl = "javascript:alert(1)" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
