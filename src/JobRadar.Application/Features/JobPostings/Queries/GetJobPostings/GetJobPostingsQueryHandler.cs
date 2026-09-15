using JobRadar.Application.Abstractions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;

public sealed class GetJobPostingsQueryHandler
    : IRequestHandler<GetJobPostingsQuery, GetJobPostingsResponse>
{
    private readonly IJobRepository _repository;

    public GetJobPostingsQueryHandler(IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetJobPostingsResponse> Handle(
        GetJobPostingsQuery request,
        CancellationToken cancellationToken)
    {
        var all = await _repository.GetAllAsync(cancellationToken);

        // Apply optional title/company search
        var filtered = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? all
            : all.Where(j =>
                j.Title.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                j.CompanyName.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase))
              .ToList();

        var totalCount = filtered.Count;
        var items = filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(j => new JobDto(
                j.Id, j.Title, j.CompanyName, j.Location, j.IsRemote,
                j.ExternalApplyUrl, j.SalaryMin, j.SalaryMax, j.SalaryCurrency,
                j.EmploymentType, j.ExperienceLevel, j.PostedAt, j.CreatedAt))
            .ToList();

        return new GetJobPostingsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
