using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class SourceRepository(AppDbContext dbContext) : Repository<Source, Guid>(dbContext), ISourceRepository
{
    public async Task<IReadOnlyList<Source>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.Status == SourceStatus.Active)
            .ToListAsync(cancellationToken);
    }
}
