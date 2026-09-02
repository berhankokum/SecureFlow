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
[Route("api/v1/teams/{teamId:guid}")]
[Authorize]
public sealed class TeamsController(
    IAuthorizationService authorizationService,
    FindTeamOrganizationHandler findOrganizationHandler,
    GetTeamHandler getTeamHandler,
    UpdateTeamHandler updateTeamHandler,
    ArchiveTeamHandler archiveTeamHandler,
    AddTeamMemberHandler addMemberHandler,
    RemoveTeamMemberHandler removeMemberHandler,
    IValidator<UpdateTeamRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid teamId, CancellationToken cancellationToken)
    {
        var authorizationError = await AuthorizeTeamAsync(teamId, Permissions.MemberView, cancellationToken);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var result = await getTeamHandler.HandleAsync(teamId, cancellationToken);
        return result.IsSuccess
            ? Ok(TeamDetailsResponse.From(result.Value))
            : TeamErrorResult.Map(this, result.Error);
    }

    [HttpPatch]
    [ValidateAntiforgery]
    public async Task<IActionResult> Update(
        Guid teamId,
        UpdateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var authorizationError = await AuthorizeTeamAsync(teamId, Permissions.TeamUpdate, cancellationToken);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(updateValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await updateTeamHandler.HandleAsync(
            new UpdateTeamCommand(teamId, request.Name, request.Description),
            cancellationToken);
        return result.IsSuccess ? NoContent() : TeamErrorResult.Map(this, result.Error);
    }

    [HttpPost("archive")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Archive(Guid teamId, CancellationToken cancellationToken)
    {
        var authorizationError = await AuthorizeTeamAsync(teamId, Permissions.TeamArchive, cancellationToken);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var result = await archiveTeamHandler.HandleAsync(teamId, cancellationToken);
        return result.IsSuccess ? NoContent() : TeamErrorResult.Map(this, result.Error);
    }

    [HttpPost("members/{userId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> AddMember(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var authorizationError = await AuthorizeTeamAsync(
            teamId, Permissions.TeamMemberManage, cancellationToken);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var result = await addMemberHandler.HandleAsync(
            new AddTeamMemberCommand(teamId, userId, currentUserId),
            cancellationToken);
        return result.IsSuccess ? NoContent() : TeamErrorResult.Map(this, result.Error);
    }

    [HttpDelete("members/{userId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> RemoveMember(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var authorizationError = await AuthorizeTeamAsync(
            teamId, Permissions.TeamMemberManage, cancellationToken);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        var result = await removeMemberHandler.HandleAsync(teamId, userId, cancellationToken);
        return result.IsSuccess ? NoContent() : TeamErrorResult.Map(this, result.Error);
    }

    private async Task<IActionResult?> AuthorizeTeamAsync(
        Guid teamId,
        string permission,
        CancellationToken cancellationToken)
    {
        var organizationId = await findOrganizationHandler.HandleAsync(teamId, cancellationToken);
        if (organizationId is null)
        {
            return NotFound();
        }

        return await TeamAuthorizationResult.RequirePermissionAsync(
            this, authorizationService, organizationId.Value, permission, cancellationToken);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
