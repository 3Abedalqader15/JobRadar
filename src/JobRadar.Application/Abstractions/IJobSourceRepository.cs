using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Repository for <see cref="Source"/> entities.
/// </summary>
public interface ISourceRepository
{
    Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Source>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Source source, CancellationToken cancellationToken = default);
    Task UpdateAsync(Source source, CancellationToken cancellationToken = default);
}
