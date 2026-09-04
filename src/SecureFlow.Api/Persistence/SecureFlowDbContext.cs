using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.Teams.Application.Abstractions;
using SecureFlow.Modules.Teams.Domain;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Application.Authorization;
using SecureFlow.Modules.Projects.Domain;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Api.Persistence;

/// <summary>
/// Composition-root DbContext for the modular monolith (single primary database).
/// Identity tabloları (AspNetUsers, ...) IdentityUserContext üzerinden gelir
/// (Organization/Project roller ASP.NET Identity Role'ü kullanmaz, bkz. SecureFlow-Final.md §8).
/// Her modül kendi entity configuration'larını ApplyConfigurationsFromAssembly ile katar.
/// </summary>
public sealed class SecureFlowDbContext(DbContextOptions<SecureFlowDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IOrganizationsDbContext, ITeamsDbContext,
        IProjectsDbContext, IWorkItemsDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<WorkItemLabel> WorkItemLabels => Set<WorkItemLabel>();

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public DbSet<WorkflowState> WorkflowStates => Set<WorkflowState>();

    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

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

    public async Task<bool> LockProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var lockedProjectId = await Database.SqlQuery<Guid>(
                $"SELECT \"Id\" AS \"Value\" FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        return lockedProjectId != Guid.Empty;
    }

    public async Task<bool> LockWorkItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var lockedWorkItemId = await Database.SqlQuery<Guid>(
                $"SELECT \"Id\" AS \"Value\" FROM \"WorkItems\" WHERE \"Id\" = {workItemId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        return lockedWorkItemId != Guid.Empty;
    }

    public async Task<bool> LockOrganizationMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var lockedMembershipId = await Database.SqlQuery<Guid>(
                $"""SELECT "Id" AS "Value" FROM "OrganizationMembers" WHERE "OrganizationId" = {organizationId} AND "UserId" = {userId} AND "RemovedAt" IS NULL FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        return lockedMembershipId != Guid.Empty;
    }

    public Task<Guid?> FindProjectOrganizationIdAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => (Guid?)project.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkItemProjectContext?> GetWorkItemProjectContextAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var project = await Projects
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new
            {
                item.Id,
                item.OrganizationId,
                item.Key,
                IsArchived = item.ArchivedAt != null,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return null;
        }

        var organizationRole = await GetActiveOrganizationRoleAsync(
            project.OrganizationId,
            userId,
            cancellationToken);
        if (organizationRole is null)
        {
            return new WorkItemProjectContext(
                project.Id, project.OrganizationId, project.Key, project.IsArchived, false, []);
        }

        var projectRole = await ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
        var hasProjectAccess = organizationRole is PermissionRole.Owner or PermissionRole.Admin
            || projectRole is not null;
        var permissions = hasProjectAccess
            ? ProjectAuthorizationEvaluator.GetPermissions(organizationRole.Value, projectRole)
            : [];

        return new WorkItemProjectContext(
            project.Id,
            project.OrganizationId,
            project.Key,
            project.IsArchived,
            hasProjectAccess,
            permissions);
    }

    public async Task<bool> CanUserAccessProjectAsync(
        Guid projectId,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationRole = await GetActiveOrganizationRoleAsync(
            organizationId,
            userId,
            cancellationToken);
        if (organizationRole is PermissionRole.Owner or PermissionRole.Admin)
        {
            return true;
        }

        return organizationRole is PermissionRole.Member
            && await ProjectMembers.AsNoTracking().AnyAsync(
                member => member.ProjectId == projectId && member.UserId == userId,
                cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Organization).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Team).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Project).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkItem).Assembly);

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

        modelBuilder.Entity<Project>()
            .HasAlternateKey(project => new { project.Id, project.OrganizationId })
            .HasName("AK_Projects_Id_OrganizationId");

        modelBuilder.Entity<WorkItem>()
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.ProjectId, workItem.OrganizationId })
            .HasPrincipalKey(project => new { project.Id, project.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Workflow>()
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(workflow => workflow.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkItem>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(workItem => workItem.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkItem>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(workItem => workItem.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Label>()
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(label => new { label.ProjectId, label.OrganizationId })
            .HasPrincipalKey(project => new { project.Id, project.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Label>()
            .HasOne<ApplicationUser>().WithMany().HasForeignKey(label => label.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkItemLabel>()
            .HasOne<ApplicationUser>().WithMany().HasForeignKey(assignment => assignment.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TeamMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.AddedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
