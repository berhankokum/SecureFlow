using FluentValidation;

namespace SecureFlow.Modules.Organizations.Presentation.Contracts;

public sealed record CreateOrganizationRequest(string Name, string? LogoUrl);

public sealed class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.LogoUrl)
            .MaximumLength(2048)
            .Must(BeValidHttpUrl)
            .When(request => !string.IsNullOrWhiteSpace(request.LogoUrl))
            .WithMessage("LogoUrl geçerli bir HTTP veya HTTPS adresi olmalıdır.");
    }

    private static bool BeValidHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
