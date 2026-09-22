using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using SecureFlow.Api.Configuration;
using SecureFlow.Api.Caching;
using SecureFlow.Api.HealthChecks;
using SecureFlow.Api.Persistence;
using SecureFlow.Api.Realtime;
using SecureFlow.Contracts.Realtime;
using SecureFlow.Modules.Identity;
using SecureFlow.Modules.Organizations;
using SecureFlow.Modules.Teams;
using SecureFlow.Modules.Projects;
using SecureFlow.Modules.WorkItems;
using SecureFlow.Modules.Collaboration;
using SecureFlow.Modules.Notifications;
using SecureFlow.Modules.Files;
using SecureFlow.Modules.Outbox;
using SecureFlow.Modules.Audit;
using SecureFlow.Contracts.Audit;
using SecureFlow.Contracts.Outbox;
using Serilog;
using SecureFlow.Api.Observability;
using SecureFlow.Api.Security;

if (args is ["--healthcheck"])
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        client.DefaultRequestHeaders.Host = Environment.GetEnvironmentVariable("AllowedHosts") ?? "localhost";
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/live");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch { Environment.ExitCode = 1; }
    return;
}

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new SafeLogFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // IDE'ler repository root .env dosyasını launchSettings ile otomatik yüklemez.
    // Debug build'lerinde ve Development ortamında yalnızca eksik değerler yerel
    // .env'den tamamlanır; gerçek environment/CLI/secret değerleri ezilmez.
#if DEBUG
    DevelopmentEnvironmentFile.AddMissingValues(builder.Configuration, builder.Environment.ContentRootPath);
#else
    if (builder.Environment.IsDevelopment())
    {
        DevelopmentEnvironmentFile.AddMissingValues(builder.Configuration, builder.Environment.ContentRootPath);
    }
#endif

    builder.AddProductionHosting();

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(new SafeLogFormatter()));

    builder.Services.AddFlowTelemetry(builder.Configuration);
    builder.Services.AddFlowSecurity(builder.Configuration);
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IAuditRequestContext, AuditRequestContext>();
    builder.Services.AddScoped<SecureFlow.Modules.Organizations.Application.Dashboard.IDashboardReader, OrganizationDashboardReader>();
    builder.Services.AddSecureFlowCaching(builder.Configuration);
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.Items["FlowTraceId"]
            ?? System.Diagnostics.Activity.Current?.TraceId.ToString());
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = false;
        options.MaximumReceiveMessageSize = 8 * 1024;
        options.MaximumParallelInvocationsPerClient = 1;
    });
    builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();
    builder.Services.AddScoped<IRealtimePublicationBuffer, RealtimePublicationBuffer>();
    builder.Services.AddScoped<IOutboxEventDispatcher, SecureFlowOutboxDispatcher>();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(postgresConnectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Postgres is required. Create the repository-root .env file or provide the value as an environment variable/secret.");
    }

    builder.Services.AddDbContext<SecureFlowDbContext>(options =>
        options.UseNpgsql(postgresConnectionString));

    builder.Services.AddIdentityModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddOrganizationsModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddTeamsModule<SecureFlowDbContext>();
    builder.Services.AddProjectsModule<SecureFlowDbContext>();
    builder.Services.AddWorkItemsModule<SecureFlowDbContext>();
    builder.Services.AddCollaborationModule<SecureFlowDbContext>();
    builder.Services.AddNotificationsModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddFilesModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddOutboxModule<SecureFlowDbContext>(builder.Configuration);
    builder.Services.AddAuditModule<SecureFlowDbContext>();
    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("invitations", context =>
        {
            var partitionKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 20,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1),
                });
        });
        options.AddPolicy("file-uploads", context =>
        {
            var partitionKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 10,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1),
                });
        });
    });

    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        // The existing local Vite proxy talks HTTP to the Development API.
        // Antiforgery rejects token creation on HTTP when SecurePolicy is Always.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
    });

    builder.Services
        .AddHealthChecks()
        .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);

    var app = builder.Build();

    // Reverse proxy'nin bildirdiği gerçek scheme/istemci bilgisi HTTPS ve cookie
    // değerlendirmesinden önce uygulanır. Güvenilen proxy listesi production
    // deployment konfigürasyonunda açıkça sınırlandırılır.
    app.UseForwardedHeaders();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseRouting();
    app.UseMiddleware<RequestTelemetryMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // Non-sensitive process health is reachable inside the private container network.
    app.UseWhen(context => context.Request.Path != "/health/live", branch => branch.UseHttpsRedirection());
    app.UseCors();

    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapControllers();
    app.MapHub<RealtimeHub>("/hubs/realtime").RequireAuthorization();

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

    // Readiness covers PostgreSQL and Object Storage; optional Redis health is reported separately.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.MapHealthChecks("/health/cache", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("cache")
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.ForContext("SourceContext", "SecureFlow.Startup").Fatal(ex, "SecureFlow.Api terminated unexpectedly during startup.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed for WebApplicationFactory&lt;Program&gt; in integration tests.</summary>
public partial class Program;
