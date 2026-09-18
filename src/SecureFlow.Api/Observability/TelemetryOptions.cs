namespace SecureFlow.Api.Observability;

public sealed class TelemetryOptions
{
    public bool ExportEnabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string Headers { get; set; } = "";
    public double TraceRatio { get; set; } = 1;
    public bool IsValid() => double.IsFinite(TraceRatio) && TraceRatio is >= 0 and <= 1
        && (!ExportEnabled || Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
        && !Headers.Contains('\r') && !Headers.Contains('\n');
}
