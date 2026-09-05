using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using SecureFlow.Modules.Files.Application.Abstractions;

namespace SecureFlow.Modules.Files.Infrastructure;

public sealed class ObjectStorageBucketInitializer(IObjectStorage objectStorage) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        objectStorage.EnsureBucketAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ObjectStorageHealthCheck(IObjectStorage objectStorage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await objectStorage.EnsureBucketAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Object storage is unavailable.", exception);
        }
    }
}
