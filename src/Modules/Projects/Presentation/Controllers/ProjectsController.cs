using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Projects.Application;
using SecureFlow.Modules.Projects.Presentation.Contracts;

namespace SecureFlow.Modules.Projects.Presentation.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}")]
[Authorize]
public sealed class ProjectsController(
    IAuthorizationService authorizationService,
    FindProjectOrganizationHandler findOrganizationHandler,
    GetProjectHandler getHandler,
    UpdateProjectHandler updateHandler,
    ArchiveProjectHandler archiveHandler,
    IValidator<UpdateProjectRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(projectId, permission: null, cancellationToken) is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await getHandler.HandleAsync(projectId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(ProjectDetailsResponse.From(result.Value))
            : ProjectErrorResult.Map(this, result.Error);
    }

    [HttpPatch]
    [ValidateAntiforgery]
    public async Task<IActionResult> Update(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(projectId, Permissions.ProjectUpdate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(updateValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await updateHandler.HandleAsync(
            new UpdateProjectCommand(projectId, request.Name, request.Description),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ProjectErrorResult.Map(this, result.Error);
    }

    [HttpPost("archive")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Archive(Guid projectId, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(projectId, Permissions.ProjectArchive, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await archiveHandler.HandleAsync(projectId, cancellationToken);
        return result.IsSuccess ? NoContent() : ProjectErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeAsync(
        Guid projectId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var organizationId = await findOrganizationHandler.HandleAsync(projectId, cancellationToken);
        if (organizationId is null)
        {
            return NotFound();
        }

        return await ProjectAuthorizationResult.RequireProjectAccessAsync(
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
