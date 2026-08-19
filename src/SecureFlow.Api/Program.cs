using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Api.HealthChecks;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Identity;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();

    builder.Services.AddDbContext<SecureFlowDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

    builder.Services.AddIdentityModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddAuthorization();

    builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

    builder.Services
        .AddHealthChecks()
        .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    else
    {
        app.UseExceptionHandler();
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapControllers();

    // SecureFlow-Final.md §7: SPA, X-CSRF-TOKEN header'ında geri göndereceği token'ı buradan alır.
    app.MapGet("/api/v1/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
        });
        return Results.NoContent();
    }).AllowAnonymous();

    // Liveness: process is up. No dependency checks.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Readiness: critical dependencies (PostgreSQL, and later Redis/Object Storage) must be reachable.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "SecureFlow.Api terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed for WebApplicationFactory&lt;Program&gt; in integration tests.</summary>
public partial class Program;
