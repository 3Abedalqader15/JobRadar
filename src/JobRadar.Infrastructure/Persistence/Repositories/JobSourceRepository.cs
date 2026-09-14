using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public sealed class JobSourceRepository
    : Repository<JobSource, Guid>, IJobSourceRepository
{
    public JobSourceRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<JobSource>> GetAllEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }
}
