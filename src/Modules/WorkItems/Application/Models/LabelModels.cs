using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Application.Models;

public sealed record LabelSummary(
    Guid Id, Guid OrganizationId, Guid ProjectId, string Name, Guid CreatedBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static LabelSummary From(Label label) => new(
        label.Id, label.OrganizationId, label.ProjectId, label.Name,
        label.CreatedBy, label.CreatedAt, label.UpdatedAt);
}

public sealed record WorkItemLabelSummary(Guid Id, string Name, Guid AssignedBy, DateTimeOffset AssignedAt);
