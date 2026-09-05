using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Files.Presentation;

internal static class FileErrorResult
{
    public static IActionResult Map(ControllerBase controller, Error error)
    {
        var statusCode = error.Code switch
        {
            "Files.NotFound" => StatusCodes.Status404NotFound,
            "Files.Forbidden" => StatusCodes.Status403Forbidden,
            "Files.ProjectArchived" or "Files.WorkItemArchived" => StatusCodes.Status409Conflict,
            "Files.TooLarge" => StatusCodes.Status413PayloadTooLarge,
            "Files.StorageUnavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };
        return controller.Problem(title: error.Message, statusCode: statusCode);
    }
}
