using FluentAssertions;
using JobRadar.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features.JobSearch;

public class JobHubSecurityTests
{
    private readonly Mock<ILogger<JobHub>> _mockLogger = new();
    private readonly Mock<IHubCallerClients> _mockClients = new();
    private readonly Mock<IGroupManager> _mockGroups = new();
    private readonly Mock<HubCallerContext> _mockContext = new();

    public JobHubSecurityTests()
    {
        _mockContext.Setup(c => c.ConnectionId).Returns("conn_test_" + Guid.NewGuid());
    }

    [Theory]
    [InlineData("grp:all", true)]
    [InlineData("grp:remote", true)]
    [InlineData("grp:loc:amman", true)]
    [InlineData("grp:emp:fulltime", true)]
    [InlineData("grp:exp:midlevel", true)]
    [InlineData("grp:skill:c#", true)]
    [InlineData("grp:skill:dotnet", true)]
    [InlineData("grp:skill:angular", true)]
    [InlineData("admin", false)]
    [InlineData("all", false)]
    [InlineData("grp:", false)]
    [InlineData("grp:unknown:foo", false)]
    [InlineData("grp:hacker_channel", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidGroupName_EnforcesStrictNamespaceAndPattern(string? groupName, bool expected)
    {
        var result = JobHub.IsValidGroupName(groupName);
        result.Should().Be(expected);
    }

    [Fact]
    public void IsValidGroupName_RejectsNamesLongerThan100Chars()
    {
        var longGroup = "grp:loc:" + new string('a', 105);
        JobHub.IsValidGroupName(longGroup).Should().BeFalse();
    }

    [Fact]
    public async Task JoinCriteriaGroup_RejectsInvalidGroupName_AndDoesNotAddToGroup()
    {
        var hub = new JobHub(_mockLogger.Object)
        {
            Context = _mockContext.Object,
            Groups = _mockGroups.Object,
            Clients = _mockClients.Object
        };

        await hub.JoinCriteriaGroup("malicious_admin_group");

        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateCriteriaSubscription_EnforcesPerCallCapOf30()
    {
        var hub = new JobHub(_mockLogger.Object)
        {
            Context = _mockContext.Object,
            Groups = _mockGroups.Object,
            Clients = _mockClients.Object
        };

        // Create 40 valid skill groups
        var groups = Enumerable.Range(1, 40).Select(i => $"grp:skill:skill{i}").ToList();

        await hub.UpdateCriteriaSubscription(null, groups);

        // Should only add at most 30 groups per call
        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.AtMost(30));
    }
}
