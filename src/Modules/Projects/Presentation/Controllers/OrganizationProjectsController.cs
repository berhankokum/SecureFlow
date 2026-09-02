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
[Route("api/v1/organizations/{organizationId:guid}/projects")]
[Authorize]
public sealed class OrganizationProjectsController(
    IAuthorizationService authorizationService,
    CreateProjectHandler createHandler,
    ListProjectsHandler listHandler,
    IValidator<CreateProjectRequest> createValidator) : ControllerBase
{
    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(
        Guid organizationId,
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await ProjectAuthorizationResult.RequireOrganizationPermissionAsync(
                this, authorizationService, organizationId, Permissions.ProjectCreate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(createValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await createHandler.HandleAsync(
            new CreateProjectCommand(
                organizationId,
                request.Key,
                request.Name,
                request.Description,
                currentUserId),
            cancellationToken);

        return result.IsSuccess
            ? Created($"/api/v1/projects/{result.Value.Id}", ProjectSummaryResponse.From(result.Value))
            : ProjectErrorResult.Map(this, result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await ProjectAuthorizationResult.RequireOrganizationPermissionAsync(
                this, authorizationService, organizationId, Permissions.MemberView, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await listHandler.HandleAsync(organizationId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(ProjectSummaryResponse.From).ToArray())
            : ProjectErrorResult.Map(this, result.Error);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
