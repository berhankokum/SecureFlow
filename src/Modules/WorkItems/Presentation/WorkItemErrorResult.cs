using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Presentation;

internal static class WorkItemErrorResult
{
    public static IActionResult Map(ControllerBase controller, Error error)
    {
        var statusCode = error.Code switch
        {
            "WorkItems.StaleVersion" => 412,
            "WorkItems.PreconditionRequired" => 428,
            "WorkItems.NotFound" or "Labels.NotFound" => StatusCodes.Status404NotFound,
            "WorkItems.Forbidden" => StatusCodes.Status403Forbidden,
            "Labels.DuplicateName"
                or "WorkItems.ProjectArchived"
                or "WorkItems.Archived"
                or "WorkItems.NumberConflict"
                or "WorkItems.NumberExhausted"
                or "Workflow.MissingConfiguration"
                or "Workflow.InvalidWorkflowTransition" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(title: error.Message, statusCode: statusCode);
    }
}
