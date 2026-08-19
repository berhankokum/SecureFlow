using FluentValidation;

namespace SecureFlow.Modules.Identity.Presentation.Contracts;

public sealed record ConfirmEmailRequest(Guid UserId, string Code);

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}
