using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Organizations.Application.Invitations;
using SecureFlow.Modules.Organizations.Presentation.Contracts;

namespace SecureFlow.Modules.Organizations.Presentation.Controllers;

[ApiController]
[Route("api/v1/invitations")]
[EnableRateLimiting("invitations")]
public sealed class InvitationsController(
    GetInvitationHandler getInvitationHandler,
    AcceptInvitationHandler acceptInvitationHandler) : ControllerBase
{
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string token, CancellationToken cancellationToken)
    {
        if (!IsValidTokenShape(token))
        {
            return NotFound();
        }

        var result = await getInvitationHandler.HandleAsync(token, cancellationToken);
        return result.IsSuccess
            ? Ok(OrganizationInvitationDetailsResponse.From(result.Value))
            : OrganizationErrorResult.Map(this, result.Error);
    }

    [HttpPost("{token}/accept")]
    [Authorize]
    [ValidateAntiforgery]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        if (!IsValidTokenShape(token))
        {
            return NotFound();
        }

        var result = await acceptInvitationHandler.HandleAsync(
            new AcceptInvitationCommand(token, currentUserId),
            cancellationToken);

        return result.IsSuccess
            ? Ok(AcceptedInvitationResponse.From(result.Value))
            : OrganizationErrorResult.Map(this, result.Error);
    }

    private static bool IsValidTokenShape(string token) =>
        token.Length is >= 32 and <= 128
        && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
