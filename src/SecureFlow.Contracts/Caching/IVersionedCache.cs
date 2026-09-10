using System.Security.Cryptography;
using System.Text;

namespace SecureFlow.Contracts.Caching;

public enum CacheCategory { Permissions, OrganizationSettings, Dashboard }

/// <summary>Only use after database authorization; version must come from current database inputs.</summary>
public sealed record CacheAddress(CacheCategory Category, Guid OrganizationId, Guid? UserId = null, Guid? ProjectId = null)
{
    public string Key(string version)
    {
        if (!Enum.IsDefined(Category) || OrganizationId == Guid.Empty || UserId == Guid.Empty || ProjectId == Guid.Empty)
            throw new ArgumentException("Invalid cache scope.");
        if (Category is CacheCategory.Permissions or CacheCategory.Dashboard && UserId is null)
            throw new ArgumentException("User-scoped cache requires a user id.");
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(version)));
        return $"secureflow:v1:{Category}:{OrganizationId:N}:{UserId:N}:{ProjectId:N}:{fingerprint}";
    }
}

public interface IVersionedCache
{
    Task<T?> GetOrCreateAsync<T>(CacheAddress address, string version,
        Func<CancellationToken, Task<T?>> load, CancellationToken cancellationToken = default) where T : class;
}
