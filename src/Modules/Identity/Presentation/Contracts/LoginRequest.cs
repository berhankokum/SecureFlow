using FluentValidation;

namespace SecureFlow.Modules.Identity.Presentation.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
    }
}
