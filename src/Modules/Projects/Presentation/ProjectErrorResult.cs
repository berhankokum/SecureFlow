using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Projects.Presentation;

internal static class ProjectErrorResult
{
    public static IActionResult Map(ControllerBase controller, Error error)
    {
        var statusCode = error.Code switch
        {
            "Projects.NotFound" or "Projects.MemberNotFound" => StatusCodes.Status404NotFound,
            "Projects.DuplicateKey"
                or "Projects.OrganizationArchived"
                or "Projects.Archived"
                or "Projects.MemberMustBelongToOrganization"
                or "Projects.ExistingMember"
                or "Projects.ConcurrentMembership" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(title: error.Message, statusCode: statusCode);
    }
}
