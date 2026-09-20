using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext dbContext) : Repository<Job, Guid>(dbContext), IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(j => j.Title.Contains(title))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Job> Jobs, double[] Scores, int TotalCount)> SearchSemanticAsync(
        float[] vector, 
        string? location, 
        JobRadar.Domain.Enums.EmploymentType? empType, 
        JobRadar.Domain.Enums.ExperienceLevel? expLevel, 
        int page, 
        int pageSize, 
        CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();

        // Must be active and have an embedding to participate in semantic search
        query = query.Where(j => j.IsActive && j.Embedding != null);

        if (!string.IsNullOrWhiteSpace(location))
        {
            // Simple ILIKE-style check or exact match, assuming EF translates Contains to ILIKE in Npgsql
            query = query.Where(j => j.Location != null && j.Location.Contains(location));
        }
        
        if (empType.HasValue)
        {
            query = query.Where(j => j.EmploymentType == empType.Value);
        }
        
        if (expLevel.HasValue)
        {
            query = query.Where(j => j.ExperienceLevel == expLevel.Value);
        }

        int totalCount = await query.CountAsync(ct);

        if (totalCount == 0)
        {
            return (Array.Empty<Job>(), Array.Empty<double>(), 0);
        }

        var pgVector = new Vector(vector);

        var results = await query
            .Select(j => new 
            { 
                Job = j, 
                Distance = j.Embedding!.CosineDistance(pgVector) 
            })
            .OrderBy(x => x.Distance)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var jobs = results.Select(x => x.Job).ToList();
        
        // Convert distance to relevance score (e.g., 1 - distance)
        var scores = results.Select(x => 1.0 - x.Distance).ToArray();

        return (jobs, scores, totalCount);
    }
}
