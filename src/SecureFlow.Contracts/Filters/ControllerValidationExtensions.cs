using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SecureFlow.Contracts.Filters;

/// <summary>
/// SecureFlow-Final.md §30: request validation FluentValidation ile yapılır.
/// Hata şekli §17.16'daki "errors: { field: [messages] }" formatına (ValidationProblemDetails) uyar.
/// </summary>
public static class ControllerValidationExtensions
{
    public static async Task<IActionResult?> ValidateAsync<T>(
        this ControllerBase controller,
        IValidator<T> validator,
        T request)
    {
        var result = await validator.ValidateAsync(request);

        if (result.IsValid)
        {
            return null;
        }

        foreach (var error in result.Errors)
        {
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return controller.ValidationProblem(controller.ModelState);
    }
}
