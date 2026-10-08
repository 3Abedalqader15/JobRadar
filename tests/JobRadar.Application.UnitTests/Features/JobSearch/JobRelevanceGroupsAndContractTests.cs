using FluentAssertions;
using JobRadar.Application.Common;
using JobRadar.Domain.Enums;
using Xunit;

namespace JobRadar.Application.UnitTests.Features.JobSearch;

public class JobRelevanceGroupsAndContractTests
{
    private static readonly string[] CanonicalSkills = { "C#", "Azure" };
    private static readonly EmploymentType[] CanonicalEmployment = { EmploymentType.FullTime, EmploymentType.Contract };
    private static readonly ExperienceLevel[] CanonicalExperience = { ExperienceLevel.Senior };

    [Fact]
    public void Tokenization_AlignsWithMinTokenLengthConstant()
    {
        JobRelevanceGroups.MinTokenLength.Should().Be(3);

        // A title with short tokens (e.g. "IT", "On") and valid tokens ("Lead", "Dev")
        var matching = JobRelevanceGroups.ComputeMatchingGroups(
            isRemote: false,
            location: null,
            employmentType: EmploymentType.FullTime,
            experienceLevel: ExperienceLevel.Senior,
            skills: null,
            title: "IT Lead On Dev");

        // "it" and "on" have length 2 < MinTokenLength (3) and must be excluded
        matching.Should().NotContain("grp:skill:it");
        matching.Should().NotContain("grp:skill:on");

        // "lead" and "dev" have length >= 3 and must be included
        matching.Should().Contain("grp:skill:lead");
        matching.Should().Contain("grp:skill:dev");

        // Query tokenization in ComputeCriteriaGroups must behave identically
        var criteria = JobRelevanceGroups.ComputeCriteriaGroups(
            query: "IT Lead On Dev",
            location: null,
            isRemote: null,
            employmentTypes: null,
            experienceLevels: null,
            skills: null);

        criteria.Should().NotContain("grp:skill:it");
        criteria.Should().NotContain("grp:skill:on");
        criteria.Should().Contain("grp:skill:lead");
        criteria.Should().Contain("grp:skill:dev");
    }

    [Fact]
    public void ComputeCriteriaGroups_WhenNoFilters_ReturnsOnlyGrpAll()
    {
        var groups = JobRelevanceGroups.ComputeCriteriaGroups(
            query: null,
            location: null,
            isRemote: null,
            employmentTypes: null,
            experienceLevels: null,
            skills: null);

        groups.Should().BeEquivalentTo(new[] { "grp:all" });
    }

    [Fact]
    public void ComputeCriteriaGroups_WhenFiltersActive_DoesNotContainGrpAll()
    {
        var groups = JobRelevanceGroups.ComputeCriteriaGroups(
            query: "Frontend",
            location: "Amman",
            isRemote: true,
            employmentTypes: new[] { EmploymentType.FullTime },
            experienceLevels: new[] { ExperienceLevel.MidLevel },
            skills: new[] { "Angular" });

        groups.Should().NotContain("grp:all");
        groups.Should().Contain("grp:remote");
        groups.Should().Contain("grp:loc:amman");
        groups.Should().Contain("grp:emp:FullTime");
        groups.Should().Contain("grp:exp:MidLevel");
        groups.Should().Contain("grp:skill:angular");
        groups.Should().Contain("grp:skill:frontend");
    }

    [Fact]
    public void ContractTest_CanonicalCriteria_ProducesExpectedGroupKeys()
    {
        // This test serves as the authoritative contract specification between backend and frontend.
        var groups = JobRelevanceGroups.ComputeCriteriaGroups(
            query: "Senior .NET",
            location: "Dubai",
            isRemote: true,
            employmentTypes: CanonicalEmployment,
            experienceLevels: CanonicalExperience,
            skills: CanonicalSkills);

        var expectedKeys = new HashSet<string>
        {
            "grp:remote",
            "grp:loc:dubai",
            "grp:emp:FullTime",
            "grp:emp:Contract",
            "grp:exp:Senior",
            "grp:skill:c#",
            "grp:skill:azure",
            "grp:skill:senior",
            "grp:skill:net"
        };

        groups.Should().BeEquivalentTo(expectedKeys);
    }
}
