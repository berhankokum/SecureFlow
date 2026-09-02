using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Modules.Projects.Presentation;

internal static class ProjectAuthorizationResult
{
    public static async Task<IActionResult?> RequireOrganizationPermissionAsync(
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

    public static async Task<IActionResult?> RequireProjectAccessAsync(
        ControllerBase controller,
        IAuthorizationService authorizationService,
        Guid projectId,
        Guid organizationId,
        string? permission,
        CancellationToken cancellationToken)
    {
        var resource = new ProjectAuthorizationResource(projectId, organizationId, cancellationToken);
        var result = permission is null
            ? await authorizationService.AuthorizeAsync(
                controller.User,
                resource,
                new ProjectAccessRequirement())
            : await authorizationService.AuthorizeAsync(controller.User, resource, permission);

        if (result.Succeeded)
        {
            return null;
        }

        return resource.HasActiveOrganizationMembership && resource.HasProjectAccess
            ? controller.Forbid()
            : controller.NotFound();
    }
}
