using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Collaboration.Application;
using SecureFlow.Modules.Collaboration.Presentation.Contracts;

namespace SecureFlow.Modules.Collaboration.Presentation.Controllers;

[ApiController]
[Route("api/v1/comments/{commentId:guid}")]
[Authorize]
public sealed class CommentsController(
    IAuthorizationService authorizationService,
    CollaborationScopeHandler scopeHandler,
    CommentHandlers handlers,
    IValidator<CommentContentRequest> validator) : ControllerBase
{
    [HttpPatch]
    [ValidateAntiforgery]
    public async Task<IActionResult> Update(
        Guid commentId,
        CommentContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(commentId, Permissions.CommentUpdate, cancellationToken) is { } error)
        {
            return error;
        }

        if (await this.ValidateAsync(validator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await handlers.UpdateAsync(
            new UpdateCommentCommand(commentId, currentUserId, request.Content),
            cancellationToken);
        return result.IsSuccess ? NoContent() : CollaborationErrorResult.Map(this, result.Error);
    }

    [HttpDelete]
    [ValidateAntiforgery]
    public async Task<IActionResult> Delete(Guid commentId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(commentId, Permissions.CommentDelete, cancellationToken) is { } error)
        {
            return error;
        }

        var result = await handlers.DeleteAsync(commentId, currentUserId, cancellationToken);
        return result.IsSuccess ? NoContent() : CollaborationErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid commentId,
        string permission,
        CancellationToken cancellationToken)
    {
        var scope = await scopeHandler.FindCommentAsync(commentId, cancellationToken);
        return scope is null
            ? NotFound()
            : await CollaborationAuthorizationResult.RequireProjectAccessAsync(
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
