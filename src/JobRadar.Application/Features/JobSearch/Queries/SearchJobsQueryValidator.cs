using FluentValidation;

namespace JobRadar.Application.Features.JobSearch.Queries;

public sealed class SearchJobsQueryValidator : AbstractValidator<SearchJobsQuery>
{
    public SearchJobsQueryValidator()
    {
        RuleFor(x => x.Query)
            .MaximumLength(500).WithMessage("Search query cannot exceed 500 characters.");

        RuleFor(x => x.Location)
            .MaximumLength(200).WithMessage("Location cannot exceed 200 characters.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("PageSize must be between 1 and 50.");

        RuleFor(x => x.SalaryMin)
            .GreaterThanOrEqualTo(0).WithMessage("SalaryMin must be greater than or equal to 0.")
            .When(x => x.SalaryMin.HasValue);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(0).WithMessage("SalaryMax must be greater than or equal to 0.")
            .When(x => x.SalaryMax.HasValue);

        RuleFor(x => x.SalaryMin)
            .LessThanOrEqualTo(x => x.SalaryMax!.Value)
            .WithMessage("SalaryMin must be less than or equal to SalaryMax.")
            .When(x => x.SalaryMin.HasValue && x.SalaryMax.HasValue);

        RuleFor(x => x.DatePosted)
            .IsInEnum().WithMessage("DatePosted must be a valid DatePostedFilter value.");

        RuleFor(x => x.SortBy)
            .IsInEnum().WithMessage("SortBy must be a valid JobSortOption value.");

        RuleFor(x => x.Skills)
            .Must(s => s == null || s.Count <= 10)
            .WithMessage("Skills filter cannot exceed 10 skills.");

        RuleForEach(x => x.Skills)
            .MaximumLength(50)
            .WithMessage("Each skill cannot exceed 50 characters.");
    }
}
