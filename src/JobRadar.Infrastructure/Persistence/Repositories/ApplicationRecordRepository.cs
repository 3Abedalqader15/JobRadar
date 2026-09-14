using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public sealed class ApplicationRecordRepository
    : Repository<ApplicationRecord, Guid>, IApplicationRecordRepository
{
    public ApplicationRecordRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ApplicationRecord>> GetByJobPostingIdAsync(
        Guid jobPostingId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(a => a.JobPostingId == jobPostingId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(cancellationToken);
    }
}
