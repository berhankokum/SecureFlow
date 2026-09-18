using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SecureFlow.Api.Security;

public static class SecurityServices
{
    public static IServiceCollection AddFlowSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName))
            .Validate(options => options.IsValid(), "Invalid Security origins or rate limits.")
            .ValidateOnStart();
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = false;
            options.Preload = false;
        });
        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<SecurityOptions>>((options, security) =>
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(security.Value.AllowedOrigins)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        .WithHeaders("Content-Type", "X-CSRF-TOKEN", "If-Match", "X-SignalR-User-Agent")
                        .WithExposedHeaders("X-Trace-Id", "ETag", "Retry-After")
                        .AllowCredentials();
                }));
        services.AddRateLimiter(options =>
        {
            foreach (var name in new[] { "auth", "auth-email" })
            {
                options.AddPolicy(name, context =>
                {
                    var settings = context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value;
                    // Route values are server-defined; never partition by attacker-supplied email.
                    var action = context.Request.RouteValues["action"]?.ToString() ?? "unknown";
                    var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter($"{address}:{action}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = name == "auth-email" ? settings.EmailPermitLimit : settings.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        AutoReplenishment = true,
                        QueueLimit = 0,
                    });
                });
            }
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Çok fazla istek gönderdiniz. Lütfen bir süre sonra tekrar deneyin.",
                };
                problem.Extensions["traceId"] = context.HttpContext.Items["FlowTraceId"];
                await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
            };
        });
        return services;
    }
}
