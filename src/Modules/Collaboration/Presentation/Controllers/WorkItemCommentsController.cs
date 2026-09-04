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
[Route("api/v1/work-items/{workItemId:guid}/comments")]
[Authorize]
public sealed class WorkItemCommentsController(
    IAuthorizationService authorizationService,
    CollaborationScopeHandler scopeHandler,
    CommentHandlers handlers,
    IValidator<CommentContentRequest> validator) : ControllerBase
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
            ? Ok(result.Value.Select(CommentResponse.From).ToArray())
            : CollaborationErrorResult.Map(this, result.Error);
    }

    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(
        Guid workItemId,
        CommentContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(workItemId, Permissions.CommentCreate, cancellationToken) is { } error)
        {
            return error;
        }

        if (await this.ValidateAsync(validator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await handlers.CreateAsync(
            new CreateCommentCommand(workItemId, currentUserId, request.Content),
            cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/comments/{result.Value.Id}", CommentResponse.From(result.Value))
            : CollaborationErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid workItemId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var scope = await scopeHandler.FindWorkItemAsync(workItemId, cancellationToken);
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
