using FluentValidation.TestHelper;
using JobRadar.Application.Features.JobSearch.Queries;
using JobRadar.Domain.Enums;

namespace JobRadar.Application.UnitTests.Features.JobSearch.Queries;

public class SearchJobsQueryValidatorTests
{
    private readonly SearchJobsQueryValidator _validator;

    public SearchJobsQueryValidatorTests()
    {
        _validator = new SearchJobsQueryValidator();
    }

    [Fact]
    public void Should_HaveError_When_PageIsLessThanOne()
    {
        var model = new SearchJobsQuery("Developer", Page: 0);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Should_HaveError_When_PageSizeIsGreaterThan50()
    {
        var model = new SearchJobsQuery("Developer", PageSize: 51);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Should_HaveError_When_SalaryMinIsGreaterThanSalaryMax()
    {
        var model = new SearchJobsQuery("Developer", SalaryMin: 100000m, SalaryMax: 80000m);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SalaryMin);
    }

    [Fact]
    public void Should_NotHaveError_When_SalaryMinIsLessThanOrEqualToSalaryMax()
    {
        var model = new SearchJobsQuery("Developer", SalaryMin: 50000m, SalaryMax: 100000m);
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.SalaryMin);
    }

    [Fact]
    public void Should_HaveError_When_SalaryIsNegative()
    {
        var model = new SearchJobsQuery("Developer", SalaryMin: -500m);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SalaryMin);
    }

    [Fact]
    public void Should_HaveError_When_DatePostedIsInvalidEnum()
    {
        var model = new SearchJobsQuery("Developer", DatePosted: (DatePostedFilter)999);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DatePosted);
    }

    [Fact]
    public void Should_HaveError_When_SortByIsInvalidEnum()
    {
        var model = new SearchJobsQuery("Developer", SortBy: (JobSortOption)999);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void Should_HaveError_When_SkillsExceeds10()
    {
        var skills = Enumerable.Range(1, 11).Select(i => $"skill{i}").ToList();
        var model = new SearchJobsQuery("Developer", Skills: skills);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Skills);
    }

    private static readonly EmploymentType[] ValidEmploymentTypes = [EmploymentType.FullTime];
    private static readonly ExperienceLevel[] ValidExperienceLevels = [ExperienceLevel.Senior];
    private static readonly string[] ValidSkills = ["csharp", "dotnet", "angular"];

    [Fact]
    public void Should_NotHaveError_When_ValidWithAllNewFilters()
    {
        var model = new SearchJobsQuery(
            Query: "Developer",
            Location: "Remote",
            IsRemote: true,
            EmploymentTypes: ValidEmploymentTypes,
            ExperienceLevels: ValidExperienceLevels,
            SalaryMin: 80000m,
            SalaryMax: 150000m,
            Skills: ValidSkills,
            DatePosted: DatePostedFilter.PastWeek,
            SortBy: JobSortOption.SalaryDescending,
            Page: 1,
            PageSize: 25
        );
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
