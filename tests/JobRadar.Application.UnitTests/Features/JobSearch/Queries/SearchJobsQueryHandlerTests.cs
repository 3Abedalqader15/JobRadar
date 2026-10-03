using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Features.JobSearch.Queries;
using JobRadar.Application.Models;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;

namespace JobRadar.Application.UnitTests.Features.JobSearch.Queries;

public class SearchJobsQueryHandlerTests
{
    private readonly Mock<IJobRepository> _mockJobRepo;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<SearchJobsQueryHandler>> _mockLogger;
    private readonly SearchJobsQueryHandler _handler;

    public SearchJobsQueryHandlerTests()
    {
        _mockJobRepo = new Mock<IJobRepository>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<SearchJobsQueryHandler>>();

        _handler = new SearchJobsQueryHandler(
            _mockJobRepo.Object,
            _mockEmbeddingService.Object,
            _mockCache.Object,
            _mockLogger.Object);
    }

    private static readonly JsonSerializerOptions CachedJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly float[] DefaultEmbedding = [0f];
    private static readonly double[] EmptyScores = [];
    private static readonly string[] DotnetSkill = ["dotnet"];
    private static readonly string[] AngularSkill = ["angular"];
    private static readonly string[] DotnetAngularSkills = ["dotnet", "angular"];
    private static readonly string[] AngularDotnetSkills = ["angular", "dotnet"];

    [Fact]
    public async Task Handle_ReturnsCachedResults_When_CacheHit()
    {
        // Arrange
        var query = new SearchJobsQuery("Developer", Page: 1, PageSize: 10);
        var cachedResult = new PagedResult<JobSearchResultDto>
        {
            Items = new[] { new JobSearchResultDto { Title = "Cached Dev" } },
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        };
        var cacheJson = JsonSerializer.Serialize(cachedResult, CachedJsonOptions);

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cacheJson));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Cached Dev");

        _mockEmbeddingService.Verify(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockJobRepo.Verify(r => r.SearchJobsAdvancedAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallsEmbeddingAndRepo_When_CacheMiss()
    {
        // Arrange
        var query = new SearchJobsQuery("Developer");
        var fakeEmbedding = new float[] { 0.1f, 0.2f };
        var fakeJob = Job.Create(Guid.NewGuid(), "New Dev", "Company", "Desc");
        var jobs = new List<Job> { fakeJob };
        var scores = new double[] { 0.9 };

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!); // Miss for both results and embedding

        _mockEmbeddingService.Setup(s => s.GenerateAsync("Developer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeEmbedding);

        _mockJobRepo.Setup(r => r.SearchJobsAdvancedAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((jobs, scores, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);

        _mockEmbeddingService.Verify(s => s.GenerateAsync("Developer", It.IsAny<CancellationToken>()), Times.Once);
        _mockJobRepo.Verify(r => r.SearchJobsAdvancedAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TwoSearches_WithDifferentFilters_ProduceDifferentCacheKeys()
    {
        // Arrange
        var query1 = new SearchJobsQuery("Developer", Page: 1);
        var query2 = new SearchJobsQuery("Developer", Page: 2);

        var keys = new List<string>();
        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!)
            .Callback<string, CancellationToken>((key, ct) => keys.Add(key));

        _mockEmbeddingService.Setup(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultEmbedding);
        _mockJobRepo.Setup(r => r.SearchJobsAdvancedAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Job>(), EmptyScores, 0));

        // Act
        await _handler.Handle(query1, CancellationToken.None);
        await _handler.Handle(query2, CancellationToken.None);

        // Assert
        var resultsKey1 = keys[0];
        var resultsKey2 = keys[2];

        // Results keys should differ because page is different
        resultsKey1.Should().NotBe(resultsKey2);
    }

    [Fact]
    public async Task TwoSearches_WithDifferentSalaryOrSkills_ProduceDifferentCacheKeys()
    {
        var query1 = new SearchJobsQuery("Developer", SalaryMin: 50000m, Skills: DotnetSkill);
        var query2 = new SearchJobsQuery("Developer", SalaryMin: 80000m, Skills: DotnetSkill);
        var query3 = new SearchJobsQuery("Developer", SalaryMin: 50000m, Skills: AngularSkill);

        var keys = new List<string>();
        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!)
            .Callback<string, CancellationToken>((key, ct) => keys.Add(key));

        _mockEmbeddingService.Setup(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultEmbedding);
        _mockJobRepo.Setup(r => r.SearchJobsAdvancedAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Job>(), EmptyScores, 0));

        // Act
        await _handler.Handle(query1, CancellationToken.None);
        await _handler.Handle(query2, CancellationToken.None);
        await _handler.Handle(query3, CancellationToken.None);

        var key1 = keys[0];
        var key2 = keys[2];
        var key3 = keys[4];

        key1.Should().NotBe(key2);
        key1.Should().NotBe(key3);
        key2.Should().NotBe(key3);
    }

    [Fact]
    public async Task TwoSearches_WithDifferentSkillOrder_ProduceIdenticalCacheKeys()
    {
        // Canonical sorting should ensure skill order does not invalidate cache
        var query1 = new SearchJobsQuery("Developer", Skills: DotnetAngularSkills);
        var query2 = new SearchJobsQuery("Developer", Skills: AngularDotnetSkills);

        var keys = new List<string>();
        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!)
            .Callback<string, CancellationToken>((key, ct) => keys.Add(key));

        _mockEmbeddingService.Setup(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultEmbedding);
        _mockJobRepo.Setup(r => r.SearchJobsAdvancedAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Job>(), EmptyScores, 0));

        // Act
        await _handler.Handle(query1, CancellationToken.None);
        await _handler.Handle(query2, CancellationToken.None);

        var key1 = keys[0];
        var key2 = keys[2];

        key1.Should().Be(key2);
    }
}
