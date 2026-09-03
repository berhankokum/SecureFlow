using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Application.Models;

public sealed record WorkItemSummary(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    long Number,
    string Key,
    WorkItemType Type,
    string Title,
    string? Description,
    WorkItemPriority Priority,
    Guid ReporterId,
    string ReporterEmail,
    Guid? AssigneeId,
    string? AssigneeEmail,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<WorkItemLabelSummary> Labels);

public sealed record WorkItemScope(Guid ProjectId, Guid OrganizationId);
