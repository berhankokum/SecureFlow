using FluentValidation;

namespace SecureFlow.Modules.Identity.Presentation.Contracts;

public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
