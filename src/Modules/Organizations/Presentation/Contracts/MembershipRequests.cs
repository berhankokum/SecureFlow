using FluentValidation;
using SecureFlow.Modules.Organizations.Domain;

namespace SecureFlow.Modules.Organizations.Presentation.Contracts;

public sealed record CreateInvitationRequest(string Email, string Role = "MEMBER");

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();

        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(OrganizationRoleParser.IsValid)
            .WithMessage("Rol OWNER, ADMIN veya MEMBER olmalıdır.");
    }
}

public sealed record ChangeMemberRoleRequest(string Role);

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(OrganizationRoleParser.IsValid)
            .WithMessage("Rol OWNER, ADMIN veya MEMBER olmalıdır.");
    }
}

public static class OrganizationRoleParser
{
    public static bool IsValid(string role) =>
        !int.TryParse(role, out _)
        && Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed);

    public static OrganizationRole Parse(string role) =>
        Enum.Parse<OrganizationRole>(role, ignoreCase: true);
}
