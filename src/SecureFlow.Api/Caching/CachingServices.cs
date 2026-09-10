using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.Caching;
using StackExchange.Redis;

namespace SecureFlow.Api.Caching;

public static class CachingServices
{
    public static IServiceCollection AddSecureFlowCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>().Bind(configuration.GetSection("Caching"))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddStackExchangeRedisCache(options =>
        {
            var connection = configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("ConnectionStrings:Redis is required for caching.");
            var redis = ConfigurationOptions.Parse(connection);
            redis.AbortOnConnectFail = false;
            redis.ConnectTimeout = configuration.GetValue("Caching:OperationTimeoutMilliseconds", 500);
            redis.AsyncTimeout = redis.ConnectTimeout;
            redis.SyncTimeout = redis.ConnectTimeout;
            redis.ConnectRetry = 0;
            redis.BacklogPolicy = BacklogPolicy.FailFast;
            redis.IncludeDetailInExceptions = false;
            options.ConfigurationOptions = redis;
        });
        services.AddSingleton<IVersionedCache, RedisVersionedCache>();
        services.AddSingleton<PermissionCache>();
        services.AddHealthChecks().AddCheck<RedisCacheHealthCheck>("redis", tags: ["cache"]);
        return services;
    }
}

public sealed class RedisCacheHealthCheck(IDistributedCache cache, IOptions<CacheOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled) return HealthCheckResult.Healthy("Caching disabled.");
        try
        {
            await cache.GetAsync("secureflow:v1:health", cancellationToken)
                .WaitAsync(TimeSpan.FromMilliseconds(options.Value.OperationTimeoutMilliseconds), cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            return HealthCheckResult.Degraded("Redis unavailable; source fallback active.");
        }
    }
}
