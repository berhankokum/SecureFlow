using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.WorkItems.Application;
using SecureFlow.Modules.WorkItems.Application.Models;
using SecureFlow.Modules.WorkItems.Presentation.Contracts;

namespace SecureFlow.Modules.WorkItems.Presentation.Controllers;

[ApiController]
[Authorize]
public sealed class LabelsController(
    IAuthorizationService authorizationService,
    FindProjectScopeHandler findProjectScope,
    FindWorkItemScopeHandler findWorkItemScope,
    LabelHandlers handlers,
    IValidator<LabelNameRequest> validator) : ControllerBase
{
    [HttpGet("api/v1/projects/{projectId:guid}/labels")]
    public async Task<IActionResult> List(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId)) return Unauthorized();
        var organizationId = await findProjectScope.HandleAsync(projectId, cancellationToken);
        if (await AuthorizeAsync(organizationId is null ? null : new(projectId, organizationId.Value), null, cancellationToken) is { } error)
            return error;
        var result = await handlers.ListAsync(projectId, userId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpPost("api/v1/projects/{projectId:guid}/labels")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(Guid projectId, LabelNameRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId)) return Unauthorized();
        var organizationId = await findProjectScope.HandleAsync(projectId, cancellationToken);
        if (await AuthorizeAsync(organizationId is null ? null : new(projectId, organizationId.Value), Permissions.LabelCreate, cancellationToken) is { } error)
            return error;
        if (await this.ValidateAsync(validator, request) is { } validationError) return validationError;
        var result = await handlers.CreateAsync(projectId, request.Name, userId, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/projects/{projectId}/labels", result.Value)
            : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpPatch("api/v1/labels/{labelId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Update(Guid labelId, LabelNameRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId)) return Unauthorized();
        if (await AuthorizeAsync(await handlers.FindScopeAsync(labelId, cancellationToken), Permissions.LabelUpdate, cancellationToken) is { } error)
            return error;
        if (await this.ValidateAsync(validator, request) is { } validationError) return validationError;
        var result = await handlers.UpdateAsync(labelId, request.Name, userId, cancellationToken);
        return result.IsSuccess ? NoContent() : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpDelete("api/v1/labels/{labelId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Delete(Guid labelId, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId)) return Unauthorized();
        if (await AuthorizeAsync(await handlers.FindScopeAsync(labelId, cancellationToken), Permissions.LabelDelete, cancellationToken) is { } error)
            return error;
        var result = await handlers.DeleteAsync(labelId, userId, cancellationToken);
        return result.IsSuccess ? NoContent() : WorkItemErrorResult.Map(this, result.Error);
    }

    [HttpPost("api/v1/work-items/{workItemId:guid}/labels/{labelId:guid}")]
    [ValidateAntiforgery]
    public Task<IActionResult> Assign(Guid workItemId, Guid labelId, CancellationToken cancellationToken) =>
        SetAssignmentAsync(workItemId, labelId, true, cancellationToken);

    [HttpDelete("api/v1/work-items/{workItemId:guid}/labels/{labelId:guid}")]
    [ValidateAntiforgery]
    public Task<IActionResult> Remove(Guid workItemId, Guid labelId, CancellationToken cancellationToken) =>
        SetAssignmentAsync(workItemId, labelId, false, cancellationToken);

    private async Task<IActionResult> SetAssignmentAsync(Guid workItemId, Guid labelId, bool assigned, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId)) return Unauthorized();
        if (await AuthorizeAsync(await findWorkItemScope.HandleAsync(workItemId, cancellationToken), Permissions.LabelAssign, cancellationToken) is { } error)
            return error;
        var result = await handlers.SetAssignmentAsync(workItemId, labelId, userId, assigned, cancellationToken);
        return result.IsSuccess ? NoContent() : WorkItemErrorResult.Map(this, result.Error);
    }

    private Task<IActionResult?> AuthorizeAsync(WorkItemScope? scope, string? permission, CancellationToken cancellationToken) =>
        scope is null ? Task.FromResult<IActionResult?>(NotFound()) :
            WorkItemAuthorizationResult.RequireProjectAccessAsync(
                this, authorizationService, scope.ProjectId, scope.OrganizationId, permission, cancellationToken);

    private bool TryGetUser(out Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
