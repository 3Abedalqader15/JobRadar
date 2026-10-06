using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Company?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Company> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<JobRadar.Application.Features.Companies.Queries.ListCompanies.CompanyDto> Items, int TotalCount)> GetCompanyDtosPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task UpdateAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
