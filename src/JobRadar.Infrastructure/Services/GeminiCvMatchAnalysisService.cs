using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace JobRadar.Infrastructure.Services;

public sealed class GeminiCvMatchAnalysisService : ICvMatchAnalysisService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiCvMatchAnalysisService> _logger;
    private readonly ResiliencePipeline _pipeline;

    private readonly string _model;
    private readonly string _systemPrompt;

    private static readonly JsonSerializerOptions _camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions _snakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonDocument _responseSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "match_score": { "type": "integer", "description": "Score 0 to 100 based on job requirements" },
            "missing_keywords": { "type": "array", "items": { "type": "string" } },
            "summary": { "type": "string", "description": "Short actionable justification" }
          },
          "required": ["match_score", "missing_keywords", "summary"]
        }
        """);

    public GeminiCvMatchAnalysisService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiCvMatchAnalysisService> logger)
    {
        _http = httpClientFactory.CreateClient("Gemini");
        _apiKey = configuration["Gemini:ApiKey"]
            ?? configuration["GEMINI_API_KEY"]
            ?? string.Empty;
        _model = configuration["Gemini:Model"] ?? "gemini-3.5-flash";
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Gemini:ApiKey is not configured. AI CV match analysis will be skipped.");
        }

        _systemPrompt =
            "You are an expert HR ATS and candidate matching analyst. " +
            "Analyze the applicant's CV text strictly against the provided Job Title and Description. " +
            "Score the match from 0 to 100 based on core skills, technologies, experience, and domain alignment. " +
            "Identify missing keywords/skills required by the job that are absent in the CV. " +
            "Provide a concise, objective summary (1-2 sentences) justifying the score. " +
            "Return only the JSON object adhering to the schema — no markdown, no explanation.";

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(3),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TaskCanceledException>()
                    .Handle<OperationCanceledException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Gemini CV match analysis attempt {Attempt} failed with {ExceptionType}: {Message}. Retrying after {Delay}ms...",
                        args.AttemptNumber + 1,
                        args.Outcome.Exception?.GetType().Name,
                        args.Outcome.Exception?.Message,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromMinutes(2),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<TaskCanceledException>()
                    .Handle<OperationCanceledException>()
            })
            .Build();
    }

    public async Task<CvMatchAnalysisResult?> AnalyzeMatchAsync(
        string cvText,
        string jobTitle,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Gemini:ApiKey is empty; skipping CV match analysis.");
            return null;
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var promptPayload = $"--- JOB TITLE ---\n{jobTitle}\n\n--- JOB DESCRIPTION ---\n{jobDescription}\n\n--- APPLICANT CV TEXT ---\n{cvText}";

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = _systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = promptPayload } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = _responseSchema.RootElement
            }
        };

        var json = JsonSerializer.Serialize(requestBody, _camelCaseOptions);

        try
        {
            var responseString = await _pipeline.ExecuteAsync(async state =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var response = await _http.SendAsync(request, state);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(state);
            }, cancellationToken);

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini returned no candidates for CV match analysis.");
                return null;
            }

            var textContent = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("Gemini returned an empty text part for CV match analysis.");
                return null;
            }

            var dto = JsonSerializer.Deserialize<GeminiCvMatchDto>(textContent, _snakeCaseOptions);
            if (dto is null)
            {
                _logger.LogWarning("Failed to deserialize Gemini CV match analysis output: {Text}", textContent);
                return null;
            }

            return new CvMatchAnalysisResult(
                Math.Clamp(dto.MatchScore, 0, 100),
                dto.MissingKeywords ?? Array.Empty<string>(),
                dto.Summary ?? string.Empty);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open for Gemini CV match service.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while analyzing CV match with Gemini API.");
            throw;
        }
    }

    private sealed class GeminiCvMatchDto
    {
        [JsonPropertyName("match_score")]
        public int MatchScore { get; set; }

        [JsonPropertyName("missing_keywords")]
        public string[]? MissingKeywords { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }
    }
}
