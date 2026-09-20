using FluentValidation.TestHelper;
using JobRadar.Application.Features.JobSearch.Queries;

namespace JobRadar.Application.UnitTests.Features.JobSearch.Queries;

public class SearchJobsQueryValidatorTests
{
    private readonly SearchJobsQueryValidator _validator;

    public SearchJobsQueryValidatorTests()
    {
        _validator = new SearchJobsQueryValidator();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_HaveError_When_QueryIsEmpty(string query)
    {
        var model = new SearchJobsQuery(query);
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Query);
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
    public void Should_NotHaveError_When_Valid()
    {
        var model = new SearchJobsQuery("Developer", Page: 1, PageSize: 50);
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
