using Microsoft.EntityFrameworkCore;
using SecureFlow.Modules.Audit.Application;
using SecureFlow.Modules.Audit.Domain;
using SecureFlow.Modules.Organizations.Domain;
using SecureFlow.Modules.Projects.Domain;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.Modules.Files.Domain;

namespace SecureFlow.Api.Persistence;

public sealed partial class SecureFlowDbContext
{
    public async Task<AuditAccess> GetAuditAccessAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var role = await OrganizationMembers.AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.UserId == userId && member.RemovedAt == null)
            .Select(member => (OrganizationRole?)member.Role).SingleOrDefaultAsync(cancellationToken);
        return role is null ? AuditAccess.NoMembership
            : role is OrganizationRole.Owner or OrganizationRole.Admin ? AuditAccess.Allowed : AuditAccess.Forbidden;
    }

    private async Task CaptureAuditEventsAsync(CancellationToken cancellationToken)
    {
        var changes = ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (changes.Any(entry => entry.Entity is AuditLog && entry.State != EntityState.Added))
            throw new InvalidOperationException("Audit records are append-only.");
        foreach (var entry in changes)
        {
            bool Changed(string name) => entry.State == EntityState.Modified && entry.Property(name).IsModified
                && !Equals(entry.Property(name).OriginalValue, entry.Property(name).CurrentValue);
            var added = entry.State == EntityState.Added;
            var deleted = entry.State == EntityState.Deleted;
            var metadata = new Dictionary<string, string?>();
            string? action = null;
            Guid? org = null;
            Guid? resourceId = null;
            string? resourceType = null;
            switch (entry.Entity)
            {
                case OrganizationInvitation invitation:
                    org = invitation.OrganizationId; resourceId = invitation.Id; resourceType = "OrganizationInvitation";
                    action = added ? "MEMBER_INVITED" : Changed(nameof(invitation.AcceptedAt)) ? "INVITATION_ACCEPTED"
                        : Changed(nameof(invitation.RevokedAt)) ? "INVITATION_REVOKED" : null;
                    metadata["role"] = invitation.Role.ToString().ToUpperInvariant();
                    break;
                case OrganizationMember member:
                    org = member.OrganizationId; resourceId = member.UserId; resourceType = "OrganizationMember";
                    action = added ? "MEMBER_ADDED" : Changed(nameof(member.RemovedAt)) ? member.RemovedAt is null ? "MEMBER_ADDED" : "MEMBER_REMOVED"
                        : Changed(nameof(member.Role)) ? "ROLE_CHANGED" : null;
                    metadata["userId"] = member.UserId.ToString();
                    metadata["role"] = member.Role.ToString().ToUpperInvariant();
                    if (Changed(nameof(member.Role))) metadata["previousRole"] = entry.Property(nameof(member.Role)).OriginalValue?.ToString()?.ToUpperInvariant();
                    break;
                case ProjectMember member:
                    action = added ? "PROJECT_MEMBER_ADDED" : deleted ? "PROJECT_MEMBER_REMOVED" : Changed(nameof(member.Role)) ? "ROLE_CHANGED" : null;
                    if (action is null) break;
                    org = Projects.Local.FirstOrDefault(project => project.Id == member.ProjectId)?.OrganizationId
                        ?? await Projects.Where(project => project.Id == member.ProjectId).Select(project => project.OrganizationId).SingleAsync(cancellationToken);
                    resourceId = member.ProjectId; resourceType = "ProjectMember";
                    metadata["userId"] = member.UserId.ToString();
                    metadata["role"] = member.Role.ToString().ToUpperInvariant();
                    if (Changed(nameof(member.Role))) metadata["previousRole"] = entry.Property(nameof(member.Role)).OriginalValue?.ToString()?.ToUpperInvariant();
                    break;
                case Project project:
                    org = project.OrganizationId; resourceId = project.Id; resourceType = "Project";
                    action = Changed(nameof(project.ArchivedAt)) && project.ArchivedAt is not null ? "PROJECT_ARCHIVED" : null;
                    break;
                case WorkItem item:
                    org = item.OrganizationId; resourceId = item.Id; resourceType = "WorkItem";
                    action = added ? "WORK_ITEM_CREATED" : Changed(nameof(item.ArchivedAt)) ? "WORK_ITEM_ARCHIVED"
                        : Changed(nameof(item.AssigneeId)) ? item.AssigneeId is null ? "WORK_ITEM_UNASSIGNED" : "WORK_ITEM_ASSIGNED"
                        : Changed(nameof(item.WorkflowStateId)) ? "WORK_ITEM_TRANSITIONED"
                        : entry.State == EntityState.Modified ? "WORK_ITEM_UPDATED" : null;
                    if (Changed(nameof(item.AssigneeId)))
                    {
                        metadata["previousAssigneeId"] = entry.Property(nameof(item.AssigneeId)).OriginalValue?.ToString();
                        metadata["assigneeId"] = item.AssigneeId?.ToString();
                    }
                    if (Changed(nameof(item.WorkflowStateId)))
                    {
                        metadata["fromStateId"] = entry.Property(nameof(item.WorkflowStateId)).OriginalValue?.ToString();
                        metadata["toStateId"] = item.WorkflowStateId.ToString();
                    }
                    break;
                case Attachment attachment:
                    org = attachment.OrganizationId; resourceId = attachment.Id; resourceType = "Attachment";
                    action = added ? "ATTACHMENT_UPLOADED" : Changed(nameof(attachment.DeletedAt)) ? "ATTACHMENT_DELETED" : null;
                    break;
            }
            if (action is not null)
                AuditLogs.Add(AuditLog.Create(org, auditContext?.ActorUserId, action, resourceType!, resourceId,
                    auditContext?.IpAddress, auditContext?.UserAgent, DateTimeOffset.UtcNow, metadata));
        }
    }
}
