using FluentValidation;

namespace SecureFlow.Modules.Identity.Presentation.Contracts;

public sealed record RegisterRequest(string Email, string Password);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
