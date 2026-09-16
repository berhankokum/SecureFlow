using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SecureFlow.Contracts.Caching;
using StackExchange.Redis;
using SecureFlow.Contracts.Observability;

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
        using var activity = FlowTelemetry.StartActivity("cache.read");
        void Record(string outcome)
        {
            activity?.SetTag("cache.category", address.Category.ToString());
            activity?.SetTag("cache.outcome", outcome);
            FlowTelemetry.Cache.Add(1,
                new KeyValuePair<string, object?>("category", address.Category.ToString()),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
        async Task<T?> LoadSource()
        {
            try { return await load(cancellationToken); }
            catch
            {
                Record("source_failure");
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
                throw;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var key = address.Key(version);
        if (!options.Value.Enabled) { Record("disabled"); return await LoadSource(); }
        var canWrite = true;
        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken)
                .WaitAsync(TimeSpan.FromMilliseconds(options.Value.OperationTimeoutMilliseconds), cancellationToken);
            if (bytes is { Length: > 0 and <= MaximumPayloadBytes })
            {
                var item = JsonSerializer.Deserialize<Envelope<T>>(bytes, JsonOptions);
                if (item?.Key == key && item.Value is not null) { Record("hit"); return item.Value; }
            }
        }
        catch (JsonException) { Record("malformed"); logger.LogDebug("Malformed cache entry ignored for {Category}.", address.Category); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            canWrite = false;
            Record("unavailable");
            logger.LogDebug("Cache unavailable for {Category}; using source.", address.Category);
        }

        Record("miss");
        var value = await LoadSource();
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
            Record("write_unavailable");
            logger.LogDebug("Cache write unavailable for {Category}; returning source value.", address.Category);
        }
        return value;
    }
}
