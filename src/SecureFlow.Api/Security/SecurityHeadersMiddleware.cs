using Microsoft.Extensions.Options;

namespace SecureFlow.Api.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var protectedPath = context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/hubs");
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy = "default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
            if (protectedPath)
            {
                headers.CacheControl = "no-store";
                headers.Pragma = "no-cache";
            }
            return Task.CompletedTask;
        });

        // CORS alone does not restrict WebSockets, nor reject cross-origin writes.
        // Missing Origin is normal for native clients and never replaces auth/CSRF.
        if (protectedPath && context.Request.Headers.TryGetValue("Origin", out var origins))
        {
            var origin = origins.Count == 1 ? origins[0] : null;
            var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            if (origin is null || !SecurityOptions.IsValidOrigin(origin)
                || (!string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase)
                    && !options.Value.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Origin is not allowed.")
                    .ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
