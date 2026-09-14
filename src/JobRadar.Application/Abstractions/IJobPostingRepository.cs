using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface IJobPostingRepository
{
    Task<JobPosting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobPosting>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobPosting>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobPosting>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default);
    Task AddAsync(JobPosting jobPosting, CancellationToken cancellationToken = default);
    Task UpdateAsync(JobPosting jobPosting, CancellationToken cancellationToken = default);
    Task DeleteAsync(JobPosting jobPosting, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
