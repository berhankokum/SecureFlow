using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Modules.Organizations.Application.Dashboard;

namespace SecureFlow.Modules.Organizations.Presentation.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/organizations/{organizationId:guid}/dashboard")]
public sealed class DashboardController(IDashboardReader reader) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken cancellationToken, [FromQuery] int myTasksPage = 1)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (myTasksPage is < 1 or > 100000) return Problem(statusCode: 400, title: "Geçersiz dashboard sayfası.");
        var result = await reader.ReadAsync(organizationId, userId, myTasksPage, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
