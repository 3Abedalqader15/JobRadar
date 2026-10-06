using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Repositories;

public class CompanyRepository : Repository<Company, Guid>, ICompanyRepository
{
    public CompanyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Company?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normSlug = slug.Trim().ToLowerInvariant();
        return await DbSet
            .FirstOrDefaultAsync(c => c.Slug == normSlug, cancellationToken);
    }

    public async Task<(IReadOnlyList<Company> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{term}%") || EF.Functions.ILike(c.Slug, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<JobRadar.Application.Features.Companies.Queries.ListCompanies.CompanyDto> Items, int TotalCount)> GetCompanyDtosPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{term}%") || EF.Functions.ILike(c.Slug, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new JobRadar.Application.Features.Companies.Queries.ListCompanies.CompanyDto(
                c.Id,
                c.Name,
                c.Slug,
                c.LogoUrl,
                c.CreatedAt,
                c.Jobs.Count(j => j.IsActive),
                c.Users.Count
            ))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normSlug = slug.Trim().ToLowerInvariant();
        var query = DbSet.AsNoTracking().Where(c => c.Slug == normSlug);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
