using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.WorkItems.Application;
using SecureFlow.Modules.WorkItems.Presentation.Contracts;

namespace SecureFlow.Modules.WorkItems.Presentation.Controllers;

[ApiController]
[Route("api/v1/work-items/{workItemId:guid}")]
[Authorize]
public sealed class WorkItemsController(
    IAuthorizationService authorizationService,
    FindWorkItemScopeHandler findScopeHandler,
    GetWorkItemHandler getHandler,
    UpdateWorkItemHandler updateHandler,
    ArchiveWorkItemHandler archiveHandler,
    IValidator<UpdateWorkItemRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(workItemId, permission: null, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await getHandler.HandleAsync(workItemId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(WorkItemResponse.From(result.Value))
            : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpPatch]
    [ValidateAntiforgery]
    public async Task<IActionResult> Update(
        Guid workItemId,
        UpdateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(workItemId, Permissions.WorkItemUpdate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(updateValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await updateHandler.HandleAsync(
            new UpdateWorkItemCommand(
                workItemId,
                WorkItemTypeParser.Parse(request.Type)!.Value,
                request.Title,
                request.Description,
                WorkItemPriorityParser.Parse(request.Priority)!.Value,
                request.DueDate,
                currentUserId),
            cancellationToken);
        return result.IsSuccess ? NoContent() : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpPost("archive")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Archive(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(workItemId, Permissions.WorkItemArchive, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await archiveHandler.HandleAsync(workItemId, currentUserId, cancellationToken);
        return result.IsSuccess ? NoContent() : WorkItemErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid workItemId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var scope = await findScopeHandler.HandleAsync(workItemId, cancellationToken);
        if (scope is null)
        {
            return NotFound();
        }

        return await WorkItemAuthorizationResult.RequireProjectAccessAsync(
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
