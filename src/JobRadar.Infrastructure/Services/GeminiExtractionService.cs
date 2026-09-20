using System.Net.Http.Headers;
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

/// <summary>
/// Uses Gemini Flash (<c>gemini-3.7-flash</c>) via the REST API with structured-output
/// (responseSchema) to extract job data from raw post text.
/// </summary>
public sealed class GeminiExtractionService : ILlmExtractionService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiExtractionService> _logger;
    private readonly ResiliencePipeline _pipeline;

    private const string Model = "gemini-3.7-flash";
    private const float ConfidenceThreshold = 0.60f;

    // Cached serialization options (CA1869)
    private static readonly JsonSerializerOptions _camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions _snakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // JSON schema sent to Gemini as the responseSchema
    private static readonly JsonDocument _responseSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "is_job_posting":   { "type": "boolean" },
            "confidence":       { "type": "number"  },
            "title":            { "type": "string"  },
            "company_name":     { "type": "string"  },
            "location":         { "type": "string"  },
            "is_remote":        { "type": "boolean" },
            "employment_type":  { "type": "string", "enum": ["FullTime","PartTime","Freelance","Internship","Contract","Temporary"] },
            "experience_level": { "type": "string", "enum": ["Internship","EntryLevel","MidLevel","Senior","Lead","Manager","Director","Executive"] },
            "skills_required":  { "type": "array", "items": { "type": "string" } },
            "salary_min":       { "type": "number"  },
            "salary_max":       { "type": "number"  },
            "salary_currency":  { "type": "string"  },
            "external_apply_url": { "type": "string" }
          },
          "required": ["is_job_posting","confidence","title","company_name","is_remote","employment_type","experience_level","skills_required"]
        }
        """);

    private static readonly string _systemPrompt =
        "You are a structured data extraction assistant. " +
        "Analyze the provided text and determine if it is a job posting. " +
        "If it is, extract the fields accurately. " +
        "Set is_job_posting=false and confidence<0.6 if the content is not a real job posting. " +
        "Return only the JSON object matching the schema — no markdown, no explanation.";

    public GeminiExtractionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiExtractionService> logger)
    {
        _http = httpClientFactory.CreateClient("Gemini");
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Missing configuration key: Gemini:ApiKey");
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
    }

    public async Task<JobExtractionResult?> ExtractJobAsync(
        string rawText,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={_apiKey}";

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
                    parts = new[] { new { text = rawText } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = _responseSchema.RootElement
            }
        };

        var json = JsonSerializer.Serialize(requestBody, _camelCaseOptions);

        GeminiExtractedRaw? raw = null;

        await _pipeline.ExecuteAsync(async ct =>
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini API returned {Status}: {Error}", response.StatusCode, err);
                response.EnsureSuccessStatusCode();
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var geminiResponse = await JsonSerializer.DeserializeAsync<GeminiApiResponse>(stream, cancellationToken: ct);

            var text = geminiResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;
            if (text is null)
            {
                _logger.LogWarning("Gemini returned empty extraction result.");
                return;
            }

            raw = JsonSerializer.Deserialize<GeminiExtractedRaw>(text, _snakeCaseOptions);
        }, cancellationToken);

        if (raw is null) return null;

        if (!raw.IsJobPosting || raw.Confidence < ConfidenceThreshold)
        {
            _logger.LogInformation(
                "Content rejected: IsJobPosting={IsJob}, Confidence={Conf:F2}",
                raw.IsJobPosting, raw.Confidence);
            return null;
        }

        return new JobExtractionResult
        {
            IsJobPosting     = raw.IsJobPosting,
            Confidence       = raw.Confidence,
            Title            = raw.Title ?? string.Empty,
            CompanyName      = raw.CompanyName ?? string.Empty,
            Location         = raw.Location,
            IsRemote         = raw.IsRemote,
            EmploymentType   = raw.EmploymentType ?? "FullTime",
            ExperienceLevel  = raw.ExperienceLevel ?? "MidLevel",
            SkillsRequired   = raw.SkillsRequired ?? new List<string>(),
            SalaryMin        = raw.SalaryMin,
            SalaryMax        = raw.SalaryMax,
            SalaryCurrency   = raw.SalaryCurrency,
            ExternalApplyUrl = raw.ExternalApplyUrl
        };
    }

    // ── Private Gemini response models ──────────────────────────────────────

    private sealed class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart>? Parts { get; set; }
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GeminiExtractedRaw
    {
        [JsonPropertyName("is_job_posting")]
        public bool IsJobPosting { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("is_remote")]
        public bool IsRemote { get; set; }

        [JsonPropertyName("employment_type")]
        public string? EmploymentType { get; set; }

        [JsonPropertyName("experience_level")]
        public string? ExperienceLevel { get; set; }

        [JsonPropertyName("skills_required")]
        public List<string>? SkillsRequired { get; set; }

        [JsonPropertyName("salary_min")]
        public decimal? SalaryMin { get; set; }

        [JsonPropertyName("salary_max")]
        public decimal? SalaryMax { get; set; }

        [JsonPropertyName("salary_currency")]
        public string? SalaryCurrency { get; set; }

        [JsonPropertyName("external_apply_url")]
        public string? ExternalApplyUrl { get; set; }
    }
}
