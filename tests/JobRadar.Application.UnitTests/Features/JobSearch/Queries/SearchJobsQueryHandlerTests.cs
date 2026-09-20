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
using System.Security.Cryptography;
using System.Text;

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
        var cacheJson = JsonSerializer.Serialize(cachedResult);
        
        var cacheKeyParams = new
        {
            query = "developer",
            location = (string?)null,
            employmentType = (EmploymentType?)null,
            experienceLevel = (ExperienceLevel?)null,
            page = 1,
            pageSize = 10
        };
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(cacheKeyParams, new JsonSerializerOptions 
        { 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        string resultsCacheKey = "search:results:" + Convert.ToHexString(SHA256.HashData(jsonBytes));

        _mockCache.Setup(c => c.GetAsync(resultsCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cacheJson));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Cached Dev");

        _mockEmbeddingService.Verify(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockJobRepo.Verify(r => r.SearchSemanticAsync(It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<EmploymentType?>(), It.IsAny<ExperienceLevel?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _mockJobRepo.Setup(r => r.SearchSemanticAsync(fakeEmbedding, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((jobs, scores, 1));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);

        _mockEmbeddingService.Verify(s => s.GenerateAsync("Developer", It.IsAny<CancellationToken>()), Times.Once);
        _mockJobRepo.Verify(r => r.SearchSemanticAsync(fakeEmbedding, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
        
        // Ensure cache set was called twice (embedding + results)
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void TwoSearches_WithDifferentFilters_ProduceDifferentCacheKeys()
    {
        // Since we can't easily test the private cache key generation directly,
        // we can observe it via the mock arguments.
        
        // Arrange
        var query1 = new SearchJobsQuery("Developer", Page: 1);
        var query2 = new SearchJobsQuery("Developer", Page: 2);
        
        var keys = new List<string>();
        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!)
            .Callback<string, CancellationToken>((key, ct) => keys.Add(key));
            
        _mockEmbeddingService.Setup(s => s.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0f });
        _mockJobRepo.Setup(r => r.SearchSemanticAsync(It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<EmploymentType?>(), It.IsAny<ExperienceLevel?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Job>(), new double[0], 0));

        // Act
        _handler.Handle(query1, CancellationToken.None).GetAwaiter().GetResult();
        _handler.Handle(query2, CancellationToken.None).GetAwaiter().GetResult();

        // Assert
        // keys will contain [resultsKey1, embeddingKey1, resultsKey2, embeddingKey2]
        var resultsKey1 = keys[0];
        var embeddingKey1 = keys[1];
        var resultsKey2 = keys[2];
        var embeddingKey2 = keys[3];
        
        // Results keys should differ because page is different
        resultsKey1.Should().NotBe(resultsKey2);
        
        // Embedding keys should be identical because query text is the same
        embeddingKey1.Should().Be(embeddingKey2);
    }
}
