using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Search;
using JobRadar.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobRadar.Application.UnitTests.Features.JobSearch.Queries;

public class QueryUnderstandingServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<QueryUnderstandingService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public QueryUnderstandingServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:Model"] = "gemini-3.5-flash-lite",
                ["SearchRelevance:EnableLlmAmbiguousQueryExpansion"] = "true",
                ["SearchRelevance:AmbiguousQueryWordThreshold"] = "3"
            })
            .Build();

        _mockCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<QueryUnderstandingService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        // Return a dummy HttpClient for factory
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
    }

    [Fact]
    public async Task UnderstandQueryAsync_ExpandsDotNetSynonyms_WithoutGemini()
    {
        // Arrange
        var service = new QueryUnderstandingService(
            _mockHttpClientFactory.Object,
            _configuration,
            _mockCache.Object,
            _mockLogger.Object);

        // Act
        var result = await service.UnderstandQueryAsync(".NET Engineer");

        // Assert
        result.OriginalQuery.Should().Be(".NET Engineer");
        result.ExpandedTerms.Should().Contain("c#");
        result.ExpandedTerms.Should().Contain("dotnet");
        result.ExpandedTerms.Should().Contain("asp.net");
        result.InferredIsRemote.Should().BeNull();
    }

    [Fact]
    public async Task UnderstandQueryAsync_DetectsRemoteIntent()
    {
        // Arrange
        var service = new QueryUnderstandingService(
            _mockHttpClientFactory.Object,
            _configuration,
            _mockCache.Object,
            _mockLogger.Object);

        // Act
        var result = await service.UnderstandQueryAsync("Work from home React developer");

        // Assert
        result.InferredIsRemote.Should().BeTrue();
        result.ExpandedTerms.Should().Contain("reactjs");
    }

    [Fact]
    public async Task UnderstandQueryAsync_ReturnsCachedResult_WhenPresent()
    {
        // Arrange
        var cached = new QueryUnderstandingResult(
            OriginalQuery: "golang",
            NormalizedQuery: "golang",
            ExpandedTerms: new[] { "go" },
            InferredIsRemote: null,
            InferredSkills: new[] { "go" },
            IsAmbiguousNaturalLanguage: false);

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached));

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var service = new QueryUnderstandingService(
            _mockHttpClientFactory.Object,
            _configuration,
            _mockCache.Object,
            _mockLogger.Object);

        // Act
        var result = await service.UnderstandQueryAsync("golang");

        // Assert
        result.OriginalQuery.Should().Be("golang");
        result.ExpandedTerms.Should().Contain("go");
    }

    [Fact]
    public async Task UnderstandQueryAsync_MarksConversationalQueries_AsAmbiguous()
    {
        // Arrange
        var service = new QueryUnderstandingService(
            _mockHttpClientFactory.Object,
            _configuration,
            _mockCache.Object,
            _mockLogger.Object);

        // Act
        var result = await service.UnderstandQueryAsync("looking for an entry level position for someone who loves coding python");

        // Assert
        result.IsAmbiguousNaturalLanguage.Should().BeTrue();
        result.ExpandedTerms.Should().Contain("django");
        result.ExpandedTerms.Should().Contain("fastapi");
    }
}
