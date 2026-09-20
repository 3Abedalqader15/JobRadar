using FluentValidation;

namespace JobRadar.Application.Features.JobSearch.Queries;

public sealed class SearchJobsQueryValidator : AbstractValidator<SearchJobsQuery>
{
    public SearchJobsQueryValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Search query cannot be empty.")
            .MaximumLength(500).WithMessage("Search query cannot exceed 500 characters.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("PageSize must be at least 1.");
            
        // The handler clamped it, but we can also just reject it if it's over 50. 
        // User said: "reject or clamp anything above 50". Rejecting is fine in Validator.
        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(50).WithMessage("PageSize cannot exceed 50.");
    }
}
