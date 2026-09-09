namespace SecureFlow.Contracts.BackgroundProcessing;

public sealed class BackgroundProcessingOptions
{
    public const string SectionName = "BackgroundProcessing";

    public bool Enabled { get; init; } = true;

    public int PollingIntervalSeconds { get; init; } = 60;

    public int BatchSize { get; init; } = 100;
}
