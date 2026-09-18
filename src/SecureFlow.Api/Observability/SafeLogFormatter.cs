using System.Text.Json;
using Serilog.Events;
using Serilog.Formatting;

namespace SecureFlow.Api.Observability;

public sealed class SafeLogFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var source = logEvent.Properties.GetValueOrDefault("SourceContext") is ScalarValue { Value: string s } ? s : "";
        var own = source.StartsWith("SecureFlow.", StringComparison.Ordinal);
        var values = new Dictionary<string, object?>
        {
            ["timestamp"] = logEvent.Timestamp,
            ["level"] = logEvent.Level.ToString(),
            ["source"] = source.Length <= 200 ? source : "other",
            ["event"] = own ? logEvent.MessageTemplate.Text : "Framework or startup event",
            ["traceId"] = logEvent.TraceId?.ToString(),
            ["spanId"] = logEvent.SpanId?.ToString(),
            ["exceptionType"] = logEvent.Exception?.GetType().Name,
        };
        // No arbitrary structured property or rendered exception ever reaches a sink.
        foreach (var name in new[] { "StatusCode", "ElapsedSeconds" })
            if (logEvent.Properties.GetValueOrDefault(name) is ScalarValue { Value: int or double } value) values[name] = value.Value;
        if (own && logEvent.Properties.GetValueOrDefault("RouteTemplate") is ScalarValue { Value: string route })
            values["route"] = route;
        output.WriteLine(JsonSerializer.Serialize(values));
    }
}
