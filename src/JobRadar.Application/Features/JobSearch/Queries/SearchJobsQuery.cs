using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.JobSearch.Queries;

public sealed record SearchJobsQuery(
    string Query,
    string? Location = null,
    EmploymentType? EmploymentType = null,
    ExperienceLevel? ExperienceLevel = null,
    int Page = 1,
    int PageSize = 10) : IRequest<PagedResult<JobSearchResultDto>>;

public sealed class SearchJobsQueryHandler : IRequestHandler<SearchJobsQuery, PagedResult<JobSearchResultDto>>
{
    private readonly IJobRepository _jobRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SearchJobsQueryHandler> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

    public SearchJobsQueryHandler(
        IJobRepository jobRepository,
        IEmbeddingService embeddingService,
        IDistributedCache cache,
        ILogger<SearchJobsQueryHandler> logger)
    {
        _jobRepository = jobRepository;
        _embeddingService = embeddingService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<JobSearchResultDto>> Handle(SearchJobsQuery request, CancellationToken cancellationToken)
    {
        // 1. Generate Results Cache Key (hash of all canonicalized parameters)
        var cacheKeyParams = new
        {
            query = request.Query.Trim().ToLowerInvariant(),
            location = request.Location?.Trim().ToLowerInvariant(),
            employmentType = request.EmploymentType,
            experienceLevel = request.ExperienceLevel,
            page = request.Page,
            pageSize = request.PageSize
        };
        
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(cacheKeyParams, _jsonOptions);
        
        string resultsCacheKey = "search:results:" + Convert.ToHexString(SHA256.HashData(jsonBytes));

        // 2. Check Results Cache (5 min TTL)
        var cachedResults = await _cache.GetStringAsync(resultsCacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedResults))
        {
            _logger.LogInformation("Cache hit for SearchJobsQuery {CacheKey}", resultsCacheKey);
            var parsed = JsonSerializer.Deserialize<PagedResult<JobSearchResultDto>>(cachedResults);
            if (parsed != null)
            {
                return parsed;
            }
        }

        _logger.LogInformation("Cache miss for SearchJobsQuery {CacheKey}", resultsCacheKey);

        // 3. Check Embedding Cache (1 hr TTL) using query text only
        string normalizedQuery = request.Query.Trim().ToLowerInvariant();
        string embeddingCacheKey = "search:embedding:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery)));
        
        float[] queryEmbedding;
        var cachedEmbedding = await _cache.GetStringAsync(embeddingCacheKey, cancellationToken);
        
        if (!string.IsNullOrEmpty(cachedEmbedding))
        {
            _logger.LogInformation("Embedding cache hit for query '{Query}'", normalizedQuery);
            queryEmbedding = JsonSerializer.Deserialize<float[]>(cachedEmbedding)!;
        }
        else
        {
            _logger.LogInformation("Embedding cache miss for query '{Query}'", normalizedQuery);
            queryEmbedding = await _embeddingService.GenerateAsync(request.Query, cancellationToken);
            
            // Save to embedding cache for 1 hour
            await _cache.SetStringAsync(
                embeddingCacheKey, 
                JsonSerializer.Serialize(queryEmbedding), 
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
                cancellationToken);
        }

        // 4. Run Semantic Hybrid Search in Repository
        var (jobs, scores, totalCount) = await _jobRepository.SearchSemanticAsync(
            queryEmbedding, 
            request.Location, 
            request.EmploymentType, 
            request.ExperienceLevel, 
            request.Page, 
            request.PageSize, 
            cancellationToken);

        // 5. Map results
        var dtos = new List<JobSearchResultDto>(jobs.Count);
        for (int i = 0; i < jobs.Count; i++)
        {
            var j = jobs[i];
            dtos.Add(new JobSearchResultDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.CompanyName,
                Location = j.Location,
                IsRemote = j.IsRemote,
                EmploymentType = j.EmploymentType,
                ExperienceLevel = j.ExperienceLevel,
                RelevanceScore = scores[i]
            });
        }

        var result = new PagedResult<JobSearchResultDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        // 6. Save to Results Cache (5 min TTL)
        await _cache.SetStringAsync(
            resultsCacheKey, 
            JsonSerializer.Serialize(result), 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            cancellationToken);

        return result;
    }
}
