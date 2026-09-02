using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Modules.Teams.Presentation;

internal static class TeamAuthorizationResult
{
    public static async Task<IActionResult?> RequirePermissionAsync(
        ControllerBase controller,
        IAuthorizationService authorizationService,
        Guid organizationId,
        string permission,
        CancellationToken cancellationToken)
    {
        var resource = new OrganizationAuthorizationResource(
            organizationId,
            cancellationToken: cancellationToken);

        var result = await authorizationService.AuthorizeAsync(controller.User, resource, permission);
        if (result.Succeeded)
        {
            return null;
        }

        return resource.HasActiveMembership ? controller.Forbid() : controller.NotFound();
    }
}
