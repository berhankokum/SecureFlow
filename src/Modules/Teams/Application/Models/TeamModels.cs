namespace SecureFlow.Modules.Teams.Application.Models;

public sealed record TeamSummary(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt);

public sealed record TeamMemberSummary(
    Guid UserId,
    string Email,
    DateTimeOffset AddedAt,
    Guid AddedBy);

public sealed record TeamDetails(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<TeamMemberSummary> Members);
