using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Collaboration.Presentation;

internal static class CollaborationErrorResult
{
    public static IActionResult Map(ControllerBase controller, Error error)
    {
        var statusCode = error.Code switch
        {
            "Collaboration.NotFound" => StatusCodes.Status404NotFound,
            "Collaboration.Forbidden" => StatusCodes.Status403Forbidden,
            "Collaboration.ProjectArchived"
                or "Collaboration.WorkItemArchived"
                or "Collaboration.CommentDeleted" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(title: error.Message, statusCode: statusCode);
    }
}
