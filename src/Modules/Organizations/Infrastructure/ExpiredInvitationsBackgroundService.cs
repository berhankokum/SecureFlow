using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.BackgroundProcessing;
using SecureFlow.Modules.Organizations.Application.Invitations;

namespace SecureFlow.Modules.Organizations.Infrastructure;

public sealed class ExpiredInvitationsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundProcessingOptions> options,
    ILogger<ExpiredInvitationsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ExpiredInvitationCleanupProcessor>();
                await processor.ProcessAsync(options.Value.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Expired invitation cleanup failed with {ErrorCode}.",
                    exception.GetType().Name);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds),
                stoppingToken);
        }
    }
}
