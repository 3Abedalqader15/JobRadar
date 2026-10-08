using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Search;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.JobSearch.Queries;

public sealed record SearchJobsQuery(
    string? Query = null,
    string? Location = null,
    bool? IsRemote = null,
    IReadOnlyList<EmploymentType>? EmploymentTypes = null,
    IReadOnlyList<ExperienceLevel>? ExperienceLevels = null,
    decimal? SalaryMin = null,
    decimal? SalaryMax = null,
    IReadOnlyList<string>? Skills = null,
    DatePostedFilter DatePosted = DatePostedFilter.AllTime,
    JobSortOption SortBy = JobSortOption.Relevance,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<JobSearchResultDto>>;

public sealed class SearchJobsQueryHandler : IRequestHandler<SearchJobsQuery, PagedResult<JobSearchResultDto>>
{
    private readonly IJobRepository _jobRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQueryUnderstandingService _queryUnderstandingService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SearchJobsQueryHandler> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SearchJobsQueryHandler(
        IJobRepository jobRepository,
        IEmbeddingService embeddingService,
        IQueryUnderstandingService queryUnderstandingService,
        IDistributedCache cache,
        ILogger<SearchJobsQueryHandler> logger)
    {
        _jobRepository = jobRepository;
        _embeddingService = embeddingService;
        _queryUnderstandingService = queryUnderstandingService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<JobSearchResultDto>> Handle(SearchJobsQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Generate Canonical Results Cache Key (Hashing ALL filter parameters)
        var canonicalParams = new
        {
            query = request.Query?.Trim().ToLowerInvariant() ?? "",
            location = request.Location?.Trim().ToLowerInvariant() ?? "",
            isRemote = request.IsRemote,
            employmentTypes = request.EmploymentTypes != null ? request.EmploymentTypes.OrderBy(x => (int)x).ToList() : null,
            experienceLevels = request.ExperienceLevels != null ? request.ExperienceLevels.OrderBy(x => (int)x).ToList() : null,
            salaryMin = request.SalaryMin,
            salaryMax = request.SalaryMax,
            skills = request.Skills != null ? request.Skills.Select(s => s.Trim().ToLowerInvariant()).OrderBy(s => s).ToList() : null,
            datePosted = (int)request.DatePosted,
            sortBy = (int)request.SortBy,
            page = request.Page,
            pageSize = request.PageSize
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(canonicalParams, _jsonOptions);
        string resultsCacheKey = "search:results:" + Convert.ToHexString(SHA256.HashData(jsonBytes));

        // 2. Check Results Cache (5 min TTL) — Redis is optional; failures are non-fatal
        try
        {
            var cachedResults = await _cache.GetStringAsync(resultsCacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedResults))
            {
                _logger.LogInformation("Cache hit for SearchJobsQuery {CacheKey}", resultsCacheKey);
                var parsed = JsonSerializer.Deserialize<PagedResult<JobSearchResultDto>>(cachedResults, _jsonOptions);
                if (parsed != null)
                {
                    stopwatch.Stop();
                    // Update search duration to reflect sub-millisecond cache latency
                    foreach (var item in parsed.Items)
                    {
                        item.SearchDurationMs = stopwatch.ElapsedMilliseconds;
                    }
                    return parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache unavailable — skipping results cache read for {CacheKey}", resultsCacheKey);
        }

        // 3. Multi-Tier Search Execution (Model A: True Parallel Fusion with Fast-Path)
        float[]? queryEmbedding = null;
        bool hasNaturalLanguageQuery = !string.IsNullOrWhiteSpace(request.Query);
        bool requiresSemanticFusion = hasNaturalLanguageQuery && request.SortBy == JobSortOption.Relevance;

        if (requiresSemanticFusion)
        {
            string normalizedQuery = request.Query!.Trim().ToLowerInvariant();
            string embeddingCacheKey = "search:embedding:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery)));

            string? cachedEmbedding = null;
            try
            {
                cachedEmbedding = await _cache.GetStringAsync(embeddingCacheKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache unavailable — skipping embedding cache read for query '{Query}'", normalizedQuery);
            }

            if (!string.IsNullOrEmpty(cachedEmbedding))
            {
                _logger.LogInformation("Embedding cache hit for query '{Query}'", normalizedQuery);
                queryEmbedding = JsonSerializer.Deserialize<float[]>(cachedEmbedding, _jsonOptions);
            }
            else
            {
                _logger.LogInformation("Embedding cache miss for query '{Query}' — generating embedding", normalizedQuery);
                try
                {
                    queryEmbedding = await _embeddingService.GenerateAsync(request.Query!, cancellationToken);

                    // Cache embedding for 1 hour
                    _ = _cache.SetStringAsync(
                        embeddingCacheKey,
                        JsonSerializer.Serialize(queryEmbedding, _jsonOptions),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Embedding service failed — falling back to pure FTS/Trigram search for '{Query}'", normalizedQuery);
                    queryEmbedding = null;
                }
            }
        }

        if (queryEmbedding != null && queryEmbedding.Length == 0)
        {
            queryEmbedding = null;
        }

        // 4. Query Understanding Layer (Synonyms, Ambiguous Intent, Remote Inference)
        var queryUnderstanding = await _queryUnderstandingService.UnderstandQueryAsync(request.Query, cancellationToken);
        bool? effectiveIsRemote = request.IsRemote ?? queryUnderstanding.InferredIsRemote;

        // 5. Execute Advanced Multi-Faceted Query via Repository
        var criteria = new JobSearchCriteria(
            Keyword: request.Query?.Trim(),
            ExpandedTerms: queryUnderstanding.ExpandedTerms,
            Vector: queryEmbedding,
            Location: request.Location?.Trim(),
            IsRemote: effectiveIsRemote,
            EmploymentTypes: request.EmploymentTypes,
            ExperienceLevels: request.ExperienceLevels,
            SalaryMin: request.SalaryMin,
            SalaryMax: request.SalaryMax,
            Skills: request.Skills,
            DatePosted: request.DatePosted,
            SortBy: request.SortBy,
            Page: request.Page,
            PageSize: request.PageSize
        );

        var (jobs, scores, totalCount) = await _jobRepository.SearchJobsAdvancedAsync(criteria, cancellationToken);

        // 6. "No Results" Recovery (Filter Relaxation & Typo Suggestions)
        string? broadeningNotice = null;
        string? suggestedQuery = null;

        if (totalCount == 0)
        {
            bool hasRestrictiveFilters = request.SalaryMin.HasValue ||
                                         request.SalaryMax.HasValue ||
                                         request.DatePosted != DatePostedFilter.AllTime ||
                                         (request.EmploymentTypes is { Count: > 0 }) ||
                                         (request.ExperienceLevels is { Count: > 0 });

            if (hasRestrictiveFilters)
            {
                _logger.LogInformation("Zero results for '{Query}' with restrictive filters; attempting filter relaxation", request.Query);

                var relaxedCriteria = criteria with
                {
                    SalaryMin = null,
                    SalaryMax = null,
                    DatePosted = DatePostedFilter.AllTime
                };

                var (relJobs, relScores, relCount) = await _jobRepository.SearchJobsAdvancedAsync(relaxedCriteria, cancellationToken);
                if (relCount > 0)
                {
                    jobs = relJobs;
                    scores = relScores;
                    totalCount = relCount;
                    broadeningNotice = "No exact matches with your salary/date filters. Showing results with relaxed filters.";
                }
                else if ((request.EmploymentTypes is { Count: > 0 }) || (request.ExperienceLevels is { Count: > 0 }))
                {
                    var ultraRelaxedCriteria = relaxedCriteria with
                    {
                        EmploymentTypes = null,
                        ExperienceLevels = null
                    };

                    var (uJobs, uScores, uCount) = await _jobRepository.SearchJobsAdvancedAsync(ultraRelaxedCriteria, cancellationToken);
                    if (uCount > 0)
                    {
                        jobs = uJobs;
                        scores = uScores;
                        totalCount = uCount;
                        broadeningNotice = "No exact matches found with all filters. Showing all positions matching your keyword.";
                    }
                }
            }

            if (totalCount == 0 && !string.IsNullOrWhiteSpace(request.Query))
            {
                var closestTitle = await _jobRepository.FindClosestActiveTitleAsync(request.Query, cancellationToken);
                if (!string.IsNullOrWhiteSpace(closestTitle) && !closestTitle.Equals(request.Query.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    suggestedQuery = closestTitle;
                    broadeningNotice = $"No exact matches found for '{request.Query}'. Did you mean '{closestTitle}'?";
                }
            }
        }

        stopwatch.Stop();
        long elapsedMs = stopwatch.ElapsedMilliseconds;

        // 7. Map results to DTOs and populate MatchedTerms
        var dtos = new List<JobSearchResultDto>(jobs.Count);
        for (int i = 0; i < jobs.Count; i++)
        {
            var j = jobs[i];
            var skillNames = j.JobSkills?
                .Where(js => js.Skill != null)
                .Select(js => js.Skill!.Name)
                .ToList() ?? new List<string>();

            var matchedTerms = ExtractMatchedTerms(j, queryUnderstanding, request.Query);

            dtos.Add(new JobSearchResultDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.CompanyName,
                Location = j.Location,
                IsRemote = j.IsRemote,
                EmploymentType = j.EmploymentType,
                ExperienceLevel = j.ExperienceLevel,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryCurrency = j.SalaryCurrency,
                PostedAt = j.PostedAt,
                Skills = skillNames,
                RelevanceScore = i < scores.Length ? scores[i] : 1.0,
                ExternalApplyUrl = j.ExternalApplyUrl,
                SearchDurationMs = elapsedMs,
                SourceName = j.Source?.Name ?? (j.RawPostId == null ? "Direct" : "Aggregated"),
                IsVerified = j.RawPostId == null,
                ApplicantsClickCount = j.ApplicantsClickCount,
                CompanyId = j.CompanyId,
                MatchedTerms = matchedTerms
            });
        }

        var result = new PagedResult<JobSearchResultDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            BroadeningNotice = broadeningNotice,
            SuggestedQuery = suggestedQuery
        };

        // 8. Cache result
        try
        {
            await _cache.SetStringAsync(
                resultsCacheKey,
                JsonSerializer.Serialize(result, _jsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache unavailable — skipping results cache write for {CacheKey}", resultsCacheKey);
        }

        return result;
    }

    private static IReadOnlyList<string> ExtractMatchedTerms(
        JobRadar.Domain.Entities.Job j,
        QueryUnderstandingResult qu,
        string? rawQuery)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(rawQuery))
        {
            foreach (var word in rawQuery.Split([' ', ',', '/', '-', '+'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length >= 2) candidates.Add(word);
            }
        }
        foreach (var term in qu.ExpandedTerms)
        {
            if (term.Length >= 2) candidates.Add(term);
        }

        var matched = new List<string>();
        var skillNames = j.JobSkills?
            .Where(s => s.Skill != null)
            .Select(s => s.Skill!.Name) ?? Enumerable.Empty<string>();
        var searchableText = $"{j.Title} {j.CompanyName} {j.Description} {string.Join(" ", skillNames)}";

        foreach (var candidate in candidates)
        {
            if (searchableText.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                matched.Add(candidate);
            }
        }

        return matched.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }
}
