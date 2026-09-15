using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureFlow.Api.Persistence;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Concurrency_requires_strong_token_and_prevents_stale_mutations_without_side_effects()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "ETAG");
        var item = await CreateWorkItemAsync(owner.Client, project, "Original");
        var id = item.GetProperty("id").GetGuid();
        var url = $"/api/v1/work-items/{id}";
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("Cookie", owner.AuthCookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        var read = await client.GetAsync(url);
        var tag = read.Headers.ETag!.ToString();
        Assert.Equal($"\"{item.GetProperty("version").GetUInt32()}\"", tag);
        Assert.Equal((HttpStatusCode)428, (await client.PatchAsJsonAsync(url, ValidWorkItem("Missing"))).StatusCode);
        foreach (var invalid in new[] { "*", "W/" + tag, tag + ", " + tag, "7" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(ValidWorkItem("Invalid")) };
            request.Headers.TryAddWithoutValidation("If-Match", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        }
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", tag);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(url, ValidWorkItem("Winner"))).StatusCode);
        var latest = await client.GetAsync(url);
        Assert.NotEqual(tag, latest.Headers.ETag!.ToString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var outboxCount = await db.OutboxMessages.CountAsync();
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.PatchAsJsonAsync(url, ValidWorkItem("Loser"))).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.PostAsync(url + "/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.PostAsync(url + "/self-assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.PostAsJsonAsync(url + "/assign", new { assigneeId = owner.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.DeleteAsync(url + "/assignment")).StatusCode);
        var workflow = await client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{project}/workflow");
        var next = workflow.GetProperty("transitions").EnumerateArray().First(transition =>
            transition.GetProperty("fromStateId").GetGuid() == item.GetProperty("workflowStateId").GetGuid());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.PostAsJsonAsync(url + "/transition",
            new { toStateId = next.GetProperty("toStateId").GetGuid() })).StatusCode);
        Assert.Equal(outboxCount, await db.OutboxMessages.CountAsync());
        var stored = await db.WorkItems.AsNoTracking().SingleAsync(row => row.Id == id);
        Assert.Equal("Winner", stored.Title);
        Assert.Null(stored.AssigneeId);
        Assert.Null(stored.ArchivedAt);
    }

    [Fact]
    public async Task Concurrent_same_version_updates_have_exactly_one_winner()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "RACE");
        var item = await CreateWorkItemAsync(owner.Client, project, "Before race");
        var url = $"/api/v1/work-items/{item.GetProperty("id").GetGuid()}";
        var tag = (await owner.Client.GetAsync(url)).Headers.ETag!.ToString();
        async Task<HttpResponseMessage> Update(string title)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(ValidWorkItem(title)) };
            request.Headers.TryAddWithoutValidation("If-Match", tag);
            return await owner.Client.SendAsync(request);
        }
        var responses = await Task.WhenAll(Update("First"), Update("Second"));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Ef_version_check_rolls_back_outbox_when_another_context_wins()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var org = await CreateOrganizationAsync(owner.Client);
        var project = await CreateProjectAsync(owner.Client, org, "EFCONC");
        var id = (await CreateWorkItemAsync(owner.Client, project, "Original")).GetProperty("id").GetGuid();
        using var leftScope = factory.Services.CreateScope();
        using var rightScope = factory.Services.CreateScope();
        var left = leftScope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var right = rightScope.ServiceProvider.GetRequiredService<SecureFlowDbContext>();
        var winner = await left.WorkItems.SingleAsync(item => item.Id == id);
        var loser = await right.WorkItems.SingleAsync(item => item.Id == id);
        var now = DateTimeOffset.UtcNow;
        winner.Update(winner.Type, "Winner", null, winner.Priority, null, DateOnly.FromDateTime(now.UtcDateTime), now);
        loser.Update(loser.Type, "Loser", null, loser.Priority, null, DateOnly.FromDateTime(now.UtcDateTime), now);
        await left.SaveChangesAsync();
        var count = await left.OutboxMessages.CountAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync());
        Assert.Equal(count, await left.OutboxMessages.CountAsync());
        Assert.Equal("Winner", (await left.WorkItems.AsNoTracking().SingleAsync(item => item.Id == id)).Title);
    }
}
