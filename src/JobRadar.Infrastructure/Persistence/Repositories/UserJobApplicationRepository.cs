using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class UserJobApplicationRepository(AppDbContext dbContext) : Repository<UserJobApplication, Guid>(dbContext), IUserJobApplicationRepository
{
    public async Task<IReadOnlyList<UserJobApplication>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);
    }
}
