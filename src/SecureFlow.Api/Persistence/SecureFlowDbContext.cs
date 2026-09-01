using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Api.Persistence;

/// <summary>
/// Composition-root DbContext for the modular monolith (single primary database).
/// Identity tabloları (AspNetUsers, ...) IdentityUserContext üzerinden gelir
/// (Organization/Project roller ASP.NET Identity Role'ü kullanmaz, bkz. SecureFlow-Final.md §8).
/// Her modül kendi entity configuration'larını ApplyConfigurationsFromAssembly ile katar.
/// </summary>
public sealed class SecureFlowDbContext(DbContextOptions<SecureFlowDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IOrganizationsDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Organization).Assembly);

        // Cross-module user references composition root'ta kurulur; domain modülleri
        // birbirlerinin assembly'lerine bağımlı hale getirilmez.
        modelBuilder.Entity<Organization>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(organization => organization.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrganizationMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrganizationMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.InvitedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
