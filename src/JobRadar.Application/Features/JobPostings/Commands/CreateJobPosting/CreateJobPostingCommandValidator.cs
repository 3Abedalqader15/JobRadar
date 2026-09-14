using FluentValidation;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed class CreateJobPostingCommandValidator : AbstractValidator<CreateJobPostingCommand>
{
    public CreateJobPostingCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title must not exceed 300 characters.");

        RuleFor(x => x.Company)
            .NotEmpty().WithMessage("Company is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(50_000).WithMessage("Description must not exceed 50,000 characters.");

        RuleFor(x => x.Location)
            .MaximumLength(200).WithMessage("Location must not exceed 200 characters.")
            .When(x => x.Location is not null);

        RuleFor(x => x.SourceUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("SourceUrl must be a valid absolute URI.")
            .When(x => !string.IsNullOrEmpty(x.SourceUrl));

        RuleFor(x => x.SalaryMin)
            .GreaterThanOrEqualTo(0).WithMessage("SalaryMin must be >= 0.")
            .When(x => x.SalaryMin.HasValue);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(x => x.SalaryMin ?? 0)
            .WithMessage("SalaryMax must be >= SalaryMin.")
            .When(x => x.SalaryMax.HasValue);

        RuleFor(x => x.JobType)
            .IsInEnum().WithMessage("Invalid JobType value.");

        RuleFor(x => x.ExperienceLevel)
            .IsInEnum().WithMessage("Invalid ExperienceLevel value.");
    }
}
