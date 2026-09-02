using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Teams.Application;
using SecureFlow.Modules.Teams.Presentation.Contracts;

namespace SecureFlow.Modules.Teams.Presentation.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/teams")]
[Authorize]
public sealed class OrganizationTeamsController(
    IAuthorizationService authorizationService,
    CreateTeamHandler createTeamHandler,
    ListTeamsHandler listTeamsHandler,
    IValidator<CreateTeamRequest> createValidator) : ControllerBase
{
    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(
        Guid organizationId,
        CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return Unauthorized();
        }

        if (await TeamAuthorizationResult.RequirePermissionAsync(
                this, authorizationService, organizationId, Permissions.TeamCreate, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(createValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await createTeamHandler.HandleAsync(
            new CreateTeamCommand(organizationId, request.Name, request.Description),
            cancellationToken);

        return result.IsSuccess
            ? Created($"/api/v1/teams/{result.Value.Id}", TeamSummaryResponse.From(result.Value))
            : TeamErrorResult.Map(this, result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken cancellationToken)
    {
        if (await TeamAuthorizationResult.RequirePermissionAsync(
                this, authorizationService, organizationId, Permissions.MemberView, cancellationToken)
            is { } authorizationError)
        {
            return authorizationError;
        }

        var teams = await listTeamsHandler.HandleAsync(organizationId, cancellationToken);
        return Ok(teams.Select(TeamSummaryResponse.From).ToArray());
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
