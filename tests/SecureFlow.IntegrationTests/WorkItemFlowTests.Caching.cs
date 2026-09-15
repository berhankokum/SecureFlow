using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Caching;
using SecureFlow.Modules.Organizations.Application.GetOrganization;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Cached_permissions_revoke_immediately_after_role_changes_and_removal()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var otherOwner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var otherOrg = await CreateOrganizationAsync(otherOwner.Client);
        await AddOrganizationMemberAsync(org, member.UserId, owner.UserId);
        var project = await CreateProjectAsync(owner.Client, org, "CACHE");
        var hiddenProject = await CreateProjectAsync(owner.Client, org, "PRIVATE");
        await AddProjectMemberAsync(owner.Client, project, member.UserId, "MANAGER");

        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, true);
        var summary = await member.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.Contains(Permissions.TeamCreate, PermissionNames(summary));
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync($"/api/v1/projects/{hiddenProject}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync($"/api/v1/organizations/{otherOrg}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.Client.GetAsync($"/api/v1/projects/{project}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/projects/{project}/members/{member.UserId}", new { role = "VIEWER" })).StatusCode);
        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, false);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PatchAsJsonAsync(
            $"/api/v1/projects/{project}", new { name = "Cannot update", description = "" })).StatusCode);
        summary = await member.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.DoesNotContain(Permissions.TeamCreate, PermissionNames(summary));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PostAsJsonAsync(
            $"/api/v1/organizations/{org}/teams", new { name = "Cannot create", description = "" })).StatusCode);

        // Promotion and subsequent downgrade must select different cache generations.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/organizations/{org}/members/{member.UserId}", new { role = "ADMIN" })).StatusCode);
        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, true);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync(
            $"/api/v1/organizations/{org}/members/{member.UserId}", new { role = "MEMBER" })).StatusCode);
        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, false);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/projects/{project}/members/{member.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync($"/api/v1/projects/{project}")).StatusCode);
        await AddProjectMemberAsync(owner.Client, project, member.UserId, "MANAGER");
        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, true);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync(
            $"/api/v1/organizations/{org}/members/{member.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync($"/api/v1/organizations/{org}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync($"/api/v1/projects/{project}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
            var membership = await db.OrganizationMembers.SingleAsync(x => x.OrganizationId == org && x.UserId == member.UserId);
            membership.Reactivate(OrganizationRole.Member, DateTimeOffset.UtcNow, owner.UserId);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetAsync($"/api/v1/projects/{project}")).StatusCode);
        var restored = await member.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.DoesNotContain(Permissions.TeamCreate, PermissionNames(restored));
    }

    [Fact]
    public async Task Organization_settings_hits_skip_metadata_load_and_committed_changes_invalidate()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var counter = factory.Services.GetRequiredService<CacheQueryCounter>();
        var before = counter.SettingsLoads;
        var first = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.Equal(before + 1, counter.SettingsLoads);
        var second = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.Equal(first.GetProperty("name").GetString(), second.GetProperty("name").GetString());
        Assert.Equal(before + 1, counter.SettingsLoads);
        await owner.Client.GetFromJsonAsync<JsonElement[]>("/api/v1/organizations");
        Assert.Equal(before + 1, counter.SettingsLoads);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        // No new settings API is introduced; exercise invalidation at the source-of-truth boundary.
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Organizations" SET "Name" = {"New settings"} WHERE "Id" = {org}""");
        var changed = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.Equal("New settings", changed.GetProperty("name").GetString());
        Assert.Equal(before + 2, counter.SettingsLoads);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Organizations" SET "Name" = {"Uncommitted"} WHERE "Id" = {org}""");
            var reader = scope.ServiceProvider.GetRequiredService<GetOrganizationHandler>();
            Assert.Equal("Uncommitted", (await reader.HandleAsync(org, owner.UserId))!.Name);
            var version = await db.Database.SqlQuery<string>($"""SELECT xmin::text AS "Value" FROM "Organizations" WHERE "Id" = {org}""").SingleAsync();
            var rawCache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            Assert.Null(await rawCache.GetAsync(new CacheAddress(CacheCategory.OrganizationSettings, org).Key(version)));
            await transaction.RollbackAsync();
        }
        var afterRollback = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/organizations/{org}");
        Assert.Equal("New settings", afterRollback.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Redis_outage_preserves_source_reads_and_permission_revocation()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var member = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(org, member.UserId, owner.UserId);
        var project = await CreateProjectAsync(owner.Client, org, "OUTAGE");
        await AddProjectMemberAsync(owner.Client, project, member.UserId, "MANAGER");
        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, true);
        // A second API instance sees the same PostgreSQL but cannot reach Redis.
        // The original Redis retains the warmed privileged entry throughout the mutation.
        using (var offline = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1")))
        {
            using var offlineOwner = offline.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            offlineOwner.DefaultRequestHeaders.Add("Cookie", owner.AuthCookie);
            offlineOwner.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(offlineOwner));
            Assert.Equal(HttpStatusCode.OK, (await offlineOwner.GetAsync($"/api/v1/organizations/{org}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await offlineOwner.GetAsync($"/api/v1/organizations/{org}/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await offlineOwner.GetAsync("/health/ready")).StatusCode);
            Assert.Equal("Degraded", await offlineOwner.GetStringAsync("/health/cache"));
            Assert.Equal(HttpStatusCode.NoContent, (await offlineOwner.PatchAsJsonAsync(
                $"/api/v1/projects/{project}/members/{member.UserId}", new { role = "VIEWER" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PatchAsJsonAsync(
                $"/api/v1/projects/{project}", new { name = "Still forbidden", description = "" })).StatusCode);
        }
        await AssertProjectPermissionAsync(member.Client, project, Permissions.ProjectUpdate, false);
    }

    private static string[] PermissionNames(JsonElement response) =>
        response.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static async Task AssertProjectPermissionAsync(HttpClient client, Guid project, string permission, bool expected)
    {
        var response = await client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{project}");
        Assert.Equal(expected, PermissionNames(response).Contains(permission, StringComparer.Ordinal));
    }
}
