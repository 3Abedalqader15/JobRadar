using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class JobApplicationQuestionRepository(AppDbContext dbContext) 
    : Repository<JobApplicationQuestion, Guid>(dbContext), IJobApplicationQuestionRepository
{
    public async Task<IReadOnlyList<JobApplicationQuestion>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(q => q.JobId == jobId)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(cancellationToken);
    }
}
