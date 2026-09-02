using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.Teams.Application.Abstractions;
using SecureFlow.Modules.Teams.Domain;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Api.Persistence;

/// <summary>
/// Composition-root DbContext for the modular monolith (single primary database).
/// Identity tabloları (AspNetUsers, ...) IdentityUserContext üzerinden gelir
/// (Organization/Project roller ASP.NET Identity Role'ü kullanmaz, bkz. SecureFlow-Final.md §8).
/// Her modül kendi entity configuration'larını ApplyConfigurationsFromAssembly ile katar.
/// </summary>
public sealed class SecureFlowDbContext(DbContextOptions<SecureFlowDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IOrganizationsDbContext, ITeamsDbContext, IProjectsDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public async Task<PermissionRole?> GetActiveOrganizationRoleAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var role = await OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId
                && member.UserId == userId
                && member.RemovedAt == null)
            .Select(member => (OrganizationRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role switch
        {
            OrganizationRole.Owner => PermissionRole.Owner,
            OrganizationRole.Admin => PermissionRole.Admin,
            OrganizationRole.Member => PermissionRole.Member,
            _ => null,
        };
    }

    public Task<bool> IsOrganizationArchivedAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        Organizations.AnyAsync(
            organization => organization.Id == organizationId && organization.ArchivedAt != null,
            cancellationToken);

    public Task<bool> IsActiveOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        OrganizationMembers.AnyAsync(
            member => member.OrganizationId == organizationId
                && member.UserId == userId
                && member.RemovedAt == null,
            cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Organization).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Team).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Project).Assembly);

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

        modelBuilder.Entity<OrganizationInvitation>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Team>()
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(team => team.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TeamMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Project>()
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Project>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(project => project.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TeamMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.AddedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
