using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

internal sealed class RawPostRepository : IRawPostRepository
{
    private readonly AppDbContext _context;

    public RawPostRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RawPost rawPost, CancellationToken cancellationToken = default)
    {
        await _context.Set<RawPost>().AddAsync(rawPost, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid sourceId, string rawUrl, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RawPost>()
            .AnyAsync(r => r.SourceId == sourceId && r.RawUrl == rawUrl, cancellationToken);
    }
}
