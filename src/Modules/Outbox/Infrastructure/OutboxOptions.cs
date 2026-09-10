using System.ComponentModel.DataAnnotations;
namespace SecureFlow.Modules.Outbox.Infrastructure;

public sealed class OutboxOptions
{
    public bool Enabled { get; init; } = true;
    [Range(100, 60000)] public int PollIntervalMilliseconds { get; init; } = 1000;
    [Range(1, 500)] public int BatchSize { get; init; } = 100;
    [Range(1, 10)] public int MaxAttempts { get; init; } = 10;
}
