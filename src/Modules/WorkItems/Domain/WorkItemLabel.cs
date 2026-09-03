using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Domain;

public sealed class WorkItemLabel
{
    private WorkItemLabel() { }

    public Guid WorkItemId { get; private set; }
    public Guid LabelId { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public static WorkItemLabel Create(WorkItem workItem, Label label, Guid assignedBy, DateTimeOffset now)
    {
        if (workItem.IsArchived)
        {
            throw new DomainException("Archive edilmiş WorkItem değiştirilemez.");
        }

        if (workItem.ProjectId != label.ProjectId || workItem.OrganizationId != label.OrganizationId)
        {
            throw new DomainException("Label ve WorkItem aynı Project ve organization içinde olmalıdır.");
        }

        if (assignedBy == Guid.Empty) throw new ArgumentException("AssignedBy cannot be empty.", nameof(assignedBy));
        return new WorkItemLabel
        {
            WorkItemId = workItem.Id, LabelId = label.Id, AssignedBy = assignedBy, AssignedAt = now,
        };
    }
}
