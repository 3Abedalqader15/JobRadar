using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<RefreshToken>> GetAllActiveTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    void Update(RefreshToken token);
}
