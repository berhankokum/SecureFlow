using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task An_owner_in_another_tenant_cannot_read_or_mutate_any_nested_resource()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organization = await CreateOrganizationAsync(owner.Client);
        var foreignOrganization = await CreateOrganizationAsync(outsider.Client);
        await CreateProjectAsync(outsider.Client, foreignOrganization, "FOREIGN");
        var project = await CreateProjectAsync(owner.Client, organization, "MATRIX");
        var item = await CreateWorkItemAsync(owner.Client, project, "Private task");
        var itemId = item.GetProperty("id").GetGuid();
        var labelResponse = await owner.Client.PostAsJsonAsync($"/api/v1/projects/{project}/labels", new { name = "Private label" });
        Assert.Equal(HttpStatusCode.Created, labelResponse.StatusCode);
        var labelId = (await labelResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var teamResponse = await owner.Client.PostAsJsonAsync($"/api/v1/organizations/{organization}/teams", new { name = "Private team" });
        Assert.Equal(HttpStatusCode.Created, teamResponse.StatusCode);
        var teamId = (await teamResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var path in new[]
        {
            $"organizations/{organization}", $"organizations/{organization}/dashboard",
            $"organizations/{organization}/members", $"organizations/{organization}/teams",
            $"organizations/{organization}/audit-logs", $"teams/{teamId}",
            $"projects/{project}", $"projects/{project}/members", $"projects/{project}/workflow",
            $"projects/{project}/labels", $"projects/{project}/work-items",
            $"work-items/{itemId}", $"work-items/{itemId}/comments",
            $"work-items/{itemId}/activity", $"work-items/{itemId}/attachments",
        })
        {
            var response = await outsider.Client.GetAsync($"/api/v1/{path}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Private", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PatchAsJsonAsync(
            $"/api/v1/labels/{labelId}", new { name = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.DeleteAsync($"/api/v1/labels/{labelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsJsonAsync(
            $"/api/v1/work-items/{itemId}/comments", new { content = "Hijacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.Client.PostAsJsonAsync(
            $"/api/v1/projects/{project}/work-items", ValidWorkItem("Hijacked"))).StatusCode);
        var labels = await owner.Client.GetFromJsonAsync<JsonElement[]>($"/api/v1/projects/{project}/labels");
        Assert.Equal("Private label", Assert.Single(labels!).GetProperty("name").GetString());
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>($"/api/v1/work-items/{itemId}/comments"))!);
        Assert.Single((await owner.Client.GetFromJsonAsync<JsonElement[]>($"/api/v1/projects/{project}/work-items"))!);
    }
}
