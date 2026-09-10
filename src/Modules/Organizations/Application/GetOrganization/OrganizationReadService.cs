using Microsoft.EntityFrameworkCore;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Caching;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Application.Authorization;
using SecureFlow.Modules.Organizations.Application.Models;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Application.GetOrganization;

public sealed class OrganizationReadService(IOrganizationsDbContext dbContext, IVersionedCache cache,
    PermissionCache permissionCache, IEnumerable<IOrganizationPermissionContributor> permissionContributors)
{
    public sealed record ReadStamp(Guid Id, string Role, string Version);
    public sealed record Settings(Guid Id, string Name, string Slug, string? LogoUrl,
        DateTimeOffset CreatedAt, DateTimeOffset? ArchivedAt);

    public async Task<IReadOnlyList<OrganizationSummary>> ReadAsync(Guid userId, Guid? organizationId,
        CancellationToken cancellationToken)
    {
        // xmin is a cache generation only. It is never sent to clients or used as a concurrency token.
        var stamps = await dbContext.Database.SqlQuery<ReadStamp>($"""
            SELECT o."Id", m."Role", o.xmin::text AS "Version"
            FROM "Organizations" o
            JOIN "OrganizationMembers" m ON m."OrganizationId" = o."Id"
            WHERE m."UserId" = {userId} AND m."RemovedAt" IS NULL
            """)
            .Where(x => organizationId == null || x.Id == organizationId)
            .ToArrayAsync(cancellationToken);
        var result = new List<OrganizationSummary>(stamps.Length);
        foreach (var stamp in stamps)
        {
            Task<Settings?> Load(CancellationToken ct) => dbContext.Database.SqlQuery<Settings>($"""
                SELECT "Id", "Name", "Slug", "LogoUrl", "CreatedAt", "ArchivedAt"
                FROM "Organizations" WHERE "Id" = {stamp.Id} AND xmin::text = {stamp.Version}
                """).SingleOrDefaultAsync(ct);
            var settings = dbContext.Database.CurrentTransaction is not null
                ? await Load(cancellationToken)
                : await cache.GetOrCreateAsync(new(CacheCategory.OrganizationSettings, stamp.Id),
                    stamp.Version, Load, cancellationToken);
            // Concurrent settings update: use the current source, never fill the previous generation.
            if (settings is null || settings.Id != stamp.Id || string.IsNullOrWhiteSpace(settings.Name)
                || string.IsNullOrWhiteSpace(settings.Slug))
                settings = await dbContext.Organizations.AsNoTracking().Where(x => x.Id == stamp.Id)
                    .Select(x => new Settings(x.Id, x.Name, x.Slug, x.LogoUrl, x.CreatedAt, x.ArchivedAt))
                    .SingleOrDefaultAsync(cancellationToken);
            if (settings is null) continue;

            var role = Enum.Parse<OrganizationRole>(stamp.Role);
            var contributed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var contributor in permissionContributors)
                contributed.UnionWith(await contributor.GetOrganizationScopedPermissionsAsync(stamp.Id, userId, cancellationToken));
            var permissions = await permissionCache.GetAsync(stamp.Id, userId, null,
                OrganizationAccessRules.ToPermissionRole(role), contributions: contributed,
                cancellationToken: cancellationToken);
            result.Add(new OrganizationSummary(settings.Id, settings.Name, settings.Slug, settings.LogoUrl,
                role.ToString().ToUpperInvariant(), permissions, settings.CreatedAt, settings.ArchivedAt));
        }
        return result.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    }
}
