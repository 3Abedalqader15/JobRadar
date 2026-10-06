using FluentValidation;

namespace JobRadar.Application.Features.Companies.Commands.UpdateCompany;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Company slug is required.")
            .MaximumLength(100).WithMessage("Company slug must not exceed 100 characters.")
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must consist of lowercase letters, numbers, and hyphens (e.g. acme-corp).");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(2048).WithMessage("Logo URL must not exceed 2048 characters.")
            .When(x => !string.IsNullOrEmpty(x.LogoUrl));
    }
}
