namespace SecureFlow.Modules.Projects.Application.Models;

public sealed record ProjectSummary(
    Guid Id,
    Guid OrganizationId,
    string Key,
    string Name,
    string? Description,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    string? ProjectRole,
    IReadOnlyList<string> Permissions);

public sealed record ProjectMemberSummary(Guid UserId, string Email, string Role);

public sealed record ProjectDetails(
    Guid Id,
    Guid OrganizationId,
    string Key,
    string Name,
    string? Description,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    string? ProjectRole,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<ProjectMemberSummary> Members);
