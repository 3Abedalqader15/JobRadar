using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface IRawPostRepository
{
    Task AddAsync(RawPost rawPost, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid sourceId, string rawUrl, CancellationToken cancellationToken = default);
}
