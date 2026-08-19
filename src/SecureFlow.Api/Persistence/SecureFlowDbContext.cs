using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Identity.Domain;

namespace SecureFlow.Api.Persistence;

/// <summary>
/// Composition-root DbContext for the modular monolith (single primary database).
/// Identity tabloları (AspNetUsers, ...) IdentityUserContext üzerinden gelir
/// (Organization/Project roller ASP.NET Identity Role'ü kullanmaz, bkz. SecureFlow-Final.md §8).
/// Her modül kendi entity configuration'larını ApplyConfigurationsFromAssembly ile katar.
/// </summary>
public sealed class SecureFlowDbContext(DbContextOptions<SecureFlowDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SecureFlowDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationUser).Assembly);
    }
}
