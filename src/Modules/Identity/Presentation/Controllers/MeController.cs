using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Identity.Presentation.Contracts;

namespace SecureFlow.Modules.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new MeResponse(user.Id, user.Email!, user.EmailConfirmed));
    }
}
