using Microsoft.Extensions.Diagnostics.HealthChecks;
using SecureFlow.Api.Persistence;

namespace SecureFlow.Api.HealthChecks;

public sealed class PostgresHealthCheck(SecureFlowDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL connection succeeded.")
                : HealthCheckResult.Unhealthy("PostgreSQL connection could not be established.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL health check threw an exception.", ex);
        }
    }
}
