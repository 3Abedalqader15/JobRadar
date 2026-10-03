using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Features.Crawlers.Commands.IngestCrawledJobs;
using JobRadar.Application.Models;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features.Crawlers.Commands.IngestCrawledJobs;

public class IngestCrawledJobsCommandHandlerTests
{
    private readonly Mock<IJobRepository> _jobRepositoryMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IJobEmbeddingQueue> _embeddingQueueMock = new();
    private readonly Mock<IJobRealtimeNotifier> _realtimeNotifierMock = new();
    private readonly Mock<ILogger<IngestCrawledJobsCommandHandler>> _loggerMock = new();
    private readonly Mock<IJobCrawlerProvider> _crawlerMock = new();

    public IngestCrawledJobsCommandHandlerTests()
    {
        _crawlerMock.SetupGet(c => c.ProviderName).Returns("MockCrawler");

        var testSource = Source.Create(
            "Crawler: MockCrawler",
            SourceType.CompanyCareersPage,
            "https://jobradar.io/crawlers/mockcrawler",
            null,
            15);

        _sourceRepositoryMock
            .Setup(s => s.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testSource);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task Handle_WhenSameExternalUrlSubmittedTwiceInBatch_DoesNotCreateDuplicateJob()
    {
        // Arrange
        const string duplicateUrl = "https://example.com/jobs/senior-net-dev";

        var discoveredJobs = new List<DiscoveredJobDto>
        {
            new(
                Title: "Senior .NET Developer",
                CompanyName: "Acme Corp",
                Description: "Exciting .NET role",
                Location: "Remote",
                IsRemote: true,
                EmploymentType: EmploymentType.FullTime,
                ExperienceLevel: ExperienceLevel.Senior,
                SalaryMin: 80000,
                SalaryMax: 120000,
                SalaryCurrency: "USD",
                ExternalApplyUrl: duplicateUrl,
                PostedAt: DateTime.UtcNow,
                Skills: new[] { "C#", ".NET", "PostgreSQL" },
                ProviderName: "MockCrawler"
            ),
            new(
                Title: "Senior .NET Developer (Duplicate Entry)",
                CompanyName: "Acme Corp",
                Description: "Same job posting received again",
                Location: "Remote",
                IsRemote: true,
                EmploymentType: EmploymentType.FullTime,
                ExperienceLevel: ExperienceLevel.Senior,
                SalaryMin: 80000,
                SalaryMax: 120000,
                SalaryCurrency: "USD",
                ExternalApplyUrl: duplicateUrl, // Same URL
                PostedAt: DateTime.UtcNow,
                Skills: new[] { "C#", ".NET" },
                ProviderName: "MockCrawler"
            )
        };

        _crawlerMock
            .Setup(c => c.CrawlJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredJobs);

        // Repository currently has no existing URLs in DB
        _jobRepositoryMock
            .Setup(r => r.GetExistingUrlsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _jobRepositoryMock
            .Setup(r => r.ExistsByTitleAndCompanyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new IngestCrawledJobsCommandHandler(
            new[] { _crawlerMock.Object },
            _jobRepositoryMock.Object,
            _sourceRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _embeddingQueueMock.Object,
            _realtimeNotifierMock.Object,
            _loggerMock.Object);

        // Act
        var result = await handler.Handle(new IngestCrawledJobsCommand(), CancellationToken.None);

        // Assert: Exactly 1 job should be created, duplicate should be filtered out
        result.Should().Be(1);

        _jobRepositoryMock.Verify(
            r => r.AddWithSkillsAsync(It.Is<Job>(j => j.ExternalApplyUrl == duplicateUrl), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _embeddingQueueMock.Verify(
            e => e.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _realtimeNotifierMock.Verify(
            n => n.NotifyJobCreatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<EmploymentType>(),
                It.IsAny<ExperienceLevel>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal?>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                duplicateUrl,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExternalUrlAlreadyExistsInRepository_SkipsJobCreation()
    {
        // Arrange
        const string existingUrl = "https://example.com/jobs/frontend-engineer";

        var discoveredJobs = new List<DiscoveredJobDto>
        {
            new(
                Title: "Frontend Engineer",
                CompanyName: "Tech Ltd",
                Description: "Angular frontend role",
                Location: "Berlin",
                IsRemote: false,
                EmploymentType: EmploymentType.FullTime,
                ExperienceLevel: ExperienceLevel.MidLevel,
                SalaryMin: 60000,
                SalaryMax: 80000,
                SalaryCurrency: "EUR",
                ExternalApplyUrl: existingUrl,
                PostedAt: DateTime.UtcNow,
                Skills: new[] { "Angular", "TypeScript" },
                ProviderName: "MockCrawler"
            )
        };

        _crawlerMock
            .Setup(c => c.CrawlJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(discoveredJobs);

        // DB already contains this URL
        _jobRepositoryMock
            .Setup(r => r.GetExistingUrlsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { existingUrl });

        var handler = new IngestCrawledJobsCommandHandler(
            new[] { _crawlerMock.Object },
            _jobRepositoryMock.Object,
            _sourceRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _embeddingQueueMock.Object,
            _realtimeNotifierMock.Object,
            _loggerMock.Object);

        // Act
        var result = await handler.Handle(new IngestCrawledJobsCommand(), CancellationToken.None);

        // Assert: 0 jobs created
        result.Should().Be(0);

        _jobRepositoryMock.Verify(
            r => r.AddWithSkillsAsync(It.IsAny<Job>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _embeddingQueueMock.Verify(
            e => e.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
