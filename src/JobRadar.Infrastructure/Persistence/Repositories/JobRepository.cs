using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext dbContext) : Repository<Job, Guid>(dbContext), IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Title.Contains(title))
            .ToListAsync(cancellationToken);
    }
}
