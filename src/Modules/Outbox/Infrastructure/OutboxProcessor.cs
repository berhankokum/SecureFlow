using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Modules.Outbox.Application;
namespace SecureFlow.Modules.Outbox.Infrastructure;

public sealed class UnsupportedOutboxEventException(string message) : Exception(message);

public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<OutboxOptions> options, ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var count = 0;
            try { count = await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Outbox batch failed."); }
            if (count == 0) await Task.Delay(_options.PollIntervalMilliseconds, stoppingToken);
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var processed = 0;
        for (; processed < _options.BatchSize; processed++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IOutboxDbContext>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var message = await db.OutboxMessages.FromSqlInterpolated($$"""
                SELECT * FROM "OutboxMessages"
                WHERE "Status" = 'Pending' AND "NextAttemptAt" <= {{now}}
                ORDER BY "NextAttemptAt", "OccurredAt"
                FOR UPDATE SKIP LOCKED LIMIT 1
                """).SingleOrDefaultAsync(cancellationToken);
            if (message is null) { await transaction.RollbackAsync(cancellationToken); return processed; }
            try
            {
                await dispatcher.DispatchAsync(new(message.Id, message.Type, message.SchemaVersion, message.Payload, message.OccurredAt), cancellationToken);
                message.MarkProcessed(now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var terminal = ex is UnsupportedOutboxEventException;
                message.MarkFailed(now, terminal ? "UNSUPPORTED_EVENT" : "DELIVERY_FAILED", _options.MaxAttempts, RetryDelay(message.AttemptCount + 1), terminal);
                logger.LogWarning("Outbox event {EventId} ({EventType}) failed with {ErrorCode}.", message.Id, message.Type, terminal ? "UNSUPPORTED_EVENT" : "DELIVERY_FAILED");
            }
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        }
        return processed;
    }

    private static TimeSpan RetryDelay(int attempt) => attempt switch { 1 => TimeSpan.FromSeconds(5), 2 => TimeSpan.FromSeconds(30), 3 => TimeSpan.FromMinutes(2), 4 => TimeSpan.FromMinutes(10), 5 => TimeSpan.FromHours(1), _ => TimeSpan.FromHours(6) };
}
