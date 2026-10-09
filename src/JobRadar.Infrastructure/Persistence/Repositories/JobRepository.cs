using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext dbContext, ILogger<JobRepository> logger) : Repository<Job, Guid>(dbContext), IJobRepository
{
    private readonly ILogger<JobRepository> _logger = logger;

    public override async Task<IReadOnlyList<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(j => j.Source)
            .Include(j => j.Company)
            .ToListAsync(cancellationToken);
    }

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
            ExpandedTerms: null,
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
            .Include(j => j.Source)
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

        bool isPostgreSql = Context.Database.IsNpgsql();

        // 7. Keyword Search & Typo Tolerance (pg_trgm + Substring + Synonyms)
        if (!string.IsNullOrWhiteSpace(criteria.Keyword) || criteria.ExpandedTerms is { Count: > 0 })
        {
            var kw = criteria.Keyword?.Trim();
            var hasKw = !string.IsNullOrWhiteSpace(kw);
            var expanded = criteria.ExpandedTerms?
                .Where(t => !string.IsNullOrWhiteSpace(t) && (kw == null || !t.Equals(kw, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .ToList();

            var syn1 = expanded != null && expanded.Count > 0 ? expanded[0] : null;
            var syn2 = expanded != null && expanded.Count > 1 ? expanded[1] : null;
            var syn3 = expanded != null && expanded.Count > 2 ? expanded[2] : null;
            var syn4 = expanded != null && expanded.Count > 3 ? expanded[3] : null;

            if (isPostgreSql)
            {
                query = query.Where(j =>
                    (hasKw && (
                        EF.Functions.ILike(j.Title, $"%{kw}%") ||
                        EF.Functions.ILike(j.CompanyName, $"%{kw}%") ||
                        (j.Location != null && EF.Functions.ILike(j.Location, $"%{kw}%")) ||
                        EF.Functions.ILike(j.Description, $"%{kw}%") ||
                        EF.Functions.TrigramsAreWordSimilar(kw!, j.Title) ||
                        EF.Functions.TrigramsAreWordSimilar(kw!, j.CompanyName) ||
                        EF.Functions.TrigramsSimilarity(j.Title, kw!) >= 0.35f ||
                        EF.Functions.TrigramsSimilarity(j.CompanyName, kw!) >= 0.40f
                    )) ||
                    (syn1 != null && (EF.Functions.ILike(j.Title, $"%{syn1}%") || EF.Functions.ILike(j.Description, $"%{syn1}%"))) ||
                    (syn2 != null && (EF.Functions.ILike(j.Title, $"%{syn2}%") || EF.Functions.ILike(j.Description, $"%{syn2}%"))) ||
                    (syn3 != null && (EF.Functions.ILike(j.Title, $"%{syn3}%") || EF.Functions.ILike(j.Description, $"%{syn3}%"))) ||
                    (syn4 != null && (EF.Functions.ILike(j.Title, $"%{syn4}%") || EF.Functions.ILike(j.Description, $"%{syn4}%")))
                );
            }
            else
            {
                query = query.Where(j =>
                    (hasKw && (
                        EF.Functions.ILike(j.Title, $"%{kw}%") ||
                        EF.Functions.ILike(j.CompanyName, $"%{kw}%") ||
                        (j.Location != null && EF.Functions.ILike(j.Location, $"%{kw}%")) ||
                        EF.Functions.ILike(j.Description, $"%{kw}%")
                    )) ||
                    (syn1 != null && (EF.Functions.ILike(j.Title, $"%{syn1}%") || EF.Functions.ILike(j.Description, $"%{syn1}%"))) ||
                    (syn2 != null && (EF.Functions.ILike(j.Title, $"%{syn2}%") || EF.Functions.ILike(j.Description, $"%{syn2}%"))) ||
                    (syn3 != null && (EF.Functions.ILike(j.Title, $"%{syn3}%") || EF.Functions.ILike(j.Description, $"%{syn3}%"))) ||
                    (syn4 != null && (EF.Functions.ILike(j.Title, $"%{syn4}%") || EF.Functions.ILike(j.Description, $"%{syn4}%")))
                );
            }
        }

        // 8. Total Count for Pagination
        int totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return (Array.Empty<Job>(), Array.Empty<double>(), 0);
        }

        // 9. Sorting & Scoring (Hybrid Fusion: Trigram + FTS + Vector)
        int skip = (criteria.Page - 1) * criteria.PageSize;
        int take = criteria.PageSize;

        if (criteria.SortBy == JobSortOption.Relevance)
        {
            var kw = criteria.Keyword?.Trim() ?? "";
            var pgVector = criteria.Vector != null && criteria.Vector.Length > 0 ? new Vector(criteria.Vector) : null;
            bool hasVector = pgVector != null;
            bool hasKw = !string.IsNullOrWhiteSpace(kw);

            if (isPostgreSql && (hasKw || hasVector))
            {
                var rankedQuery = query.Select(j => new
                {
                    Job = j,
                    TrigramScore = hasKw ? (double)EF.Functions.TrigramsSimilarity(j.Title, kw) : 0.0,
                    VectorScore = hasVector && j.Embedding != null ? Math.Max(0.0, 1.0 - (double)j.Embedding.CosineDistance(pgVector!)) : 0.0,
                    TitleBonus = hasKw && EF.Functions.ILike(j.Title, $"%{kw}%") ? 0.35 : 0.0
                });

                var rankedResults = await rankedQuery
                    .Select(x => new
                    {
                        x.Job,
                        x.TrigramScore,
                        x.VectorScore,
                        CombinedScore = hasVector
                            ? (0.45 * (x.TrigramScore + x.TitleBonus) + 0.30 * x.VectorScore + 0.25 * (x.Job.PostedAt > DateTime.UtcNow.AddDays(-7) ? 1.0 : 0.4))
                            : (0.70 * (x.TrigramScore + x.TitleBonus) + 0.30 * (x.Job.PostedAt > DateTime.UtcNow.AddDays(-7) ? 1.0 : 0.4))
                    })
                    .OrderByDescending(x => x.CombinedScore)
                    .ThenByDescending(x => x.Job.PostedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken);

                // Structured logging for ranking quality audit
                foreach (var item in rankedResults.Take(5))
                {
                    _logger.LogInformation(
                        "Search Ranking Audit | Query: '{Query}' | JobId: {JobId} | Title: '{Title}' | TrigramScore: {Trigram:F3} | VectorScore: {Vector:F3} | CombinedScore: {Combined:F3}",
                        kw, item.Job.Id, item.Job.Title, item.TrigramScore, item.VectorScore, item.CombinedScore);
                }

                var jobs = rankedResults.Select(x => x.Job).ToList();
                var scores = rankedResults.Select(x => Math.Round(Math.Min(1.0, Math.Max(0.05, x.CombinedScore)), 4)).ToArray();
                return (jobs, scores, totalCount);
            }
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
        var trimmedTitle = title.Trim();
        var trimmedCompany = companyName.Trim();

        return await DbSet
            .AsNoTracking()
            .AnyAsync(j => EF.Functions.ILike(j.Title, trimmedTitle) && EF.Functions.ILike(j.CompanyName, trimmedCompany), cancellationToken);
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
            var skill = Context.Skills.Local.FirstOrDefault(s => s.Name.Equals(normSkill, StringComparison.OrdinalIgnoreCase))
                ?? await Context.Skills.FirstOrDefaultAsync(s => s.Name.ToLower() == lowerSkill, cancellationToken);

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

    public async Task<string?> FindClosestActiveTitleAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var trimmed = query.Trim();

        if (Context.Database.IsNpgsql())
        {
            return await DbSet
                .AsNoTracking()
                .Where(j => j.IsActive)
                .Select(j => new
                {
                    j.Title,
                    Sim = (double)EF.Functions.TrigramsSimilarity(j.Title, trimmed)
                })
                .Where(x => x.Sim > 0.3)
                .OrderByDescending(x => x.Sim)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }
}
