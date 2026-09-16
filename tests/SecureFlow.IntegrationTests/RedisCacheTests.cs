using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureFlow.Api.Caching;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Caching;
using StackExchange.Redis;

namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RedisCacheTests(AuthWebApplicationFactory factory)
{
    public sealed record Value(string Name);

    [Fact]
    public async Task Redis_hits_have_absolute_ttl_and_expiration_reloads_source()
    {
        var distributed = factory.Services.GetRequiredService<IDistributedCache>();
        var cache = new RedisVersionedCache(distributed, Options.Create(new CacheOptions { OrganizationSettingsTtlSeconds = 1 }),
            NullLogger<RedisVersionedCache>.Instance);
        var address = new CacheAddress(CacheCategory.OrganizationSettings, Guid.NewGuid());
        var calls = 0;
        Task<Value?> Load(CancellationToken _) { calls++; return Task.FromResult<Value?>(new("source")); }
        Assert.Equal("source", (await cache.GetOrCreateAsync(address, "v1", Load))!.Name);
        Assert.Equal("source", (await cache.GetOrCreateAsync(address, "v1", Load))!.Name);
        Assert.Equal(1, calls);
        using var connection = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        var ttl = await connection.GetDatabase().KeyTimeToLiveAsync(address.Key("v1"));
        Assert.NotNull(ttl);
        Assert.InRange(ttl!.Value.TotalMilliseconds, 1, 1000);
        // Docker's clock/scheduling can lag the host under CI load. Observe actual
        // expiry without reading/refilling the cache; keep a bounded failure timeout.
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (await connection.GetDatabase().KeyExistsAsync(address.Key("v1"))
            && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(100);
        }
        Assert.False(await connection.GetDatabase().KeyExistsAsync(address.Key("v1")));
        await cache.GetOrCreateAsync(address, "v1", Load);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Concurrent_old_fill_cannot_replace_new_generation()
    {
        var cache = factory.Services.GetRequiredService<IVersionedCache>();
        var address = new CacheAddress(CacheCategory.OrganizationSettings, Guid.NewGuid());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = cache.GetOrCreateAsync<Value>(address, "old", async _ =>
        {
            started.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return new("old");
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await cache.GetOrCreateAsync<Value>(address, "new", _ => Task.FromResult<Value?>(new("new")));
        }
        finally { release.TrySetResult(); }
        await old;
        var current = await cache.GetOrCreateAsync<Value>(address, "new", _ => throw new InvalidOperationException("Should hit new version"));
        Assert.Equal("new", current!.Name);
    }

    [Fact]
    public async Task Tenant_user_project_and_category_keys_are_isolated_and_malformed_values_fall_back()
    {
        var cache = factory.Services.GetRequiredService<IVersionedCache>();
        var raw = factory.Services.GetRequiredService<IDistributedCache>();
        var address = new CacheAddress(CacheCategory.Permissions, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await cache.GetOrCreateAsync<Value>(address, "roles", _ => Task.FromResult<Value?>(new("original")));
        var copied = await raw.GetAsync(address.Key("roles"));
        Assert.NotNull(copied);
        foreach (var other in new[]
        {
            address with { OrganizationId = Guid.NewGuid() },
            address with { UserId = Guid.NewGuid() },
            address with { ProjectId = Guid.NewGuid() },
            address with { Category = CacheCategory.Dashboard },
        })
        {
            Assert.NotEqual(address.Key("roles"), other.Key("roles"));
            await raw.SetAsync(other.Key("roles"), copied!, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
            var value = await cache.GetOrCreateAsync<Value>(other, "roles", _ => Task.FromResult<Value?>(new("correct scope")));
            Assert.Equal("correct scope", value!.Name);
        }
        await raw.SetAsync(address.Key("roles"), Encoding.UTF8.GetBytes("not-json"));
        var repaired = await cache.GetOrCreateAsync<Value>(address, "roles", _ => Task.FromResult<Value?>(new("repaired")));
        Assert.Equal("repaired", repaired!.Name);
    }

    [Fact]
    public async Task Permission_cache_never_trusts_a_corrupted_permission_array()
    {
        var raw = factory.Services.GetRequiredService<IDistributedCache>();
        var permissions = factory.Services.GetRequiredService<PermissionCache>();
        var org = Guid.NewGuid();
        var user = Guid.NewGuid();
        var project = Guid.NewGuid();
        await permissions.GetAsync(org, user, project, PermissionRole.Member, PermissionRole.Viewer);
        var key = new CacheAddress(CacheCategory.Permissions, org, user, project).Key("matrix-v1:Member:Viewer:");
        var malicious = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { key, value = new[] { Permissions.ProjectUpdate } });
        await raw.SetAsync(key, malicious);
        var result = await permissions.GetAsync(org, user, project, PermissionRole.Member, PermissionRole.Viewer);
        Assert.DoesNotContain(Permissions.ProjectUpdate, result);
    }

    [Fact]
    public async Task Disabled_cache_and_source_failures_do_not_return_stale_data()
    {
        var raw = factory.Services.GetRequiredService<IDistributedCache>();
        var enabled = factory.Services.GetRequiredService<IVersionedCache>();
        var disabled = new RedisVersionedCache(raw, Options.Create(new CacheOptions { Enabled = false }),
            NullLogger<RedisVersionedCache>.Instance);
        var address = new CacheAddress(CacheCategory.OrganizationSettings, Guid.NewGuid());
        await enabled.GetOrCreateAsync<Value>(address, "old", _ => Task.FromResult<Value?>(new("old")));
        Assert.Equal("fresh", (await disabled.GetOrCreateAsync<Value>(address, "old", _ => Task.FromResult<Value?>(new("fresh"))))!.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() => enabled.GetOrCreateAsync<Value>(address, "new",
            _ => throw new InvalidOperationException("Source failure")));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enabled.GetOrCreateAsync<Value>(address, "old",
            _ => throw new InvalidOperationException("Cancelled request should not load"), cancelled.Token));
    }
}
