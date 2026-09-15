using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Modules.Audit.Application;

namespace SecureFlow.Modules.Audit.Presentation;

[ApiController]
[Authorize]
[Route("api/v1/organizations/{organizationId:guid}/audit-logs")]
public sealed class AuditLogsController(AuditReadHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, [FromQuery] AuditQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await handler.HandleAsync(organizationId, userId, query, cancellationToken);
        if (result.IsFailure) return Problem(title: result.Error.Message, statusCode: result.Error.Code switch
        {
            "Audit.NotFound" => 404,
            "Audit.Forbidden" => 403,
            _ => 400,
        });
        Response.Headers.CacheControl = "no-store";
        return Ok(result.Value.Select(log => new
        {
            log.Id,
            log.OrganizationId,
            log.ActorUserId,
            log.Action,
            log.ResourceType,
            log.ResourceId,
            log.IpAddress,
            log.UserAgent,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string?>>(log.Metadata),
            log.Timestamp,
        }));
    }
}
