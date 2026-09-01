using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace SecureFlow.Contracts.Filters;

/// <summary>
/// SecureFlow-Final.md §7: state-changing request'lerde CSRF koruması uygulanır.
/// Web API senaryosunda ASP.NET Core'un IAntiforgery servisini doğrudan çağırır
/// (bkz. learn.microsoft.com/aspnet/core/security/anti-request-forgery, "JavaScript, AJAX, and SPAs").
/// Frontend, GET /api/v1/antiforgery/token ile aldığı değeri X-CSRF-TOKEN header'ında geri gönderir.
/// </summary>
public sealed class ValidateAntiforgeryAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "CSRF doğrulaması başarısız.",
                Status = StatusCodes.Status400BadRequest,
            })
            {
                StatusCode = StatusCodes.Status400BadRequest,
            };
            return;
        }

        await next();
    }
}
