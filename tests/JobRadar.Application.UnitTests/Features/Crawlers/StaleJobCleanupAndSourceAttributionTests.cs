using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;
using JobRadar.Application.Messages;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using JobRadar.Workers.Jobs;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features.Crawlers;

public class StaleJobCleanupAndSourceAttributionTests
{
    private static readonly Guid WellKnownManualSourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CrawlerSourceId = Guid.NewGuid();

    [Fact]
    public async Task StaleJobCleanup_PurgesCrawledJobsOlderThan10Days_WhileStrictlyPreservingDirectPosts()
    {
        // Arrange
        var repositoryMock = new Mock<IJobRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<StaleJobCleanupJob>>();

        var now = DateTime.UtcNow;

        // 1. Crawled job, 15 days old -> SHOULD BE PURGED
        var oldCrawledJob = Job.Create(
            sourceId: CrawlerSourceId,
            title: "Old Crawled Job",
            companyName: "External Company",
            description: "Old listing",
            location: "Remote",
            isRemote: true,
            postedAt: now.AddDays(-15),
            rawPostId: Guid.NewGuid());

        // 2. Crawled job, 4 days old -> SHOULD BE KEPT
        var freshCrawledJob = Job.Create(
            sourceId: CrawlerSourceId,
            title: "Fresh Crawled Job",
            companyName: "External Company",
            description: "Fresh listing",
            location: "Remote",
            isRemote: true,
            postedAt: now.AddDays(-4),
            rawPostId: Guid.NewGuid());

        // 3. Direct platform post, 45 days old -> MUST BE PRESERVED INDEFINITELY
        var oldDirectPost = Job.Create(
            sourceId: WellKnownManualSourceId,
            title: "Senior .NET Lead",
            companyName: "Jordan Tech Hub",
            description: "Official direct posting",
            location: "Amman, Jordan",
            isRemote: false,
            postedAt: now.AddDays(-45),
            rawPostId: null,
            companyId: Guid.NewGuid());

        // 4. Direct platform post, 1 day old -> MUST BE PRESERVED
        var freshDirectPost = Job.Create(
            sourceId: WellKnownManualSourceId,
            title: "Junior QA Engineer",
            companyName: "Amman Solutions",
            description: "Official direct posting",
            location: "Amman, Jordan",
            isRemote: false,
            postedAt: now.AddDays(-1),
            rawPostId: null,
            companyId: Guid.NewGuid());

        var allJobs = new List<Job> { oldCrawledJob, freshCrawledJob, oldDirectPost, freshDirectPost };

        repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allJobs);

        var deletedJobs = new List<Job>();
        repositoryMock
            .Setup(r => r.DeleteAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .Callback<Job, CancellationToken>((j, _) => deletedJobs.Add(j))
            .Returns(Task.CompletedTask);

        var cleanupJob = new StaleJobCleanupJob(
            repositoryMock.Object,
            unitOfWorkMock.Object,
            publishEndpointMock.Object,
            loggerMock.Object);

        // Act
        await cleanupJob.ExecuteAsync(CancellationToken.None);

        // Assert
        // Only the 15-day-old crawled job must be deleted
        deletedJobs.Should().ContainSingle();
        deletedJobs[0].Id.Should().Be(oldCrawledJob.Id);

        // Direct posts must NEVER be deleted, regardless of age
        deletedJobs.Should().NotContain(j => j.Id == oldDirectPost.Id);
        deletedJobs.Should().NotContain(j => j.Id == freshDirectPost.Id);
        deletedJobs.Should().NotContain(j => j.Id == freshCrawledJob.Id);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishEndpointMock.Verify(p => p.Publish(It.Is<JobDeactivatedEvent>(e => e.JobId == oldCrawledJob.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetJobPostingsQueryHandler_WithSourceFilterDirect_ReturnsOnlyDirectPlatformPosts()
    {
        // Arrange
        var repositoryMock = new Mock<IJobRepository>();
        var currentUserServiceMock = new Mock<ICurrentUserService>();

        currentUserServiceMock.SetupGet(u => u.IsAdmin).Returns(true);
        currentUserServiceMock.SetupGet(u => u.IsHr).Returns(false);

        var directJob = Job.Create(
            sourceId: WellKnownManualSourceId,
            title: "Direct Posting",
            companyName: "Direct Employer LLC",
            description: "Direct job",
            postedAt: DateTime.UtcNow.AddDays(-2),
            rawPostId: null,
            companyId: Guid.NewGuid());

        var crawledJob = Job.Create(
            sourceId: CrawlerSourceId,
            title: "Crawled Job",
            companyName: "External Firm",
            description: "Crawled job",
            postedAt: DateTime.UtcNow.AddDays(-1),
            rawPostId: Guid.NewGuid());

        repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Job> { directJob, crawledJob });

        var handler = new GetJobPostingsQueryHandler(repositoryMock.Object, currentUserServiceMock.Object);

        // Act - filter: "direct"
        var directResult = await handler.Handle(new GetJobPostingsQuery(SourceFilter: "direct"), CancellationToken.None);

        // Assert
        directResult.Items.Should().ContainSingle();
        directResult.Items[0].Id.Should().Be(directJob.Id);
        directResult.Items[0].IsDirect.Should().BeTrue();
        directResult.Items[0].SourceName.Should().Be("JobRadar Direct");

        // Act - filter: "crawled"
        var crawledResult = await handler.Handle(new GetJobPostingsQuery(SourceFilter: "crawled"), CancellationToken.None);

        // Assert
        crawledResult.Items.Should().ContainSingle();
        crawledResult.Items[0].Id.Should().Be(crawledJob.Id);
        crawledResult.Items[0].IsDirect.Should().BeFalse();
    }
}
