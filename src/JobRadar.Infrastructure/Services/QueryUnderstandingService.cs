using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobRadar.Application.Common.Search;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace JobRadar.Infrastructure.Services;

public sealed class QueryUnderstandingService : IQueryUnderstandingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly IDistributedCache _cache;
    private readonly ILogger<QueryUnderstandingService> _logger;
    private readonly ResiliencePipeline _pipeline;

    private readonly Dictionary<string, HashSet<string>> _synonymMap;
    private readonly bool _enableLlmExpansion;
    private readonly int _ambiguousWordThreshold;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] ConversationalPhrases =
    [
        "looking for", "job where", "jobs where", "role where", "roles where",
        "want to", "wants to", "career in", "interested in", "junior who", "senior who",
        "developer that", "engineer that", "entry level job", "fresh graduate"
    ];

    public QueryUnderstandingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IDistributedCache cache,
        ILogger<QueryUnderstandingService> logger)
    {
        _http = httpClientFactory.CreateClient("Gemini");
        _apiKey = configuration["Gemini:ApiKey"]
            ?? configuration["GEMINI_API_KEY"]
            ?? string.Empty;
        _model = configuration["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        _cache = cache;
        _logger = logger;

        _enableLlmExpansion = !bool.TryParse(configuration?["SearchRelevance:EnableLlmAmbiguousQueryExpansion"], out var enableLlm) || enableLlm;
        _ambiguousWordThreshold = int.TryParse(configuration?["SearchRelevance:AmbiguousQueryWordThreshold"], out var threshold) ? threshold : 3;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            })
            .Build();

        _synonymMap = LoadSynonyms(configuration);
    }

    public async Task<QueryUnderstandingResult> UnderstandQueryAsync(string? rawQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return new QueryUnderstandingResult(
                OriginalQuery: string.Empty,
                NormalizedQuery: string.Empty,
                ExpandedTerms: Array.Empty<string>(),
                InferredIsRemote: null,
                InferredSkills: Array.Empty<string>(),
                IsAmbiguousNaturalLanguage: false);
        }

        var trimmed = rawQuery.Trim();
        var normalized = trimmed.ToLowerInvariant();
        var words = Regex.Split(normalized, @"\s+").Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();

        // 1. Detect remote intent in query
        bool? inferredRemote = null;
        if (normalized.Contains("remote") || normalized.Contains("wfh") || normalized.Contains("work from home"))
        {
            inferredRemote = true;
        }

        // 2. Expand via configurable in-memory synonym dictionary
        var expandedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check entire normalized query in dictionary
        if (_synonymMap.TryGetValue(normalized, out var fullMatches))
        {
            foreach (var match in fullMatches) expandedTerms.Add(match);
        }

        // Check individual words in dictionary
        foreach (var word in words)
        {
            if (_synonymMap.TryGetValue(word, out var wordMatches))
            {
                foreach (var match in wordMatches) expandedTerms.Add(match);
            }
        }

        // 3. Ambiguous natural-language check
        bool isAmbiguous = words.Length >= _ambiguousWordThreshold &&
            (ConversationalPhrases.Any(phrase => normalized.Contains(phrase)) || words.Length > 5);

        // If not ambiguous or LLM expansion disabled/no API key, return fast local result
        if (!isAmbiguous || !_enableLlmExpansion || string.IsNullOrWhiteSpace(_apiKey))
        {
            return new QueryUnderstandingResult(
                OriginalQuery: trimmed,
                NormalizedQuery: normalized,
                ExpandedTerms: expandedTerms.ToList(),
                InferredIsRemote: inferredRemote,
                InferredSkills: Array.Empty<string>(),
                IsAmbiguousNaturalLanguage: isAmbiguous);
        }

        // 4. LLM-based query understanding for genuinely ambiguous natural-language queries
        var llmResult = await ExpandWithGeminiAsync(trimmed, normalized, cancellationToken);
        if (llmResult != null)
        {
            foreach (var term in llmResult.ExpandedTerms)
            {
                expandedTerms.Add(term);
            }

            return new QueryUnderstandingResult(
                OriginalQuery: trimmed,
                NormalizedQuery: normalized,
                ExpandedTerms: expandedTerms.ToList(),
                InferredIsRemote: inferredRemote ?? llmResult.InferredIsRemote,
                InferredSkills: llmResult.InferredSkills,
                IsAmbiguousNaturalLanguage: true,
                CanonicalRole: llmResult.CanonicalRole);
        }

        return new QueryUnderstandingResult(
            OriginalQuery: trimmed,
            NormalizedQuery: normalized,
            ExpandedTerms: expandedTerms.ToList(),
            InferredIsRemote: inferredRemote,
            InferredSkills: Array.Empty<string>(),
            IsAmbiguousNaturalLanguage: true);
    }

    private async Task<QueryUnderstandingResult?> ExpandWithGeminiAsync(string rawQuery, string normalizedQuery, CancellationToken cancellationToken)
    {
        string cacheKey = "search:llm_query:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery)));

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cached))
            {
                _logger.LogInformation("Query Understanding LLM cache hit for '{Query}'", normalizedQuery);
                return JsonSerializer.Deserialize<QueryUnderstandingResult>(cached, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for query expansion {CacheKey}", cacheKey);
        }

        try
        {
            _logger.LogInformation("Calling Gemini for ambiguous query expansion: '{Query}'", normalizedQuery);

            string prompt = $$"""
                You are a search query understanding engine for a tech job board.
                Analyze the following user query: "{{rawQuery}}"
                Extract the search intent into JSON:
                {
                  "canonicalRole": string or null (e.g. "Mobile Developer", "Backend Engineer"),
                  "inferredSkills": [string] (e.g. ["Flutter", "Dart"]),
                  "expandedTerms": [string] (e.g. ["Flutter Developer", "Dart Developer", "Mobile Engineer"]),
                  "isRemote": boolean or null
                }
                Return ONLY valid JSON.
                """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await _pipeline.ExecuteAsync(
                async ct => await _http.PostAsync(url, content, ct), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini query expansion returned status {StatusCode}", response.StatusCode);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return null;

            var text = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text)) return null;

            using var parsedDoc = JsonDocument.Parse(text);
            var root = parsedDoc.RootElement;

            string? role = root.TryGetProperty("canonicalRole", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            bool? isRemote = root.TryGetProperty("isRemote", out var rem) && (rem.ValueKind == JsonValueKind.True || rem.ValueKind == JsonValueKind.False) ? rem.GetBoolean() : null;

            var skills = new List<string>();
            if (root.TryGetProperty("inferredSkills", out var sk) && sk.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in sk.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                        skills.Add(el.GetString()!.Trim());
                }
            }

            var terms = new List<string>();
            if (root.TryGetProperty("expandedTerms", out var exp) && exp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in exp.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
                        terms.Add(el.GetString()!.Trim().ToLowerInvariant());
                }
            }

            if (!string.IsNullOrWhiteSpace(role)) terms.Add(role.ToLowerInvariant());
            foreach (var s in skills) terms.Add(s.ToLowerInvariant());

            var result = new QueryUnderstandingResult(
                OriginalQuery: rawQuery,
                NormalizedQuery: normalizedQuery,
                ExpandedTerms: terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                InferredIsRemote: isRemote,
                InferredSkills: skills,
                IsAmbiguousNaturalLanguage: true,
                CanonicalRole: role);

            // Cache for 24 hours
            _ = _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result, _jsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                CancellationToken.None);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini query expansion failed for '{Query}' — falling back to local tokens", normalizedQuery);
            return null;
        }
    }

    private static Dictionary<string, HashSet<string>> LoadSynonyms(IConfiguration? configuration)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // Core built-in tech synonym defaults
        void AddDefault(string key, params string[] synonyms)
        {
            if (!map.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[key] = set;
            }
            foreach (var s in synonyms) set.Add(s);
        }

        AddDefault(".net", "c#", "dotnet", "asp.net", "asp.net core");
        AddDefault("dotnet", ".net", "c#", "asp.net");
        AddDefault("c#", ".net", "dotnet", "asp.net");
        AddDefault("react", "reactjs", "react.js", "frontend");
        AddDefault("node", "nodejs", "node.js", "express", "backend");
        AddDefault("angular", "typescript", "frontend");
        AddDefault("vue", "vuejs", "frontend");
        AddDefault("python", "django", "fastapi", "flask");
        AddDefault("golang", "go", "backend");
        AddDefault("go", "golang", "backend");
        AddDefault("frontend", "front-end", "react", "vue", "angular");
        AddDefault("backend", "back-end", ".net", "node", "python", "golang");
        AddDefault("remote", "work from home", "wfh", "telecommute", "distributed");
        AddDefault("wfh", "remote", "work from home");
        AddDefault("work from home", "remote", "wfh");

        // Overlay with any custom definitions from configuration section "SearchRelevance:Synonyms"
        var section = configuration?.GetSection("SearchRelevance:Synonyms");
        if (section != null && section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                var key = child.Key.Trim();
                var values = child.Get<string[]>() ?? child.GetChildren().Select(c => c.Value ?? "").Where(v => !string.IsNullOrEmpty(v)).ToArray();
                if (values.Length > 0)
                {
                    if (!map.TryGetValue(key, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        map[key] = set;
                    }
                    foreach (var val in values)
                    {
                        set.Add(val.Trim());
                    }
                }
            }
        }

        return map;
    }
}
