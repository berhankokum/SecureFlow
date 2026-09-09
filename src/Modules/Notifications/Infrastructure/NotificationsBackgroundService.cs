using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.BackgroundProcessing;
using SecureFlow.Modules.Notifications.Application;

namespace SecureFlow.Modules.Notifications.Infrastructure;

public sealed class NotificationsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundProcessingOptions> options,
    ILogger<NotificationsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAsync<DeadlineReminderProcessor>(
                processor => processor.ProcessAsync(options.Value.BatchSize, stoppingToken),
                stoppingToken);
            await RunAsync<NotificationEmailDeliveryProcessor>(
                async processor =>
                {
                    await processor.DiscoverAsync(options.Value.BatchSize, stoppingToken);
                    await processor.DeliverAsync(options.Value.BatchSize, stoppingToken);
                },
                stoppingToken);

            await Task.Delay(
                TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds),
                stoppingToken);
        }
    }

    private async Task RunAsync<TProcessor>(
        Func<TProcessor, Task> operation,
        CancellationToken cancellationToken)
        where TProcessor : notnull
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await operation(scope.ServiceProvider.GetRequiredService<TProcessor>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Background processor {ProcessorName} failed with {ErrorCode}.",
                typeof(TProcessor).Name,
                exception.GetType().Name);
        }
    }
}
