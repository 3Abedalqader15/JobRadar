using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class SourceRepository(AppDbContext dbContext) : Repository<Source, Guid>(dbContext), ISourceRepository
{
    public async Task<Source?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<Source>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.Status == SourceStatus.Active)
            .ToListAsync(cancellationToken);
    }
}
