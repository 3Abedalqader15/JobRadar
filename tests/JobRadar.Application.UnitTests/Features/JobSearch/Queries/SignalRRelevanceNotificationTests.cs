using FluentAssertions;
using JobRadar.Application.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Application.UnitTests.Features.JobSearch.Queries;

public class SignalRRelevanceNotificationTests
{
    [Fact]
    public void ComputeMatchingGroups_IncludesAllEssentialAttributes()
    {
        // Arrange
        bool isRemote = true;
        string location = "Dubai, UAE";
        EmploymentType empType = EmploymentType.FullTime;
        ExperienceLevel expLevel = ExperienceLevel.Senior;
        var skills = new[] { "dotnet", "angular" };

        // Act
        var groups = JobRelevanceGroups.ComputeMatchingGroups(isRemote, location, empType, expLevel, skills);

        // Assert
        groups.Should().Contain("grp:all");
        groups.Should().Contain("grp:remote");
        groups.Should().Contain("grp:loc:dubai, uae");
        groups.Should().Contain("grp:emp:FullTime");
        groups.Should().Contain("grp:exp:Senior");
        groups.Should().Contain("grp:skill:dotnet");
        groups.Should().Contain("grp:skill:angular");
    }

    [Fact]
    public void ComputeMatchingGroups_DoesNotIncludeRemote_WhenJobIsNotRemote()
    {
        // Arrange
        bool isRemote = false;
        string location = "London";
        EmploymentType empType = EmploymentType.Contract;
        ExperienceLevel expLevel = ExperienceLevel.MidLevel;

        // Act
        var groups = JobRelevanceGroups.ComputeMatchingGroups(isRemote, location, empType, expLevel);

        // Assert
        groups.Should().NotContain("grp:remote");
        groups.Should().Contain("grp:loc:london");
        groups.Should().Contain("grp:emp:Contract");
        groups.Should().Contain("grp:exp:MidLevel");
        groups.Should().NotContain("grp:exp:Senior");
    }
}
