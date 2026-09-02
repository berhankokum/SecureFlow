using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Domain;

public sealed class WorkItem : AggregateRoot
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 10_000;

    private WorkItem()
    {
    }

    private WorkItem(
        Guid id,
        Guid organizationId,
        Guid projectId,
        long number,
        WorkItemType type,
        string title,
        string? description,
        WorkItemPriority priority,
        Guid reporterId,
        DateOnly? dueDate,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = RequireId(organizationId, nameof(organizationId));
        ProjectId = RequireId(projectId, nameof(projectId));
        Number = number > 0 ? number : throw new ArgumentOutOfRangeException(nameof(number));
        Type = type;
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Priority = priority;
        ReporterId = RequireId(reporterId, nameof(reporterId));
        DueDate = dueDate;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public long Number { get; private set; }

    public WorkItemType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public WorkItemPriority Priority { get; private set; }

    public Guid ReporterId { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static WorkItem Create(
        Guid organizationId,
        Guid projectId,
        long number,
        WorkItemType type,
        string title,
        string? description,
        WorkItemPriority priority,
        Guid reporterId,
        DateOnly? dueDate,
        DateOnly currentDate,
        DateTimeOffset createdAt)
    {
        EnsureDueDateIsNotPast(dueDate, currentDate);
        return new WorkItem(
            Guid.NewGuid(), organizationId, projectId, number, type, title,
            description, priority, reporterId, dueDate, createdAt);
    }

    public void Update(
        WorkItemType type,
        string title,
        string? description,
        WorkItemPriority priority,
        DateOnly? dueDate,
        DateOnly currentDate,
        DateTimeOffset updatedAt)
    {
        EnsureMutable();
        if (dueDate != DueDate)
        {
            EnsureDueDateIsNotPast(dueDate, currentDate);
        }

        Type = type;
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Priority = priority;
        DueDate = dueDate;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (IsArchived)
        {
            return;
        }

        ArchivedAt = archivedAt;
        UpdatedAt = archivedAt;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException("Id cannot be empty.", parameterName);

    private static string NormalizeTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var normalized = title.Trim();
        if (normalized.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Title cannot exceed {TitleMaxLength} characters.", nameof(title));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalized = description.Trim();
        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"Description cannot exceed {DescriptionMaxLength} characters.",
                nameof(description));
        }

        return normalized;
    }

    private static void EnsureDueDateIsNotPast(DateOnly? dueDate, DateOnly currentDate)
    {
        if (dueDate < currentDate)
        {
            throw new DomainException("DueDate geçmiş bir tarih olamaz.");
        }
    }

    private void EnsureMutable()
    {
        if (IsArchived)
        {
            throw new DomainException("Archive edilmiş WorkItem değiştirilemez.");
        }
    }
}
