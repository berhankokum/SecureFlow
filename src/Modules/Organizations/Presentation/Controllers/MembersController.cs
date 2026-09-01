using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Organizations.Application.Members;
using SecureFlow.Modules.Organizations.Presentation.Contracts;

namespace SecureFlow.Modules.Organizations.Presentation.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/members")]
[Authorize]
public sealed class MembersController(
    IAuthorizationService authorizationService,
    ListMembersHandler listMembersHandler,
    ChangeMemberRoleHandler changeMemberRoleHandler,
    RemoveMemberHandler removeMemberHandler,
    IValidator<ChangeMemberRoleRequest> changeRoleValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await OrganizationAuthorizationResult.RequirePermissionAsync(
                this,
                authorizationService,
                organizationId,
                Permissions.MemberView,
                targetUserId: null,
                cancellationToken) is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await listMembersHandler.HandleAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.Select(OrganizationMemberResponse.From).ToArray())
            : OrganizationErrorResult.Map(this, result.Error);
    }

    [HttpPatch("{userId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> ChangeRole(
        Guid organizationId,
        Guid userId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await OrganizationAuthorizationResult.RequirePermissionAsync(
                this,
                authorizationService,
                organizationId,
                Permissions.MemberRoleChange,
                userId,
                cancellationToken) is { } authorizationError)
        {
            return authorizationError;
        }

        if (await this.ValidateAsync(changeRoleValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await changeMemberRoleHandler.HandleAsync(
            new ChangeMemberRoleCommand(
                organizationId,
                currentUserId,
                userId,
                OrganizationRoleParser.Parse(request.Role)),
            cancellationToken);

        return result.IsSuccess ? NoContent() : OrganizationErrorResult.Map(this, result.Error);
    }

    [HttpDelete("{userId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Remove(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await OrganizationAuthorizationResult.RequirePermissionAsync(
                this,
                authorizationService,
                organizationId,
                Permissions.MemberRemove,
                userId,
                cancellationToken) is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await removeMemberHandler.HandleAsync(
            new RemoveMemberCommand(organizationId, currentUserId, userId),
            cancellationToken);

        return result.IsSuccess ? NoContent() : OrganizationErrorResult.Map(this, result.Error);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
