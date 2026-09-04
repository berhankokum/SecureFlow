using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Filters;
using SecureFlow.Modules.Notifications.Application;
using SecureFlow.Modules.Notifications.Presentation.Contracts;

namespace SecureFlow.Modules.Notifications.Presentation.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationHandlers handlers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        return Ok(NotificationListResponse.From(await handlers.ListAsync(currentUserId, cancellationToken)));
    }

    [HttpPost("{notificationId:guid}/read")]
    [ValidateAntiforgery]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await handlers.MarkReadAsync(notificationId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : Problem(title: result.Error.Message, statusCode: StatusCodes.Status404NotFound);
    }

    [HttpPost("read-all")]
    [ValidateAntiforgery]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        await handlers.MarkAllReadAsync(currentUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        return Ok(NotificationPreferenceResponse.From(
            await handlers.GetPreferenceAsync(currentUserId, cancellationToken)));
    }

    [HttpPut("preferences")]
    [ValidateAntiforgery]
    public async Task<IActionResult> UpdatePreferences(
        UpdateNotificationPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        return Ok(NotificationPreferenceResponse.From(
            await handlers.UpdatePreferenceAsync(currentUserId, request.EmailEnabled, cancellationToken)));
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
