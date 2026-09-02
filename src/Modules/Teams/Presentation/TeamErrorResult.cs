using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Teams.Presentation;

internal static class TeamErrorResult
{
    public static IActionResult Map(ControllerBase controller, Error error)
    {
        var statusCode = error.Code switch
        {
            "Teams.NotFound" or "Teams.MemberNotFound" => StatusCodes.Status404NotFound,
            "Teams.OrganizationArchived"
                or "Teams.Archived"
                or "Teams.MemberMustBelongToOrganization"
                or "Teams.ExistingMember" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(title: error.Message, statusCode: statusCode);
    }
}
