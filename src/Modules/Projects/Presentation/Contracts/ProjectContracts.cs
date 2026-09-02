using FluentValidation;
using SecureFlow.Modules.Projects.Application.Models;
using SecureFlow.Modules.Projects.Domain;

namespace SecureFlow.Modules.Projects.Presentation.Contracts;

public sealed record CreateProjectRequest(string Key, string Name, string? Description);
public sealed record UpdateProjectRequest(string Name, string? Description);
public sealed record AddProjectMemberRequest(Guid UserId, string Role);
public sealed record ChangeProjectMemberRoleRequest(string Role);

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .Must(key => IsValidKey(key))
            .WithMessage("Key, A-Z ile başlamalı ve 2-10 harf/rakam içermelidir.");
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(Project.DescriptionMaxLength);
    }

    private static bool IsValidKey(string key)
    {
        try
        {
            Project.NormalizeKey(key);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(request => request.Description).MaximumLength(Project.DescriptionMaxLength);
    }
}

public sealed class AddProjectMemberRequestValidator : AbstractValidator<AddProjectMemberRequest>
{
    public AddProjectMemberRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Role).Must(ProjectRoleParser.IsValid).WithMessage("Geçersiz Project role.");
    }
}

public sealed class ChangeProjectMemberRoleRequestValidator : AbstractValidator<ChangeProjectMemberRoleRequest>
{
    public ChangeProjectMemberRoleRequestValidator() =>
        RuleFor(request => request.Role).Must(ProjectRoleParser.IsValid).WithMessage("Geçersiz Project role.");
}

public static class ProjectRoleParser
{
    public static bool IsValid(string? role) => Parse(role) is not null;

    public static ProjectRole? Parse(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "MANAGER" => ProjectRole.Manager,
        "CONTRIBUTOR" => ProjectRole.Contributor,
        "VIEWER" => ProjectRole.Viewer,
        _ => null,
    };
}

public sealed record ProjectSummaryResponse(
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
    IReadOnlyList<string> Permissions)
{
    public static ProjectSummaryResponse From(ProjectSummary project) =>
        new(project.Id, project.OrganizationId, project.Key, project.Name, project.Description,
            project.CreatedBy, project.CreatedAt, project.UpdatedAt, project.ArchivedAt,
            project.ProjectRole, project.Permissions);
}

public sealed record ProjectMemberResponse(Guid UserId, string Email, string Role)
{
    public static ProjectMemberResponse From(ProjectMemberSummary member) =>
        new(member.UserId, member.Email, member.Role);
}

public sealed record ProjectDetailsResponse(
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
    IReadOnlyList<ProjectMemberResponse> Members)
{
    public static ProjectDetailsResponse From(ProjectDetails project) =>
        new(project.Id, project.OrganizationId, project.Key, project.Name, project.Description,
            project.CreatedBy, project.CreatedAt, project.UpdatedAt, project.ArchivedAt,
            project.ProjectRole, project.Permissions,
            project.Members.Select(ProjectMemberResponse.From).ToArray());
}
