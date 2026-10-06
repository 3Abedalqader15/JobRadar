using MediatR;

namespace JobRadar.Application.Features.Companies.Queries.ListCompanies;

public sealed record ListCompaniesQuery(
    int Page = 1,
    int PageSize = 50,
    string? Search = null) : IRequest<ListCompaniesResponse>;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    DateTime CreatedAt,
    int ActiveJobsCount,
    int HrUsersCount);

public sealed record ListCompaniesResponse(
    IReadOnlyList<CompanyDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
