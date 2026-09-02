using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Modules.Organizations.Presentation;

internal static class OrganizationAuthorizationResult
{
    public static async Task<IActionResult?> RequirePermissionAsync(
        ControllerBase controller,
        IAuthorizationService authorizationService,
        Guid organizationId,
        string permission,
        Guid? targetUserId,
        CancellationToken cancellationToken)
    {
        var resource = new OrganizationAuthorizationResource(
            organizationId,
            targetUserId,
            cancellationToken);

        var result = await authorizationService.AuthorizeAsync(
            controller.User,
            resource,
            permission);

        if (result.Succeeded)
        {
            return null;
        }

        // Cross-tenant callers must not learn whether the resource exists.
        return resource.HasActiveMembership
            ? controller.Forbid()
            : controller.NotFound();
    }
}
