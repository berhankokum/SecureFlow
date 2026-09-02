using FluentValidation;
using SecureFlow.Modules.Teams.Application.Models;
using SecureFlow.Modules.Teams.Domain;

namespace SecureFlow.Modules.Teams.Presentation.Contracts;

public sealed record CreateTeamRequest(string Name, string? Description);

public sealed record UpdateTeamRequest(string Name, string? Description);

public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Team.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(Team.DescriptionMaxLength);
    }
}

public sealed class UpdateTeamRequestValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Team.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(Team.DescriptionMaxLength);
    }
}

public sealed record TeamSummaryResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt)
{
    public static TeamSummaryResponse From(TeamSummary team) =>
        new(team.Id, team.OrganizationId, team.Name, team.Description, team.MemberCount,
            team.CreatedAt, team.UpdatedAt, team.ArchivedAt);
}

public sealed record TeamMemberResponse(
    Guid UserId,
    string Email,
    DateTimeOffset AddedAt,
    Guid AddedBy)
{
    public static TeamMemberResponse From(TeamMemberSummary member) =>
        new(member.UserId, member.Email, member.AddedAt, member.AddedBy);
}

public sealed record TeamDetailsResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<TeamMemberResponse> Members)
{
    public static TeamDetailsResponse From(TeamDetails team) =>
        new(team.Id, team.OrganizationId, team.Name, team.Description, team.CreatedAt,
            team.UpdatedAt, team.ArchivedAt, team.Members.Select(TeamMemberResponse.From).ToArray());
}
