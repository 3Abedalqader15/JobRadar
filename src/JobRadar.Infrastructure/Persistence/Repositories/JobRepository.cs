using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext dbContext) : Repository<Job, Guid>(dbContext), IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> SearchByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(j => j.Title.Contains(title))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Job> Jobs, double[] Scores, int TotalCount)> SearchSemanticAsync(
        float[] vector, 
        string? location, 
        EmploymentType? empType, 
        ExperienceLevel? expLevel, 
        int page, 
        int pageSize, 
        CancellationToken ct = default)
    {
        var criteria = new JobSearchCriteria(
            Keyword: null,
            Vector: vector,
            Location: location,
            IsRemote: null,
            EmploymentTypes: empType.HasValue ? [empType.Value] : null,
            ExperienceLevels: expLevel.HasValue ? [expLevel.Value] : null,
            SalaryMin: null,
            SalaryMax: null,
            Skills: null,
            DatePosted: DatePostedFilter.AllTime,
            SortBy: JobSortOption.Relevance,
            Page: page,
            PageSize: pageSize
        );

        return await SearchJobsAdvancedAsync(criteria, ct);
    }

    public async Task<(IReadOnlyList<Job> Jobs, double[] Scores, int TotalCount)> SearchJobsAdvancedAsync(
        JobSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(j => j.JobSkills)
                .ThenInclude(js => js.Skill)
            .Where(j => j.IsActive);

        // 1. Work Mode & Location Filters
        if (criteria.IsRemote.HasValue)
        {
            query = query.Where(j => j.IsRemote == criteria.IsRemote.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Location))
        {
            query = query.Where(j => j.Location != null && EF.Functions.ILike(j.Location, $"%{criteria.Location}%"));
        }

        // 2. Employment Types (Multi-select)
        if (criteria.EmploymentTypes is { Count: > 0 })
        {
            query = query.Where(j => criteria.EmploymentTypes.Contains(j.EmploymentType));
        }

        // 3. Experience Levels (Multi-select)
        if (criteria.ExperienceLevels is { Count: > 0 })
        {
            query = query.Where(j => criteria.ExperienceLevels.Contains(j.ExperienceLevel));
        }

        // 4. Salary Range
        if (criteria.SalaryMin.HasValue)
        {
            query = query.Where(j => j.SalaryMax == null || j.SalaryMax >= criteria.SalaryMin.Value);
        }

        if (criteria.SalaryMax.HasValue)
        {
            query = query.Where(j => j.SalaryMin == null || j.SalaryMin <= criteria.SalaryMax.Value);
        }

        // 5. Skills Many-to-Many Join via JobSkills -> Skill
        if (criteria.Skills is { Count: > 0 })
        {
            var lowerSkills = criteria.Skills.Select(s => s.ToLowerInvariant()).ToList();
            query = query.Where(j => j.JobSkills.Any(js =>
                js.Skill != null && (lowerSkills.Contains(js.Skill.Slug.ToLowerInvariant()) || lowerSkills.Contains(js.Skill.Name.ToLowerInvariant()))));
        }

        // 6. Date Posted Filter
        if (criteria.DatePosted == DatePostedFilter.Last24Hours)
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            query = query.Where(j => j.PostedAt >= cutoff);
        }
        else if (criteria.DatePosted == DatePostedFilter.PastWeek)
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            query = query.Where(j => j.PostedAt >= cutoff);
        }
        else if (criteria.DatePosted == DatePostedFilter.PastMonth)
        {
            var cutoff = DateTime.UtcNow.AddDays(-30);
            query = query.Where(j => j.PostedAt >= cutoff);
        }

        // 7. Keyword Search (Trigram / Full-text substring matching)
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var kw = criteria.Keyword.Trim();
            query = query.Where(j =>
                EF.Functions.ILike(j.Title, $"%{kw}%") ||
                EF.Functions.ILike(j.CompanyName, $"%{kw}%") ||
                (j.Location != null && EF.Functions.ILike(j.Location, $"%{kw}%")) ||
                EF.Functions.ILike(j.Description, $"%{kw}%"));
        }

        // 8. Total Count for Pagination
        int totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return (Array.Empty<Job>(), Array.Empty<double>(), 0);
        }

        // 9. Sorting & Scoring (Model A)
        int skip = (criteria.Page - 1) * criteria.PageSize;
        int take = criteria.PageSize;

        if (criteria.Vector != null && criteria.SortBy == JobSortOption.Relevance)
        {
            var pgVector = new Vector(criteria.Vector);

            var rankedResults = await query
                .Select(j => new
                {
                    Job = j,
                    Distance = j.Embedding != null ? j.Embedding.CosineDistance(pgVector) : 1.0
                })
                .OrderBy(x => x.Distance)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            var jobs = rankedResults.Select(x => x.Job).ToList();
            var scores = rankedResults.Select(x => Math.Max(0.0, Math.Min(1.0, 1.0 - x.Distance))).ToArray();
            return (jobs, scores, totalCount);
        }

        // Fast-path ordering (Newest, Salary, or default)
        IQueryable<Job> orderedQuery = criteria.SortBy switch
        {
            JobSortOption.SalaryDescending => query.OrderByDescending(j => j.SalaryMax ?? j.SalaryMin ?? 0),
            JobSortOption.Newest => query.OrderByDescending(j => j.PostedAt),
            _ => query.OrderByDescending(j => j.PostedAt)
        };

        var list = await orderedQuery
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var defaultScores = Enumerable.Repeat(1.0, list.Count).ToArray();
        return (list, defaultScores, totalCount);
    }

    public async Task<IReadOnlyList<string>> GetExistingUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var urlList = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        if (urlList.Count == 0) return Array.Empty<string>();

        return await DbSet
            .AsNoTracking()
            .Where(j => j.ExternalApplyUrl != null && urlList.Contains(j.ExternalApplyUrl))
            .Select(j => j.ExternalApplyUrl!)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByTitleAndCompanyAsync(string title, string companyName, CancellationToken cancellationToken = default)
    {
        var normTitle = title.Trim().ToLowerInvariant();
        var normCompany = companyName.Trim().ToLowerInvariant();

#pragma warning disable CA1862 // EF Core LINQ-to-Entities translates ToLowerInvariant() directly to SQL LOWER()
        return await DbSet
            .AsNoTracking()
            .AnyAsync(j => j.Title.ToLowerInvariant() == normTitle && j.CompanyName.ToLowerInvariant() == normCompany, cancellationToken);
#pragma warning restore CA1862
    }

    public async Task AddWithSkillsAsync(Job job, IEnumerable<string> skillNames, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(job, cancellationToken);

        if (skillNames == null) return;

        foreach (var skillName in skillNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normSkill = skillName.Trim();
            if (string.IsNullOrWhiteSpace(normSkill)) continue;

            var lowerSkill = normSkill.ToLowerInvariant();
#pragma warning disable CA1862 // EF Core LINQ-to-Entities translates ToLowerInvariant() directly to SQL LOWER()
            var skill = Context.Skills.Local.FirstOrDefault(s => s.Name.Equals(normSkill, StringComparison.OrdinalIgnoreCase))
                ?? await Context.Skills.FirstOrDefaultAsync(s => s.Name.ToLowerInvariant() == lowerSkill, cancellationToken);
#pragma warning restore CA1862

            if (skill == null)
            {
                var slug = normSkill.ToLowerInvariant()
                    .Replace(' ', '-')
                    .Replace('.', '-')
                    .Replace('#', 's');
                skill = Skill.Create(normSkill, slug);
                Context.Skills.Add(skill);
            }

            var alreadyMapped = Context.JobSkillMaps.Local.Any(m => m.JobId == job.Id && m.SkillId == skill.Id);
            if (!alreadyMapped)
            {
                Context.JobSkillMaps.Add(new JobSkillMap(job.Id, skill.Id));
            }
        }
    }
}
