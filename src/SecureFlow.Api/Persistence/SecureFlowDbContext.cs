using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Identity.Domain;
using SecureFlow.Modules.Organizations.Application.Abstractions;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.Teams.Application.Abstractions;
using SecureFlow.Modules.Teams.Domain;
using SecureFlow.Contracts.Authorization;
using SecureFlow.Contracts.Activity;
using SecureFlow.Modules.Projects.Application.Abstractions;
using SecureFlow.Modules.Projects.Application.Authorization;
using SecureFlow.Modules.Projects.Domain;
using SecureFlow.Modules.WorkItems.Application.Abstractions;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.Modules.Collaboration.Application.Abstractions;
using SecureFlow.Modules.Collaboration.Domain;
using SecureFlow.Modules.Notifications.Application.Abstractions;
using SecureFlow.Modules.Notifications.Domain;
using SecureFlow.Modules.Files.Application.Abstractions;
using SecureFlow.Modules.Files.Domain;
using SecureFlow.Modules.Outbox.Application;
using SecureFlow.Modules.Outbox.Domain;
using SecureFlow.Contracts.Outbox;
using SecureFlow.Contracts.Caching;
using System.Text.Json;
using SecureFlow.Contracts.Audit;
using SecureFlow.Modules.Audit.Application;
using SecureFlow.Modules.Audit.Domain;

namespace SecureFlow.Api.Persistence;

