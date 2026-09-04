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
[Route("api/v1/projects/{projectId:guid}/members")]
[Authorize]
public sealed class ProjectMembersController(
    IAuthorizationService authorizationService,
    FindProjectOrganizationHandler findOrganizationHandler,
    GetProjectHandler getProjectHandler,
    AddProjectMemberHandler addHandler,
    ChangeProjectMemberRoleHandler changeRoleHandler,
    RemoveProjectMemberHandler removeHandler,
    IValidator<AddProjectMemberRequest> addValidator,
    IValidator<ChangeProjectMemberRoleRequest> changeValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(projectId, permission: null, cancellationToken) is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await getProjectHandler.HandleAsync(projectId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Members.Select(ProjectMemberResponse.From).ToArray())
            : ProjectErrorResult.Map(this, result.Error);
    }

    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Add(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(projectId, Permissions.ProjectUpdate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(addValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await addHandler.HandleAsync(
            new AddProjectMemberCommand(projectId, request.UserId, ProjectRoleParser.Parse(request.Role)!.Value),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ProjectErrorResult.Map(this, result.Error);
    }

    [HttpPatch("{userId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> ChangeRole(
        Guid projectId,
        Guid userId,
        ChangeProjectMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(projectId, Permissions.ProjectUpdate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(changeValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await changeRoleHandler.HandleAsync(
            new ChangeProjectMemberRoleCommand(projectId, userId, ProjectRoleParser.Parse(request.Role)!.Value),
            cancellationToken);
        return result.IsSuccess ? NoContent() : ProjectErrorResult.Map(this, result.Error);
    }

    [HttpDelete("{userId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Remove(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await AuthorizeAsync(projectId, Permissions.ProjectUpdate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await removeHandler.HandleAsync(projectId, userId, currentUserId, cancellationToken);
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
