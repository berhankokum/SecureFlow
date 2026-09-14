using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.WorkItems.Application;

namespace SecureFlow.Modules.WorkItems.Presentation;

public sealed class WorkItemConcurrencyExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateConcurrencyException) return;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = 412,
            Title = WorkItemPrecondition.Stale.Message,
        })
        { StatusCode = 412 };
        context.ExceptionHandled = true;
    }
}
