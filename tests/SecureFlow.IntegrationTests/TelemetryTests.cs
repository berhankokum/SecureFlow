using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureFlow.Api.Observability;
using SecureFlow.Contracts.Observability;
using Serilog;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class TelemetryTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Server_errors_expose_only_the_same_trace_reference_even_when_sampling_is_disabled()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Telemetry:TraceRatio", "0");
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(TelemetryFailureController).Assembly));
        });
        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/__test/observability/fail");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SensitiveException", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), json.RootElement.GetProperty("traceId").GetString());
    }

    private sealed class ExportCapture : HttpMessageHandler
    {
        public ConcurrentBag<string> Paths { get; } = [];
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));
    }

    [Fact]
    public void Enabled_otlp_exports_traces_and_metrics_through_http_protobuf_without_a_real_service()
    {
        using var capture = new ExportCapture();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:ExportEnabled"] = "true",
            ["Telemetry:Endpoint"] = "https://collector.invalid",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowTelemetry(configuration);
        services.ConfigureAll<OtlpExporterOptions>(o => o.HttpClientFactory = () => new HttpClient(capture, disposeHandler: false));
        using var provider = services.BuildServiceProvider();
        var traces = provider.GetRequiredService<TracerProvider>();
        var metrics = provider.GetRequiredService<MeterProvider>();
        using (FlowTelemetry.StartActivity("http.request")) { }
        FlowTelemetry.Requests.Add(1);
        Assert.True(traces.ForceFlush());
        Assert.True(metrics.ForceFlush());
        Assert.Contains("/v1/traces", capture.Paths);
        Assert.Contains("/v1/metrics", capture.Paths);
    }

    private sealed class FormattingSink(TextWriter writer) : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) => new SafeLogFormatter().Format(logEvent, writer);
    }
    [Fact]
    public async Task Trace_headers_problem_details_and_route_only_spans_are_correlated()
    {
        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FlowTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using var client = factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Trace-Id", "attacker-secret");
        client.DefaultRequestHeaders.Add("tracestate", "secret=SensitiveToken");
        var response = await client.GetAsync("/api/v1/invitations/SensitiveToken?password=SensitivePassword");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var id = response.Headers.GetValues("X-Trace-Id").Single();
        Assert.Matches("^[a-f0-9]{32}$", id);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, body.GetProperty("traceId").GetString());
        var span = Assert.Single(spans, s => s.DisplayName == "http.request" && s.TraceId.ToString() == id);
        Assert.Contains("{token}", span.GetTagItem("http.route")!.ToString());
        Assert.Null(span.TraceStateString);
        Assert.DoesNotContain("Sensitive", JsonSerializer.Serialize(span.TagObjects));
        Assert.False(factory.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value.ExportEnabled);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Http_and_job_metrics_use_only_bounded_labels()
    {
        var measurements = new ConcurrentBag<(string Name, string Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => { if (instrument.Meter.Name == FlowTelemetry.Name) meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => measurements.Add((instrument.Name, string.Join(",", tags.ToArray().Select(t => t.Key + "=" + t.Value)))));
        listener.Start();
        using (var job = FlowTelemetry.StartJob("invitation_cleanup")) job.Complete();
        using (FlowTelemetry.StartJob("attacker-sensitive-job")) { }
        using var client = factory.CreateSecureClient();
        await client.GetAsync("/unmatched-sensitive-path?secret=value");
        Assert.Contains(measurements, m => m.Name == "secureflow.jobs.completed" && m.Tags.Contains("outcome=success"));
        Assert.Contains(measurements, m => m.Name == "secureflow.jobs.completed" && m.Tags.Contains("operation=unknown") && m.Tags.Contains("outcome=failure"));
        Assert.Contains(measurements, m => m.Name == "secureflow.http.requests" && m.Tags.Contains("http.route=unmatched"));
        Assert.All(measurements, m => Assert.DoesNotContain("sensitive", m.Tags));
    }

    [Fact]
    public void Json_log_formatter_drops_secrets_and_preserves_trace_correlation()
    {
        using var output = new StringWriter();
        using var logger = new LoggerConfiguration().WriteTo.Sink(new FormattingSink(output)).CreateLogger();
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        logger.ForContext("SourceContext", "SecureFlow.Test").Warning(new Exception("Sensitive exception"), "Failure for {Email}", "Sensitive email");
        logger.ForContext("SourceContext", "Microsoft.EntityFrameworkCore.Database.Command").Warning("SQL Sensitive literal");
        var text = output.ToString();
        Assert.DoesNotContain("Sensitive", text);
        Assert.Contains(activity.TraceId.ToString(), text);
        Assert.Contains("exceptionType", text);
    }

    [Theory]
    [InlineData("https://collector.example.com", true)]
    [InlineData("http://127.0.0.1:4318", true)]
    [InlineData("http://collector.example.com", false)]
    [InlineData("https://user:secret@collector.example.com", false)]
    [InlineData("https://collector.example.com?token=secret", false)]
    [InlineData("", false)]
    public void Export_requires_an_explicit_safe_endpoint(string endpoint, bool expected)
    {
        Assert.Equal(expected, new TelemetryOptions { ExportEnabled = true, Endpoint = endpoint }.IsValid());
        Assert.False(new TelemetryOptions { TraceRatio = double.NaN }.IsValid());
        Assert.True(new TelemetryOptions().IsValid());
    }
}

[ApiController]
[Route("__test/observability/fail")]
public sealed class TelemetryFailureController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("SensitiveException password=secret");
}
