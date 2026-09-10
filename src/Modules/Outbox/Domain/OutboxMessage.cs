namespace SecureFlow.Modules.Outbox.Domain;

public enum OutboxStatus { Pending, Processed, Failed }

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    private OutboxMessage(Guid id, string type, int schemaVersion, string payload, DateTimeOffset occurredAt)
    { Id = id; Type = type; SchemaVersion = schemaVersion; Payload = payload; OccurredAt = occurredAt; CreatedAt = occurredAt; NextAttemptAt = occurredAt; Status = OutboxStatus.Pending; }
    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public int SchemaVersion { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public static OutboxMessage Create(Guid id, string type, int schemaVersion, string payload, DateTimeOffset occurredAt) => new(id, type, schemaVersion, payload, occurredAt);
    public void MarkProcessed(DateTimeOffset now) { AttemptCount++; LastAttemptAt = now; ProcessedAt = now; Status = OutboxStatus.Processed; LastErrorCode = null; }
    public void MarkFailed(DateTimeOffset now, string code, int maxAttempts, TimeSpan delay, bool terminal = false)
    { AttemptCount++; LastAttemptAt = now; LastErrorCode = code; if (terminal || AttemptCount >= maxAttempts) Status = OutboxStatus.Failed; else NextAttemptAt = now.Add(delay); }
}
