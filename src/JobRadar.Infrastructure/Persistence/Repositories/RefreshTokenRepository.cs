using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;

    public RefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(RefreshToken token)
    {
        _dbContext.Set<RefreshToken>().Add(token);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<RefreshToken>()
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IEnumerable<RefreshToken>> GetAllActiveTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public void Update(RefreshToken token)
    {
        _dbContext.Set<RefreshToken>().Update(token);
    }
}
