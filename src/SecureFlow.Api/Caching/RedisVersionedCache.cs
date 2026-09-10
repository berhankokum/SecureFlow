using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.Caching;
using StackExchange.Redis;

namespace SecureFlow.Api.Caching;

public sealed class RedisVersionedCache(IDistributedCache cache, IOptions<CacheOptions> options,
    ILogger<RedisVersionedCache> logger) : IVersionedCache
{
    private const int MaximumPayloadBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record Envelope<T>(string Key, T Value);

    public async Task<T?> GetOrCreateAsync<T>(CacheAddress address, string version,
        Func<CancellationToken, Task<T?>> load, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = address.Key(version);
        if (!options.Value.Enabled) return await load(cancellationToken);
        var canWrite = true;
        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken)
                .WaitAsync(TimeSpan.FromMilliseconds(options.Value.OperationTimeoutMilliseconds), cancellationToken);
            if (bytes is { Length: > 0 and <= MaximumPayloadBytes })
            {
                var item = JsonSerializer.Deserialize<Envelope<T>>(bytes, JsonOptions);
                if (item?.Key == key && item.Value is not null) return item.Value;
            }
        }
        catch (JsonException) { logger.LogDebug("Malformed cache entry ignored for {Category}.", address.Category); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            canWrite = false;
            logger.LogDebug("Cache unavailable for {Category}; using source.", address.Category);
        }

        var value = await load(cancellationToken);
        if (value is null || !canWrite) return value;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Envelope<T>(key, value), JsonOptions);
        if (payload.Length > MaximumPayloadBytes) return value;
        try
        {
            await cache.SetAsync(key, payload, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.Value.Ttl(address.Category),
            }, cancellationToken).WaitAsync(TimeSpan.FromMilliseconds(options.Value.OperationTimeoutMilliseconds), cancellationToken);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogDebug("Cache write unavailable for {Category}; returning source value.", address.Category);
        }
        return value;
    }
}
