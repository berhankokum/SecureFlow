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
[Route("api/v1/projects/{projectId:guid}/work-items")]
[Authorize]
public sealed class ProjectWorkItemsController(
    IAuthorizationService authorizationService,
    FindProjectScopeHandler findProjectScopeHandler,
    CreateWorkItemHandler createHandler,
    ListWorkItemsHandler listHandler,
    IValidator<CreateWorkItemRequest> createValidator) : ControllerBase
{
    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(
        Guid projectId,
        CreateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(projectId, Permissions.WorkItemCreate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(createValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await createHandler.HandleAsync(
            new CreateWorkItemCommand(
                projectId,
                WorkItemTypeParser.Parse(request.Type)!.Value,
                request.Title,
                request.Description,
                WorkItemPriorityParser.Parse(request.Priority)!.Value,
                request.DueDate,
                currentUserId),
            cancellationToken);

        return result.IsSuccess
            ? Created($"/api/v1/work-items/{result.Value.Id}", WorkItemResponse.From(result.Value))
            : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(projectId, permission: null, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await listHandler.HandleAsync(projectId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(WorkItemResponse.From).ToArray())
            : WorkItemErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid projectId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var organizationId = await findProjectScopeHandler.HandleAsync(projectId, cancellationToken);
        if (organizationId is null)
        {
            return NotFound();
        }

        return await WorkItemAuthorizationResult.RequireProjectAccessAsync(
            this,
            authorizationService,
            projectId,
            organizationId.Value,
            permission,
            cancellationToken);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
