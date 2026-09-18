using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using SecureFlow.Contracts.Observability;

namespace SecureFlow.Api.Observability;

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using var activity = FlowTelemetry.StartActivity("http.request", ActivityKind.Server);
        var traceId = Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
        context.Items["FlowTraceId"] = traceId;
        context.Response.OnStarting(() => { context.Response.Headers["X-Trace-Id"] = traceId; return Task.CompletedTask; });
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var method = context.Request.Method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" ? context.Request.Method : "OTHER";
        var started = Stopwatch.GetTimestamp();
        var status = 500;
        try { await next(context); status = context.Response.StatusCode; }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            var tags = new TagList { { "http.request.method", method }, { "http.route", route }, { "http.response.status_code", status } };
            FlowTelemetry.Requests.Add(1, tags);
            FlowTelemetry.RequestDuration.Record(elapsed, tags);
            activity?.SetTag("http.request.method", method);
            activity?.SetTag("http.route", route);
            activity?.SetTag("http.response.status_code", status);
            activity?.SetStatus(status >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            logger.LogInformation("HTTP request completed for {RouteTemplate}: {StatusCode} in {ElapsedSeconds}s", route, status, elapsed);
        }
    }
}
