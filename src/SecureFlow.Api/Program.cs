using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Api.HealthChecks;
using SecureFlow.Api.Persistence;
using SecureFlow.Modules.Identity;
using SecureFlow.Modules.Organizations;
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

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(postgresConnectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Postgres is required. Load the project .env file before starting SecureFlow.Api.");
    }

    builder.Services.AddDbContext<SecureFlowDbContext>(options =>
        options.UseNpgsql(postgresConnectionString));

    builder.Services.AddIdentityModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddOrganizationsModule<SecureFlowDbContext>();
    builder.Services.AddAuthorization();

    builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

    builder.Services
        .AddHealthChecks()
        .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

    var app = builder.Build();

    // Reverse proxy'nin bildirdiği gerçek scheme/istemci bilgisi HTTPS ve cookie
    // değerlendirmesinden önce uygulanır. Güvenilen proxy listesi production
    // deployment konfigürasyonunda açıkça sınırlandırılır.
    app.UseForwardedHeaders();

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
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
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
