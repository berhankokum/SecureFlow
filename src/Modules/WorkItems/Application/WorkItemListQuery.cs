using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Application;

public sealed class WorkItemListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public long? Number { get; init; }
    public Guid? Status { get; init; }
    public string? Type { get; init; }
    public string? Priority { get; init; }
    public Guid? Assignee { get; init; }
    public Guid? Reporter { get; init; }
    public Guid? Label { get; init; }
    public DateOnly? CreatedFrom { get; init; }
    public DateOnly? CreatedTo { get; init; }
    public DateOnly? DueFrom { get; init; }
    public DateOnly? DueTo { get; init; }
    public string Sort { get; init; } = "createdAt";
    public string Direction { get; init; } = "desc";

    public IQueryable<WorkItem> Apply(IQueryable<WorkItem> source, IQueryable<WorkItemLabel> labels)
    {
        if (Number is { } number) source = source.Where(item => item.Number == number);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search.Trim();
            source = source.Where(item => EF.Property<NpgsqlTsVector>(item, "SearchVector")
                .Matches(EF.Functions.PlainToTsQuery("simple", search)));
        }
        if (Status is { } status) source = source.Where(item => item.WorkflowStateId == status);
        if (Assignee is { } assignee) source = source.Where(item => item.AssigneeId == assignee);
        if (Reporter is { } reporter) source = source.Where(item => item.ReporterId == reporter);
        if (Label is { } label) source = source.Where(item => labels.Any(link => link.WorkItemId == item.Id && link.LabelId == label));
        if (Type is not null)
        {
            var type = Enum.Parse<WorkItemType>(Type, true);
            source = source.Where(item => item.Type == type);
        }
        if (Priority is not null)
        {
            var priority = Enum.Parse<WorkItemPriority>(Priority, true);
            source = source.Where(item => item.Priority == priority);
        }
        if (CreatedFrom is { } createdFrom)
        {
            var start = new DateTimeOffset(createdFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            source = source.Where(item => item.CreatedAt >= start);
        }
        if (CreatedTo is { } createdTo && createdTo < DateOnly.MaxValue)
        {
            var end = new DateTimeOffset(createdTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            source = source.Where(item => item.CreatedAt < end);
        }
        if (DueFrom is { } dueFrom) source = source.Where(item => item.DueDate >= dueFrom);
        if (DueTo is { } dueTo) source = source.Where(item => item.DueDate <= dueTo);

        var descending = Direction == "desc";
        var ordered = (Sort, descending) switch
        {
            ("updatedAt", true) => source.OrderByDescending(item => item.UpdatedAt),
            ("updatedAt", false) => source.OrderBy(item => item.UpdatedAt),
            ("dueDate", true) => source.OrderBy(item => item.DueDate == null).ThenByDescending(item => item.DueDate),
            ("dueDate", false) => source.OrderBy(item => item.DueDate == null).ThenBy(item => item.DueDate),
            ("priority", true) => source.OrderByDescending(item => item.Priority == WorkItemPriority.Critical ? 3 : item.Priority == WorkItemPriority.High ? 2 : item.Priority == WorkItemPriority.Medium ? 1 : 0),
            ("priority", false) => source.OrderBy(item => item.Priority == WorkItemPriority.Critical ? 3 : item.Priority == WorkItemPriority.High ? 2 : item.Priority == WorkItemPriority.Medium ? 1 : 0),
            (_, true) => source.OrderByDescending(item => item.CreatedAt),
            _ => source.OrderBy(item => item.CreatedAt),
        };
        return (descending ? ordered.ThenByDescending(item => item.Number) : ordered.ThenBy(item => item.Number))
            .Skip((Page - 1) * PageSize).Take(PageSize);
    }
}

public sealed class WorkItemListQueryValidator : AbstractValidator<WorkItemListQuery>
{
    public WorkItemListQueryValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 100000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(200);
        RuleFor(query => query.Number).GreaterThan(0).When(query => query.Number.HasValue);
        RuleFor(query => query.Sort).Must(value => value is "createdAt" or "updatedAt" or "dueDate" or "priority");
        RuleFor(query => query.Direction).Must(value => value is "asc" or "desc");
        RuleFor(query => query.Type).Must(value => value is null || value is "TASK" or "BUG" or "STORY");
        RuleFor(query => query.Priority).Must(value => value is null || value is "LOW" or "MEDIUM" or "HIGH" or "CRITICAL");
        RuleFor(query => query.CreatedTo).Must((query, value) => value is null || query.CreatedFrom is null || value >= query.CreatedFrom);
        RuleFor(query => query.DueTo).Must((query, value) => value is null || query.DueFrom is null || value >= query.DueFrom);
    }
}
