using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Modules.WorkItems.Application;
using SecureFlow.Modules.WorkItems.Presentation.Contracts;

namespace SecureFlow.Modules.WorkItems.Presentation.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/workflow")]
[Authorize]
public sealed class ProjectWorkflowController(WorkflowHandlers workflowHandlers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await workflowHandlers.GetAsync(projectId, currentUserId, cancellationToken);
        return result.IsSuccess
            ? Ok(WorkflowResponse.From(result.Value))
            : WorkItemErrorResult.Map(this, result.Error);
    }
}
