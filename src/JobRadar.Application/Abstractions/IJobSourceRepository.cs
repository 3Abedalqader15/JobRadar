using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface IJobSourceRepository
{
    Task<JobSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobSource>> GetAllEnabledAsync(CancellationToken cancellationToken = default);
    Task AddAsync(JobSource jobSource, CancellationToken cancellationToken = default);
    Task UpdateAsync(JobSource jobSource, CancellationToken cancellationToken = default);
}
