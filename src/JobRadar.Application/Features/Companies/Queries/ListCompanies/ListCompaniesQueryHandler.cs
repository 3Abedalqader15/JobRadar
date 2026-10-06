using JobRadar.Application.Abstractions;
using MediatR;

namespace JobRadar.Application.Features.Companies.Queries.ListCompanies;

public sealed class ListCompaniesQueryHandler : IRequestHandler<ListCompaniesQuery, ListCompaniesResponse>
{
    private readonly ICompanyRepository _companyRepository;

    public ListCompaniesQueryHandler(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<ListCompaniesResponse> Handle(ListCompaniesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _companyRepository.GetCompanyDtosPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            cancellationToken);

        return new ListCompaniesResponse(items, totalCount, request.Page, request.PageSize);
    }
}
