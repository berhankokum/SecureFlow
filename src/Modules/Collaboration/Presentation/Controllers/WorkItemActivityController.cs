using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Modules.Collaboration.Application;
using SecureFlow.Modules.Collaboration.Presentation.Contracts;

namespace SecureFlow.Modules.Collaboration.Presentation.Controllers;

[ApiController]
[Route("api/v1/work-items/{workItemId:guid}/activity")]
[Authorize]
public sealed class WorkItemActivityController(
    IAuthorizationService authorizationService,
    CollaborationScopeHandler scopeHandler,
    ActivityHandlers handlers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var scope = await scopeHandler.FindWorkItemAsync(workItemId, cancellationToken);
        if (scope is null)
        {
            return NotFound();
        }

        if (await CollaborationAuthorizationResult.RequireProjectAccessAsync(
                this,
                authorizationService,
                scope.ProjectId,
                scope.OrganizationId,
                permission: null,
                cancellationToken) is { } authorizationError)
        {
            return authorizationError;
        }

        var result = await handlers.ListAsync(workItemId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value.Select(ActivityResponse.From).ToArray())
            : CollaborationErrorResult.Map(this, result.Error);
    }
}
