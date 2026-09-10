using SecureFlow.Contracts.Authorization;

namespace SecureFlow.Contracts.Caching;

/// <summary>Caches projections, never membership or access decisions. Inputs must be freshly database-validated.</summary>
public sealed class PermissionCache(IVersionedCache cache)
{
    public async Task<IReadOnlyList<string>> GetAsync(Guid organizationId, Guid userId, Guid? projectId,
        PermissionRole organizationRole, PermissionRole? projectRole = null,
        IReadOnlyCollection<string>? contributions = null, CancellationToken cancellationToken = default)
    {
        if (organizationRole is not (PermissionRole.Owner or PermissionRole.Admin or PermissionRole.Member)
            || (projectRole is not null && projectRole is not (PermissionRole.Manager or PermissionRole.Contributor or PermissionRole.Viewer)))
            throw new ArgumentException("Invalid permission role scope.");

        var additional = (contributions ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var version = $"matrix-v1:{organizationRole}:{projectRole}:{string.Join(',', additional)}";
        var permissions = await cache.GetOrCreateAsync<string[]>(
            new(CacheCategory.Permissions, organizationId, userId, projectId), version,
            _ => Task.FromResult<string[]?>(Calculate()), cancellationToken);
        // Invalid cache values must never create new permissions.
        var expected = Calculate();
        return permissions is not null && permissions.SequenceEqual(expected, StringComparer.Ordinal) ? permissions : expected;

        string[] Calculate() => PermissionMatrix.GetPermissions(organizationRole)
            .Concat(projectRole is { } role ? PermissionMatrix.GetPermissions(role) : [])
            .Concat(additional).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
