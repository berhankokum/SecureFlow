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
    Guid WorkflowStateId,
    string WorkflowStateName,
    string WorkflowStateCategory,
    Guid ReporterId,
    string ReporterEmail,
    Guid? AssigneeId,
    string? AssigneeEmail,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<WorkItemLabelSummary> Labels,
    uint Version);

public sealed record WorkItemScope(Guid ProjectId, Guid OrganizationId);

public sealed record WorkflowStateSummary(Guid Id, string Name, string Category, int Position);

public sealed record WorkflowTransitionSummary(
    Guid Id,
    Guid FromStateId,
    Guid ToStateId,
    string? RequiredPermission);

public sealed record WorkflowSummary(
    Guid Id,
    Guid OrganizationId,
    string Name,
    IReadOnlyList<WorkflowStateSummary> States,
    IReadOnlyList<WorkflowTransitionSummary> Transitions);
