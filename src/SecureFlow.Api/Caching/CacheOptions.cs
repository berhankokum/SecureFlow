using System.ComponentModel.DataAnnotations;
using SecureFlow.Contracts.Caching;

namespace SecureFlow.Api.Caching;

public sealed class CacheOptions
{
    public bool Enabled { get; set; } = true;
    [Range(1, 3600)] public int PermissionTtlSeconds { get; set; } = 60;
    [Range(1, 3600)] public int OrganizationSettingsTtlSeconds { get; set; } = 300;
    [Range(1, 300)] public int DashboardTtlSeconds { get; set; } = 30;
    [Range(100, 5000)] public int OperationTimeoutMilliseconds { get; set; } = 500;
    public TimeSpan Ttl(CacheCategory category) => TimeSpan.FromSeconds(category switch
    {
        CacheCategory.Permissions => PermissionTtlSeconds,
        CacheCategory.OrganizationSettings => OrganizationSettingsTtlSeconds,
        CacheCategory.Dashboard => DashboardTtlSeconds,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    });
}
