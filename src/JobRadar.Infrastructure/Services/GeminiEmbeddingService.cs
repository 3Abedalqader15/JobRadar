using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace JobRadar.Infrastructure.Services;

/// <summary>
/// Uses <c>gemini-embedding-2</c> via the REST API to generate embedding vectors.
/// Output dimensionality is fixed at 1536 to match the existing Job.Embedding column.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiEmbeddingService> _logger;
    private readonly ResiliencePipeline _pipeline;

    private const string Model = "gemini-embedding-2";
    private const int OutputDimensionality = 1536;

    public GeminiEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiEmbeddingService> logger)
    {
        _http = httpClientFactory.CreateClient("Gemini");
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Missing configuration key: Gemini:ApiKey");
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            })
            .Build();
    }

    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:embedContent?key={_apiKey}";

        // For job retrieval, use asymmetric retrieval document format per Gemini docs
        var documentText = $"title: none | text: {text}";

        var requestBody = new
        {
            content = new
            {
                parts = new[] { new { text = documentText } }
            },
            output_dimensionality = OutputDimensionality
        };

        var json = JsonSerializer.Serialize(requestBody);
        float[]? values = null;

        await _pipeline.ExecuteAsync(async ct =>
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini Embeddings API returned {Status}: {Error}", response.StatusCode, err);
                response.EnsureSuccessStatusCode();
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var embedResponse = await JsonSerializer.DeserializeAsync<EmbedContentResponse>(
                stream, cancellationToken: ct);

            values = embedResponse?.Embedding?.Values?.ToArray()
                ?? throw new InvalidOperationException("Gemini returned an empty embedding.");
        }, cancellationToken);

        _logger.LogDebug("Generated embedding with {Dims} dimensions.", values!.Length);
        return values!;
    }

    // ── Private response models ──────────────────────────────────────────────

    private sealed class EmbedContentResponse
    {
        [JsonPropertyName("embedding")]
        public EmbeddingObject? Embedding { get; set; }
    }

    private sealed class EmbeddingObject
    {
        [JsonPropertyName("values")]
        public List<float>? Values { get; set; }
    }
}
