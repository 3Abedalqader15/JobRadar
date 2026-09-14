using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public sealed class JobPostingRepository
    : Repository<JobPosting, Guid>, IJobPostingRepository
{
    public JobPostingRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<JobPosting>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .OrderByDescending(j => j.PostedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobPosting>> SearchByTitleAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(j => EF.Functions.ILike(j.Title, $"%{title}%"))
            .OrderByDescending(j => j.PostedAt)
            .ToListAsync(cancellationToken);
    }
}
