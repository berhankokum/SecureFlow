using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SecureFlow.Contracts.Observability;

public static class FlowTelemetry
{
    public const string Name = "SecureFlow";
    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("secureflow.http.requests");
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("secureflow.http.duration", "s");
    public static readonly Counter<long> Cache = Meter.CreateCounter<long>("secureflow.cache.operations");
    public static readonly Counter<long> Jobs = Meter.CreateCounter<long>("secureflow.jobs.completed");
    public static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>("secureflow.jobs.duration", "s");
    public static JobScope StartJob(string operation) => new(operation);
    public static System.Diagnostics.Activity? StartActivity(string operation, ActivityKind kind = ActivityKind.Internal)
    {
        var activity = Source.StartActivity(operation, kind);
        if (activity is not null) activity.TraceStateString = null;
        return activity;
    }

    public sealed class JobScope : IDisposable
    {
        private readonly string operation;
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly System.Diagnostics.Activity? activity;
        private bool succeeded;
        internal JobScope(string operation)
        {
            this.operation = operation is "outbox" or "deadline_reminders" or "email_discovery" or "email_delivery" or "invitation_cleanup"
                ? operation : "unknown";
            activity = StartActivity("job." + this.operation);
        }
        public void Complete() => succeeded = true;
        public void Dispose()
        {
            var outcome = succeeded ? "success" : "failure";
            var tags = new TagList { { "operation", operation }, { "outcome", outcome } };
            Jobs.Add(1, tags);
            JobDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
            activity?.SetTag("outcome", outcome);
            activity?.SetStatus(succeeded ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            activity?.Dispose();
        }
    }
}
