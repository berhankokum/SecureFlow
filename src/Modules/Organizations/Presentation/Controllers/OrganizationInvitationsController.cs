using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Organizations.Application.Invitations;
using SecureFlow.Modules.Organizations.Presentation.Contracts;

namespace SecureFlow.Modules.Organizations.Presentation.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/invitations")]
[Authorize]
[EnableRateLimiting("invitations")]
public sealed class OrganizationInvitationsController(
    CreateInvitationHandler createInvitationHandler,
    ListInvitationsHandler listInvitationsHandler,
    RevokeInvitationHandler revokeInvitationHandler,
    IValidator<CreateInvitationRequest> createInvitationValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await listInvitationsHandler.HandleAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.Select(OrganizationInvitationResponse.From).ToArray())
            : OrganizationErrorResult.Map(this, result.Error);
    }

    [HttpPost]
    [ValidateAntiforgery]
    public async Task<IActionResult> Create(
        Guid organizationId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (await this.ValidateAsync(createInvitationValidator, request) is { } validationError)
        {
            return validationError;
        }

        var result = await createInvitationHandler.HandleAsync(
            new CreateInvitationCommand(
                organizationId,
                currentUserId,
                request.Email,
                OrganizationRoleParser.Parse(request.Role)),
            cancellationToken);

        return result.IsSuccess
            ? Created(
                $"/api/v1/organizations/{organizationId}/invitations/{result.Value.Id}",
                OrganizationInvitationResponse.From(result.Value))
            : OrganizationErrorResult.Map(this, result.Error);
    }

    [HttpDelete("{invitationId:guid}")]
    [ValidateAntiforgery]
    public async Task<IActionResult> Revoke(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await revokeInvitationHandler.HandleAsync(
            new RevokeInvitationCommand(organizationId, invitationId, currentUserId),
            cancellationToken);

        return result.IsSuccess ? NoContent() : OrganizationErrorResult.Map(this, result.Error);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
