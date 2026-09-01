using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Organizations.Presentation;

internal static class OrganizationErrorResult
{
    public static IActionResult Map(ControllerBase controller, Error error)
    {
        var statusCode = error.Code switch
        {
            "Organizations.NotFound" or "Memberships.NotFound" or "Invitations.NotFound" =>
                StatusCodes.Status404NotFound,
            "Organizations.Forbidden" or "Invitations.EmailMismatch" =>
                StatusCodes.Status403Forbidden,
            "Invitations.EmailDeliveryFailed" =>
                StatusCodes.Status503ServiceUnavailable,
            "Organizations.Archived"
                or "Memberships.LastOwner"
                or "Memberships.ConcurrentRequest"
                or "Invitations.ExistingMember"
                or "Invitations.Expired"
                or "Invitations.Revoked"
                or "Invitations.AlreadyUsed"
                or "Invitations.ConcurrentRequest" =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(title: error.Message, statusCode: statusCode);
    }
}
