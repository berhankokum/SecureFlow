using FluentValidation;
using SecureFlow.Modules.WorkItems.Domain;

namespace SecureFlow.Modules.WorkItems.Presentation.Contracts;

public sealed record LabelNameRequest(string Name);

public sealed class LabelNameRequestValidator : AbstractValidator<LabelNameRequest>
{
    public LabelNameRequestValidator()
    {
        RuleFor(request => request.Name).Must(Label.IsValidName)
            .WithMessage($"Label adı boş olamaz ve normalize edildiğinde en fazla {Label.NameMaxLength} karakter olabilir.");
    }
}