/// <summary>
/// Composition-root DbContext for the modular monolith (single primary database).
/// Identity tabloları (AspNetUsers, ...) IdentityUserContext üzerinden gelir
/// (Organization/Project roller ASP.NET Identity Role'ü kullanmaz, bkz. SecureFlow-Final.md §8).
/// Her modül kendi entity configuration'larını ApplyConfigurationsFromAssembly ile katar.
/// </summary>
public sealed partial class SecureFlowDbContext(DbContextOptions<SecureFlowDbContext> options, PermissionCache permissionCache,
    IAuditRequestContext? auditContext = null)
    : IdentityUserContext<ApplicationUser, Guid>(options), IOrganizationsDbContext, ITeamsDbContext,
        IProjectsDbContext, IWorkItemsDbContext, ICollaborationDbContext, INotificationsDbContext,
        IFilesDbContext, IOutboxDbContext, IAuditDbContext
{
    private static readonly JsonSerializerOptions OutboxJsonOptions = new(JsonSerializerDefaults.Web);
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

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    public DbSet<Mention> Mentions => Set<Mention>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<NotificationEmailDelivery> NotificationEmailDeliveries => Set<NotificationEmailDelivery>();

    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await CaptureAuditEventsAsync(cancellationToken);
        await CaptureOutboxEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task CaptureOutboxEventsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<WorkItem>().Where(x => x.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            var type = entry.State == EntityState.Added ? OutboxEventTypes.WorkItemCreated
                : entry.Property(nameof(WorkItem.AssigneeId)).IsModified ? OutboxEventTypes.WorkItemAssigned
                : entry.Property(nameof(WorkItem.WorkflowStateId)).IsModified ? OutboxEventTypes.WorkItemTransitioned
                : OutboxEventTypes.WorkItemUpdated;
            var eventId = type switch
            {
                OutboxEventTypes.WorkItemCreated => FindActivityEventId(entry.Entity.Id, ActivityTypes.WorkItemCreated),
                OutboxEventTypes.WorkItemAssigned => FindActivityEventId(entry.Entity.Id, ActivityTypes.WorkItemAssigned, ActivityTypes.WorkItemUnassigned),
                OutboxEventTypes.WorkItemTransitioned => FindActivityEventId(entry.Entity.Id, ActivityTypes.WorkItemTransitioned),
                _ => null,
            } ?? Guid.NewGuid();
            AddOutbox(eventId, type, new WorkItemOutboxPayload(entry.Entity.OrganizationId, entry.Entity.ProjectId, entry.Entity.Id), now);
        }

        foreach (var entry in ChangeTracker.Entries<WorkItemLabel>().Where(x => x.State is EntityState.Added or EntityState.Deleted).ToArray())
        {
            var workItem = WorkItems.Local.FirstOrDefault(x => x.Id == entry.Entity.WorkItemId)
                ?? await WorkItems.AsNoTracking().SingleAsync(x => x.Id == entry.Entity.WorkItemId, cancellationToken);
            AddOutbox(FindActivityEventId(workItem.Id, ActivityTypes.LabelAssigned, ActivityTypes.LabelRemoved) ?? Guid.NewGuid(), OutboxEventTypes.WorkItemUpdated, new WorkItemOutboxPayload(workItem.OrganizationId, workItem.ProjectId, workItem.Id), now);
        }

        foreach (var entry in ChangeTracker.Entries<Comment>().Where(x => x.State == EntityState.Added).ToArray())
        {
            var workItem = WorkItems.Local.FirstOrDefault(x => x.Id == entry.Entity.WorkItemId)
                ?? await WorkItems.AsNoTracking().SingleAsync(x => x.Id == entry.Entity.WorkItemId, cancellationToken);
            AddOutbox(FindActivityEventId(workItem.Id, ActivityTypes.CommentCreated) ?? Guid.NewGuid(), OutboxEventTypes.CommentCreated, new CommentOutboxPayload(entry.Entity.OrganizationId, workItem.ProjectId, entry.Entity.WorkItemId, entry.Entity.Id), entry.Entity.CreatedAt);
        }

        foreach (var entry in ChangeTracker.Entries<Notification>().Where(x => x.State == EntityState.Added).ToArray())
            AddOutbox(entry.Entity.Id, OutboxEventTypes.NotificationCreated, new NotificationOutboxPayload(entry.Entity.UserId, entry.Entity.Id), entry.Entity.CreatedAt);

        foreach (var entry in ChangeTracker.Entries<OrganizationMember>().Where(x => x.State == EntityState.Modified && x.Property(nameof(OrganizationMember.RemovedAt)).IsModified && x.Entity.RemovedAt is not null).ToArray())
            AddOutbox(Guid.NewGuid(), OutboxEventTypes.MemberRemoved, new MembershipOutboxPayload(entry.Entity.OrganizationId, entry.Entity.UserId), entry.Entity.RemovedAt!.Value);

        foreach (var entry in ChangeTracker.Entries<Attachment>().Where(x => x.State == EntityState.Modified && x.Property(nameof(Attachment.DeletedAt)).IsModified && x.Entity.DeletedAt is not null).ToArray())
            AddOutbox(AttachmentDeleteEventId(entry.Entity.Id), OutboxEventTypes.AttachmentObjectDeleteRequested, new AttachmentObjectDeletePayload(entry.Entity.StorageKey), entry.Entity.DeletedAt!.Value);
    }

    private void AddOutbox(Guid id, string type, object payload, DateTimeOffset occurredAt)
    {
        if (OutboxMessages.Local.Any(x => x.Id == id)) return;
        OutboxMessages.Add(OutboxMessage.Create(id, type, 1, JsonSerializer.Serialize(payload, payload.GetType(), OutboxJsonOptions), occurredAt));
    }

    private Guid? FindActivityEventId(Guid workItemId, params string[] types) =>
        ChangeTracker.Entries<ActivityEvent>()
            .Where(x => x.State == EntityState.Added && x.Entity.WorkItemId == workItemId && types.Contains(x.Entity.Type, StringComparer.Ordinal))
            .Select(x => (Guid?)x.Entity.Id)
            .LastOrDefault();

    public static Guid AttachmentDeleteEventId(Guid attachmentId)
    {
        Span<byte> bytes = stackalloc byte[16]; attachmentId.TryWriteBytes(bytes); bytes[0] ^= 0xA7; bytes[15] ^= 0x17; return new Guid(bytes);
    }

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

    public async Task<bool> LockOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var lockedOrganizationId = await Database.SqlQuery<Guid>(
                $"""SELECT "Id" AS "Value" FROM "Organizations" WHERE "Id" = {organizationId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        return lockedOrganizationId != Guid.Empty;
    }

    public async Task<bool> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lockedUserId = await Database.SqlQuery<Guid>(
                $"""SELECT "Id" AS "Value" FROM "AspNetUsers" WHERE "Id" = {userId} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
        return lockedUserId != Guid.Empty;
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
            ? await permissionCache.GetAsync(project.OrganizationId, userId, projectId, organizationRole.Value,
                projectRole is { } role ? ProjectAuthorizationEvaluator.ToPermissionRole(role) : null,
                cancellationToken: cancellationToken)
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

    public async Task<IReadOnlyList<DueReminderCandidate>> GetDueReminderCandidatesAsync(
        DateOnly dueDate,
        int offset,
        int limit,
        CancellationToken cancellationToken) =>
        await (
            from workItem in WorkItems.AsNoTracking()
            join project in Projects.AsNoTracking() on workItem.ProjectId equals project.Id
            join state in WorkflowStates.AsNoTracking() on workItem.WorkflowStateId equals state.Id
            where workItem.DueDate == dueDate
                && workItem.ArchivedAt == null
                && project.ArchivedAt == null
                && state.Category != WorkflowCategory.Done
                && workItem.OrganizationId == project.OrganizationId
            orderby workItem.Id
            select new DueReminderCandidate(
                workItem.Id,
                workItem.OrganizationId,
                workItem.ProjectId,
                project.Key,
                workItem.Number,
                workItem.Title,
                workItem.ReporterId,
                workItem.AssigneeId,
                workItem.DueDate!.Value))
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

    public Task<CollaborationResourceScope?> FindWorkItemScopeAsync(
        Guid workItemId,
        CancellationToken cancellationToken) =>
        WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == workItemId)
            .Select(workItem => new CollaborationResourceScope(
                workItem.Id,
                workItem.ProjectId,
                workItem.OrganizationId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CollaborationAccessContext?> GetCollaborationAccessAsync(
        Guid workItemId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var resource = await (
            from workItem in WorkItems.AsNoTracking()
            join project in Projects.AsNoTracking() on workItem.ProjectId equals project.Id
            where workItem.Id == workItemId && workItem.OrganizationId == project.OrganizationId
            select new
            {
                WorkItemId = workItem.Id,
                workItem.ProjectId,
                workItem.OrganizationId,
                workItem.ReporterId,
                workItem.AssigneeId,
                IsWorkItemArchived = workItem.ArchivedAt != null,
                IsProjectArchived = project.ArchivedAt != null,
            }).SingleOrDefaultAsync(cancellationToken);
        if (resource is null)
        {
            return null;
        }

        var organizationRole = await GetActiveOrganizationRoleAsync(
            resource.OrganizationId,
            userId,
            cancellationToken);
        if (organizationRole is null)
        {
            return new CollaborationAccessContext(
                resource.WorkItemId,
                resource.ProjectId,
                resource.OrganizationId,
                resource.ReporterId,
                resource.AssigneeId,
                resource.IsWorkItemArchived,
                resource.IsProjectArchived,
                false,
                false,
                []);
        }

        var projectRole = await ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == resource.ProjectId && member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
        var hasProjectAccess = organizationRole is PermissionRole.Owner or PermissionRole.Admin
            || projectRole is not null;
        var permissions = hasProjectAccess
            ? await permissionCache.GetAsync(resource.OrganizationId, userId, resource.ProjectId, organizationRole.Value,
                projectRole is { } role ? ProjectAuthorizationEvaluator.ToPermissionRole(role) : null,
                cancellationToken: cancellationToken)
            : [];
        var canModerate = organizationRole is PermissionRole.Owner or PermissionRole.Admin
            || projectRole is ProjectRole.Manager;

        return new CollaborationAccessContext(
            resource.WorkItemId,
            resource.ProjectId,
            resource.OrganizationId,
            resource.ReporterId,
            resource.AssigneeId,
            resource.IsWorkItemArchived,
            resource.IsProjectArchived,
            hasProjectAccess,
            canModerate,
            permissions);
    }

    public async Task<IReadOnlyList<Guid>> GetMentionCandidateUserIdsAsync(
        Guid projectId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId
                && member.RemovedAt == null
                && (member.Role == OrganizationRole.Owner
                    || member.Role == OrganizationRole.Admin
                    || ProjectMembers.Any(projectMember =>
                        projectMember.ProjectId == projectId
                        && projectMember.UserId == member.UserId)))
            .Select(member => member.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    public Task<FileResourceScope?> FindWorkItemFileScopeAsync(
        Guid workItemId,
        CancellationToken cancellationToken) =>
        WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.Id == workItemId)
            .Select(workItem => new FileResourceScope(
                workItem.Id,
                workItem.ProjectId,
                workItem.OrganizationId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<FileAccessContext?> GetFileAccessAsync(
        Guid workItemId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await GetCollaborationAccessAsync(workItemId, userId, cancellationToken);
        return access is null
            ? null
            : new FileAccessContext(
                access.WorkItemId,
                access.ProjectId,
                access.OrganizationId,
                access.IsWorkItemArchived,
                access.IsProjectArchived,
                access.HasProjectAccess,
                access.CanModerateComments,
                access.Permissions);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Organization).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Team).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Project).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkItem).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Comment).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Notification).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Attachment).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxMessage).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLog).Assembly);
        modelBuilder.Entity<AuditLog>().HasOne<Organization>().WithMany()
            .HasForeignKey(log => log.OrganizationId).OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<WorkItem>()
            .HasAlternateKey(workItem => new { workItem.Id, workItem.OrganizationId })
            .HasName("AK_WorkItems_Id_OrganizationId");

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

        modelBuilder.Entity<Comment>()
            .HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(comment => new { comment.WorkItemId, comment.OrganizationId })
            .HasPrincipalKey(workItem => new { WorkItemId = workItem.Id, workItem.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Comment>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Comment>()
            .HasAlternateKey(comment => new { comment.Id, comment.OrganizationId })
            .HasName("AK_Comments_Id_OrganizationId");

        modelBuilder.Entity<Mention>()
            .HasOne<Comment>()
            .WithMany()
            .HasForeignKey(mention => new { mention.CommentId, mention.OrganizationId })
            .HasPrincipalKey(comment => new { comment.Id, comment.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Mention>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(mention => mention.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityEvent>()
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(activity => new { activity.ProjectId, activity.OrganizationId })
            .HasPrincipalKey(project => new { project.Id, project.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(notification => notification.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<NotificationPreference>()
            .HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<NotificationPreference>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<NotificationEmailDelivery>()
            .HasOne<Notification>()
            .WithOne()
            .HasForeignKey<NotificationEmailDelivery>(delivery => delivery.NotificationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attachment>()
            .HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(attachment => new { attachment.WorkItemId, attachment.OrganizationId })
            .HasPrincipalKey(workItem => new { WorkItemId = workItem.Id, workItem.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Attachment>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(attachment => attachment.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityEvent>()
            .HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(activity => new { activity.WorkItemId, activity.OrganizationId })
            .HasPrincipalKey(workItem => new { workItem.Id, workItem.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityEvent>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(activity => activity.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TeamMember>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(member => member.AddedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
