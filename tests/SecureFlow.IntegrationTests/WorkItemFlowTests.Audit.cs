using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Audit.Domain;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Audit_captures_actor_and_deferred_workitem_assignment_and_download_events_without_secrets()
    {
        var owner = await CreateAuthenticatedUserAsync();
        owner.Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AuditTest/<script>probe</script>");
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "AUDIT");
        var item = await CreateWorkItemAsync(owner.Client, project, "Sensitive title must not enter audit");
        var id = item.GetProperty("id").GetGuid();
        var tag = (await owner.Client.GetAsync($"/api/v1/work-items/{id}")).Headers.ETag!.ToString();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync($"/api/v1/work-items/{id}", ValidWorkItem("Private replacement"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/self-assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/self-assign", null)).StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/work-items/{id}") { Content = JsonContent.Create(ValidWorkItem("Stale secret")) };
        stale.Headers.TryAddWithoutValidation("If-Match", tag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await owner.Client.SendAsync(stale)).StatusCode);
        var upload = await UploadAsync(owner.Client, id, "secret-name.pdf", "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nPrivate content\n%%EOF"));
        upload.EnsureSuccessStatusCode();
        var attachment = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.GetAsync($"/api/v1/attachments/{attachment}")).StatusCode);
        var url = $"/api/v1/organizations/{org}/audit-logs?pageSize=100";
        var response = await owner.Client.GetAsync(url);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var body = await response.Content.ReadAsStringAsync();
        var logs = JsonSerializer.Deserialize<JsonElement[]>(body)!;
        Assert.Single(logs, log => log.GetProperty("action").GetString() == "WORK_ITEM_UPDATED");
        Assert.Single(logs, log => log.GetProperty("action").GetString() == "WORK_ITEM_ASSIGNED");
        Assert.Contains(logs, log => log.GetProperty("action").GetString() == "WORK_ITEM_CREATED");
        Assert.Contains(logs, log => log.GetProperty("action").GetString() == "ATTACHMENT_DOWNLOADED");
        Assert.All(logs, log => Assert.Equal(owner.UserId, log.GetProperty("actorUserId").GetGuid()));
        Assert.DoesNotContain("Private", body);
        Assert.DoesNotContain("secret-name", body);
        Assert.DoesNotContain(owner.Email, body);
        Assert.All(logs, log => Assert.Equal("AuditTest/<script>probe</script>", log.GetProperty("userAgent").GetString()));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/attachments/{attachment}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/work-items/{id}/assignment")).StatusCode);
        var workflow = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{project}/workflow");
        var next = workflow.GetProperty("transitions").EnumerateArray().First(transition => transition.GetProperty("fromStateId").GetGuid() == item.GetProperty("workflowStateId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsJsonAsync($"/api/v1/work-items/{id}/transition", new { toStateId = next.GetProperty("toStateId").GetGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/work-items/{id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PostAsync($"/api/v1/projects/{project}/archive", null)).StatusCode);
        var completedLogs = (await owner.Client.GetFromJsonAsync<JsonElement[]>(url))!;
        foreach (var action in new[] { "ATTACHMENT_UPLOADED", "ATTACHMENT_DELETED", "WORK_ITEM_UNASSIGNED", "WORK_ITEM_TRANSITIONED", "WORK_ITEM_ARCHIVED", "PROJECT_ARCHIVED" })
            Assert.Single(completedLogs, log => log.GetProperty("action").GetString() == action);
    }

    [Fact]
    public async Task Audit_tenant_access_is_live_and_global_account_events_are_never_exposed()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var admin = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        await AddOrganizationMemberAsync(org, admin.UserId, owner.UserId, OrganizationRole.Admin);
        var url = $"/api/v1/organizations/{org}/audit-logs";
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.GetAsync(url)).StatusCode);
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        anonymous.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(anonymous));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = "not-found@example.com", password = "Private-Password123!" })).StatusCode);
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>(url + "?action=LOGIN_FAILURE"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.PatchAsJsonAsync($"/api/v1/organizations/{org}/members/{admin.UserId}", new { role = "MEMBER" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync($"/api/v1/organizations/{org}/members/{admin.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.GetAsync(url)).StatusCode);
        var logs = (await owner.Client.GetFromJsonAsync<JsonElement[]>(url))!;
        Assert.Contains(logs, log => log.GetProperty("action").GetString() == "ROLE_CHANGED");
        Assert.Contains(logs, log => log.GetProperty("action").GetString() == "MEMBER_REMOVED");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var failure = await db.AuditLogs.Where(log => log.Action == "LOGIN_FAILURE").OrderByDescending(log => log.Timestamp).FirstAsync();
        Assert.Null(failure.OrganizationId);
        Assert.Null(failure.ActorUserId);
        Assert.Null(failure.ResourceId);
        Assert.Equal("{}", failure.Metadata);
        var membership = await db.OrganizationMembers.SingleAsync(member => member.OrganizationId == org && member.UserId == admin.UserId);
        membership.Reactivate(OrganizationRole.Member, DateTimeOffset.UtcNow, owner.UserId);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.AuditLogs.CountAsync(log => log.OrganizationId == org && log.ResourceId == admin.UserId && log.Action == "MEMBER_ADDED"));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Audit_password_reset_is_global_atomic_and_invalid_token_writes_nothing()
    {
        var owner = await CreateAuthenticatedUserAsync();
        using var anonymous = factory.CreateSecureClient();
        anonymous.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(anonymous));
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var user = (await manager.FindByIdAsync(owner.UserId.ToString()))!;
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await manager.GeneratePasswordResetTokenAsync(user)));
        var invalid = await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = owner.Email, code = "aW52YWxpZA", newPassword = "NewSecret123!" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.False(await db.AuditLogs.AnyAsync(log => log.Action == "PASSWORD_CHANGED" && log.ResourceId == user.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/v1/auth/reset-password", new { email = owner.Email, code, newPassword = "NewSecret123!" })).StatusCode);
        var log = await db.AuditLogs.SingleAsync(log => log.Action == "PASSWORD_CHANGED" && log.ResourceId == user.Id);
        Assert.Null(log.OrganizationId);
        Assert.Null(log.ActorUserId);
        Assert.Equal("{}", log.Metadata);
    }

    [Fact]
    public async Task Audit_filters_pagination_and_database_immutability_are_enforced()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var resource = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var timestamp = new DateTimeOffset(2020, 1, 1, 23, 59, 59, TimeSpan.Zero);
        for (var i = 0; i < 3; i++) db.AuditLogs.Add(AuditLog.Create(org, owner.UserId, "WORK_ITEM_UPDATED", "WorkItem", resource, null, null, timestamp));
        await db.SaveChangesAsync();
        var expected = await db.AuditLogs.Where(log => log.ResourceId == resource).OrderByDescending(log => log.Id).Select(log => log.Id).ToArrayAsync();
        var url = $"/api/v1/organizations/{org}/audit-logs?resourceId={resource}&resourceType=WorkItem&action=WORK_ITEM_UPDATED&actorUserId={owner.UserId}&from=2020-01-01&to=2020-01-01&pageSize=2";
        var first = (await owner.Client.GetFromJsonAsync<JsonElement[]>(url))!;
        var second = (await owner.Client.GetFromJsonAsync<JsonElement[]>(url + "&page=2"))!;
        Assert.Equal(expected.Take(2), first.Select(log => log.GetProperty("id").GetGuid()));
        Assert.Equal(expected[2], Assert.Single(second).GetProperty("id").GetGuid());
        foreach (var invalid in new[] { "page=0", "pageSize=101", "action=UNKNOWN", "actorUserId=bad", "from=2021-01-01&to=2020-01-01" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.GetAsync($"/api/v1/organizations/{org}/audit-logs?{invalid}")).StatusCode);
        var id = expected[0];
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "AuditLogs" SET "Metadata" = "Metadata" WHERE "Id" = {id}"""));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "AuditLogs" WHERE "Id" = {id}"""));
        Assert.Equal(3, await db.AuditLogs.CountAsync(log => log.ResourceId == resource));
    }

    [Fact]
    public void Audit_request_context_uses_connection_ip_and_authenticated_identity_only()
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        http.Request.Headers["X-Forwarded-For"] = "198.51.100.2";
        var user = Guid.NewGuid();
        http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.ToString())]));
        var context = new AuditRequestContext(new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = http });
        Assert.Null(context.ActorUserId);
        Assert.Equal("192.0.2.1", context.IpAddress);
        http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(http.User.Claims, "Test"));
        Assert.Equal(user, context.ActorUserId);
    }
}
