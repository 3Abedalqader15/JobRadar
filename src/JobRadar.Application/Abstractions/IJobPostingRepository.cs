using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;

namespace JobRadar.Application.Abstractions;

public sealed record JobSearchCriteria(
    string? Keyword,
    float[]? Vector,
    string? Location,
    bool? IsRemote,
    IReadOnlyList<EmploymentType>? EmploymentTypes,
    IReadOnlyList<ExperienceLevel>? ExperienceLevels,
    decimal? SalaryMin,
    decimal? SalaryMax,
    IReadOnlyList<string>? Skills,
    DatePostedFilter DatePosted,
    JobSortOption SortBy,
    int Page,
    int PageSize);

/// <summary>
/// Repository for <see cref="Job"/> entities.
/// </summary>
public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Job>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Job>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default);
    Task AddAsync(Job job, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task DeleteAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Job> Jobs, double[] Scores, int TotalCount)> SearchSemanticAsync(
        float[] vector, 
        string? location, 
        EmploymentType? empType, 
        ExperienceLevel? expLevel, 
        int page, 
        int pageSize, 
        CancellationToken ct = default);

    Task<(IReadOnlyList<Job> Jobs, double[] Scores, int TotalCount)> SearchJobsAdvancedAsync(
        JobSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
