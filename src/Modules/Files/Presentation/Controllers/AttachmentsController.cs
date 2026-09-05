using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Files.Application;

namespace SecureFlow.Modules.Files.Presentation.Controllers;

[ApiController]
[Route("api/v1/attachments/{attachmentId:guid}")]
[Authorize]
public sealed class AttachmentsController(
    IAuthorizationService authorizationService,
    FileScopeHandler scopeHandler,
    AttachmentHandlers handlers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Download(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(attachmentId, permission: null, cancellationToken) is { } error)
        {
            return error;
        }

        var result = await handlers.DownloadAsync(attachmentId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, false)
            : FileErrorResult.Map(this, result.Error);
    }

    [HttpDelete]
    [ValidateAntiforgery]
    public async Task<IActionResult> Delete(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(attachmentId, Permissions.AttachmentDelete, cancellationToken) is { } error)
        {
            return error;
        }

        var result = await handlers.DeleteAsync(attachmentId, currentUserId, cancellationToken);
        return result.IsSuccess ? NoContent() : FileErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid attachmentId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var scope = await scopeHandler.FindAttachmentAsync(attachmentId, cancellationToken);
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
