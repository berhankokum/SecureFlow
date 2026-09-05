using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Files.Application;
using SecureFlow.Modules.Files.Presentation.Contracts;

namespace SecureFlow.Modules.Files.Presentation.Controllers;

[ApiController]
[Route("api/v1/work-items/{workItemId:guid}/attachments")]
[Authorize]
public sealed class WorkItemAttachmentsController(
    IAuthorizationService authorizationService,
    FileScopeHandler scopeHandler,
    AttachmentHandlers handlers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(workItemId, permission: null, cancellationToken) is { } error)
        {
            return error;
        }

        var result = await handlers.ListAsync(workItemId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(AttachmentResponse.From).ToArray())
            : FileErrorResult.Map(this, result.Error);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ValidateAntiforgery]
    [EnableRateLimiting("file-uploads")]
    [RequestSizeLimit(AttachmentContentValidator.MaximumFileSize + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = AttachmentContentValidator.MaximumFileSize + 64 * 1024)]
    public async Task<IActionResult> Upload(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(workItemId, Permissions.AttachmentUpload, cancellationToken) is { } error)
        {
            return error;
        }

        if (!Request.HasFormContentType)
        {
            return Problem(title: "multipart/form-data body gereklidir.", statusCode: StatusCodes.Status400BadRequest);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Files.Count != 1 || !string.Equals(form.Files[0].Name, "file", StringComparison.Ordinal))
        {
            return Problem(title: "Tam olarak bir 'file' alanı gönderilmelidir.", statusCode: StatusCodes.Status400BadRequest);
        }

        var file = form.Files[0];

        await using var stream = file.OpenReadStream();
        var result = await handlers.UploadAsync(
            workItemId,
            currentUserId,
            file.FileName,
            file.ContentType,
            file.Length,
            stream,
            cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/attachments/{result.Value.Id}", AttachmentResponse.From(result.Value))
            : FileErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid workItemId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var scope = await scopeHandler.FindWorkItemAsync(workItemId, cancellationToken);
        return scope is null
            ? NotFound()
            : await FileAuthorizationResult.RequireProjectAccessAsync(
                this,
                authorizationService,
                scope.ProjectId,
                scope.OrganizationId,
                permission,
                cancellationToken);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
