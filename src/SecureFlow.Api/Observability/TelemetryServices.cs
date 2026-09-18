using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SecureFlow.Contracts.Observability;
using Serilog.Core;
using Serilog.Events;

namespace SecureFlow.Api.Observability;

public static class TelemetryServices
{
    public static IServiceCollection AddFlowTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Telemetry").Get<TelemetryOptions>() ?? new();
        if (!settings.IsValid()) throw new InvalidOperationException("Invalid Telemetry settings. Check endpoint, headers and trace ratio.");
        services.AddSingleton(Options.Create(settings));
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService("SecureFlow.Api"))
            .WithTracing(t =>
            {
                t.AddSource(FlowTelemetry.Name).SetSampler(new TraceIdRatioBasedSampler(settings.TraceRatio));
                if (settings.ExportEnabled) t.AddOtlpExporter(o => Configure(o, settings, "traces"));
            })
            .WithMetrics(m =>
            {
                m.AddMeter(FlowTelemetry.Name);
                if (settings.ExportEnabled) m.AddOtlpExporter(o => Configure(o, settings, "metrics"));
            });
        if (settings.ExportEnabled) services.AddSingleton<ILogEventSink>(_ => new SafeOtlpLogSink(settings));
        return services;
    }

    internal static void Configure(OtlpExporterOptions options, TelemetryOptions settings, string signal)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri(settings.Endpoint.TrimEnd('/') + "/v1/" + signal);
        options.Headers = settings.Headers;
        options.TimeoutMilliseconds = 3000;
    }
}

public sealed class SafeOtlpLogSink : ILogEventSink, IDisposable
{
    private readonly ILoggerFactory factory;
    private readonly ILogger logger;
    public SafeOtlpLogSink(TelemetryOptions settings)
    {
        factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SecureFlow.Api"));
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = false;
            o.AddOtlpExporter(exporter => TelemetryServices.Configure(exporter, settings, "logs"));
        }));
        logger = factory.CreateLogger("SecureFlow.SafeLog");
    }
    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        new SafeLogFormatter().Format(logEvent, writer);
        logger.Log((LogLevel)Math.Clamp((int)logEvent.Level, 0, 5), "{SafeEvent}", writer.ToString());
    }
    public void Dispose() => factory.Dispose();
}
