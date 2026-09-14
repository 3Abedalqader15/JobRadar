using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface IApplicationRecordRepository
{
    Task<ApplicationRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationRecord>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ApplicationRecord applicationRecord, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApplicationRecord applicationRecord, CancellationToken cancellationToken = default);
    Task DeleteAsync(ApplicationRecord applicationRecord, CancellationToken cancellationToken = default);
}
