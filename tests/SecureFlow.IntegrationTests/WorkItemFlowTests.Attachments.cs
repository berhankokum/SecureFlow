using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SecureFlow.IntegrationTests;

public partial class WorkItemFlowTests
{
    [Fact]
    public async Task Attachment_lifecycle_uses_private_storage_and_records_activity()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "FILE");
        var workItem = await CreateWorkItemAsync(owner.Client, projectId, "Attachment lifecycle");
        var workItemId = workItem.GetProperty("id").GetGuid();
        var fileBytes = Encoding.ASCII.GetBytes("%PDF-1.7\nSecureFlow attachment\n%%EOF");

        var uploadResponse = await UploadAsync(
            owner.Client,
            workItemId,
            "evidence.pdf",
            "application/pdf",
            fileBytes);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var attachmentId = uploaded.GetProperty("id").GetGuid();
        Assert.Equal("evidence.pdf", uploaded.GetProperty("originalFileName").GetString());
        Assert.Equal(fileBytes.Length, uploaded.GetProperty("size").GetInt64());
        Assert.True(uploaded.GetProperty("canDelete").GetBoolean());

        var listed = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/work-items/{workItemId}/attachments");
        Assert.Single(listed!);
        Assert.Equal(attachmentId, listed![0].GetProperty("id").GetGuid());

        var download = await owner.Client.GetAsync($"/api/v1/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("evidence.pdf", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(download.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(fileBytes, await download.Content.ReadAsByteArrayAsync());

        var activity = await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/work-items/{workItemId}/activity");
        var uploadActivity = Assert.Single(activity!, item =>
            item.GetProperty("type").GetString() == "ATTACHMENT_UPLOADED");
        Assert.Equal(attachmentId.ToString(),
            uploadActivity.GetProperty("metadata").GetProperty("attachmentId").GetString());

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Client.GetAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
        Assert.Empty((await owner.Client.GetFromJsonAsync<JsonElement[]>(
            $"/api/v1/work-items/{workItemId}/attachments"))!);
    }

    [Fact]
    public async Task Attachment_authorization_hides_tenants_and_enforces_contributor_ownership()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var firstContributor = await CreateAuthenticatedUserAsync();
        var secondContributor = await CreateAuthenticatedUserAsync();
        var viewer = await CreateAuthenticatedUserAsync();
        var outsider = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        foreach (var user in new[] { firstContributor, secondContributor, viewer })
        {
            await AddOrganizationMemberAsync(organizationId, user.UserId, owner.UserId);
        }

        var projectId = await CreateProjectAsync(owner.Client, organizationId, "AUTHFILE");
        await AddProjectMemberAsync(owner.Client, projectId, firstContributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, secondContributor.UserId, "CONTRIBUTOR");
        await AddProjectMemberAsync(owner.Client, projectId, viewer.UserId, "VIEWER");
        var workItem = await CreateWorkItemAsync(owner.Client, projectId, "Authorization");
        var workItemId = workItem.GetProperty("id").GetGuid();
        var uploaded = await UploadAsync(
            firstContributor.Client,
            workItemId,
            "owned.png",
            "image/png",
            ValidPng());
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var attachmentId = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await UploadAsync(viewer.Client, workItemId, "viewer.png", "image/png", ValidPng())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/work-items/{workItemId}/attachments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.Client.GetAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await secondContributor.Client.DeleteAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
    }

    [Fact]
    public async Task Attachment_validation_rejects_extension_mime_signature_and_size_mismatches()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "VALIDFILE");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Validation"))
            .GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(owner.Client, workItemId, "payload.exe", "application/octet-stream", [1, 2, 3])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(owner.Client, workItemId, "fake.pdf", "application/pdf", Encoding.ASCII.GetBytes("not a pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(owner.Client, workItemId, "wrong.png", "image/jpeg", ValidPng())).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await UploadAsync(
                owner.Client,
                workItemId,
                "large.png",
                "image/png",
                new byte[10 * 1024 * 1024 + 1])).StatusCode);
    }

    [Fact]
    public async Task Archived_resources_preserve_attachment_reads_but_block_mutations()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "ARCHFILE");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Archive files"))
            .GetProperty("id").GetGuid();
        var uploaded = await UploadAsync(owner.Client, workItemId, "kept.png", "image/png", ValidPng());
        var attachmentId = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.PostAsync($"/api/v1/projects/{projectId}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.Client.GetAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await UploadAsync(owner.Client, workItemId, "blocked.png", "image/png", ValidPng())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.Client.DeleteAsync($"/api/v1/attachments/{attachmentId}")).StatusCode);
    }

    [Fact]
    public async Task Attachment_uploads_are_rate_limited_per_authenticated_user()
    {
        var owner = await CreateAuthenticatedUserAsync();
        var organizationId = await CreateOrganizationAsync(owner.Client);
        var projectId = await CreateProjectAsync(owner.Client, organizationId, "RATEFILE");
        var workItemId = (await CreateWorkItemAsync(owner.Client, projectId, "Upload rate limit"))
            .GetProperty("id").GetGuid();

        for (var index = 0; index < 10; index++)
        {
            var response = await UploadAsync(
                owner.Client,
                workItemId,
                $"allowed-{index}.png",
                "image/png",
                ValidPng());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await UploadAsync(owner.Client, workItemId, "limited.png", "image/png", ValidPng())).StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid workItemId,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        return await client.PostAsync($"/api/v1/work-items/{workItemId}/attachments", form);
    }

    private static byte[] ValidPng() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];
}
