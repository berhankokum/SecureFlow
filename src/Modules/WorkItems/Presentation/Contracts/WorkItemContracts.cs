using FluentValidation;
using SecureFlow.Modules.WorkItems.Application.Models;
using SecureFlow.Modules.WorkItems.Domain;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.WorkItems.Presentation.Contracts;

public sealed record CreateWorkItemRequest(
    string Type,
    string Title,
    string? Description,
    string Priority,
    DateOnly? DueDate);

public sealed record UpdateWorkItemRequest(
    string Type,
    string Title,
    string? Description,
    string Priority,
    DateOnly? DueDate);

public sealed class CreateWorkItemRequestValidator : AbstractValidator<CreateWorkItemRequest>
{
    public CreateWorkItemRequestValidator(IClock clock)
    {
        RuleFor(request => request.Type)
            .Must(WorkItemTypeParser.IsValid)
            .WithMessage("Geçersiz WorkItem type.");
        RuleFor(request => request.Title).NotEmpty().MaximumLength(WorkItem.TitleMaxLength);
        RuleFor(request => request.Description).MaximumLength(WorkItem.DescriptionMaxLength);
        RuleFor(request => request.Priority)
            .Must(WorkItemPriorityParser.IsValid)
            .WithMessage("Geçersiz WorkItem priority.");
        RuleFor(request => request.DueDate)
            .Must(dueDate => dueDate is null || dueDate >= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
            .WithMessage("DueDate geçmiş bir tarih olamaz.");
    }
}

public sealed class UpdateWorkItemRequestValidator : AbstractValidator<UpdateWorkItemRequest>
{
    public UpdateWorkItemRequestValidator()
    {
        RuleFor(request => request.Type)
            .Must(WorkItemTypeParser.IsValid)
            .WithMessage("Geçersiz WorkItem type.");
        RuleFor(request => request.Title).NotEmpty().MaximumLength(WorkItem.TitleMaxLength);
        RuleFor(request => request.Description).MaximumLength(WorkItem.DescriptionMaxLength);
        RuleFor(request => request.Priority)
            .Must(WorkItemPriorityParser.IsValid)
            .WithMessage("Geçersiz WorkItem priority.");
    }
}

public static class WorkItemTypeParser
{
    public static bool IsValid(string? value) => Parse(value) is not null;

    public static WorkItemType? Parse(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "TASK" => WorkItemType.Task,
        "BUG" => WorkItemType.Bug,
        "STORY" => WorkItemType.Story,
        _ => null,
    };
}

public static class WorkItemPriorityParser
{
    public static bool IsValid(string? value) => Parse(value) is not null;

    public static WorkItemPriority? Parse(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "LOW" => WorkItemPriority.Low,
        "MEDIUM" => WorkItemPriority.Medium,
        "HIGH" => WorkItemPriority.High,
        "CRITICAL" => WorkItemPriority.Critical,
        _ => null,
    };
}

public sealed record WorkItemResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string ProjectKey,
    long Number,
    string Key,
    string Type,
    string Title,
    string? Description,
    string Priority,
    Guid ReporterId,
    string ReporterEmail,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<WorkItemLabelSummary> Labels)
{
    public static WorkItemResponse From(WorkItemSummary workItem) =>
        new(
            workItem.Id,
            workItem.OrganizationId,
            workItem.ProjectId,
            workItem.ProjectKey,
            workItem.Number,
            workItem.Key,
            workItem.Type.ToString().ToUpperInvariant(),
            workItem.Title,
            workItem.Description,
            workItem.Priority.ToString().ToUpperInvariant(),
            workItem.ReporterId,
            workItem.ReporterEmail,
            workItem.DueDate,
            workItem.CreatedAt,
            workItem.UpdatedAt,
            workItem.ArchivedAt,
            workItem.Permissions,
            workItem.Labels);
}
