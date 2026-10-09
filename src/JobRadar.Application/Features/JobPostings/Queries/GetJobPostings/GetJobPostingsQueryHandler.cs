using JobRadar.Application.Abstractions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;

public sealed class GetJobPostingsQueryHandler
    : IRequestHandler<GetJobPostingsQuery, GetJobPostingsResponse>
{
    private readonly IJobRepository _repository;
    private readonly ICurrentUserService _currentUserService;

    public GetJobPostingsQueryHandler(
        IJobRepository repository,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _currentUserService = currentUserService;
    }

    public async Task<GetJobPostingsResponse> Handle(
        GetJobPostingsQuery request,
        CancellationToken cancellationToken)
    {
        // Fail closed for HR user without an assigned company
        if (_currentUserService.IsHr && !_currentUserService.CompanyId.HasValue)
        {
            return new GetJobPostingsResponse(Array.Empty<JobDto>(), 0, request.Page, request.PageSize);
        }

        var all = await _repository.GetAllAsync(cancellationToken);

        // Apply scoping rules
        IEnumerable<Domain.Entities.Job> scoped = all;
        if (_currentUserService.IsHr)
        {
            var hrCompanyId = _currentUserService.CompanyId!.Value;
            scoped = scoped.Where(j => j.CompanyId == hrCompanyId);
        }
        else if (_currentUserService.IsAdmin && request.CompanyId.HasValue)
        {
            scoped = scoped.Where(j => j.CompanyId == request.CompanyId.Value);
        }

        // Apply optional title/company search
        var filtered = string.IsNullOrWhiteSpace(request.SearchTerm)
            ? scoped
            : scoped.Where(j =>
                j.Title.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                j.CompanyName.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase));

        // Apply source filter if provided ("direct", "crawled", or "all")
        if (!string.IsNullOrWhiteSpace(request.SourceFilter))
        {
            var filter = request.SourceFilter.Trim().ToLowerInvariant();
            if (filter == "direct")
            {
                filtered = filtered.Where(j => j.IsDirectPlatformPost);
            }
            else if (filter == "crawled")
            {
                filtered = filtered.Where(j => !j.IsDirectPlatformPost);
            }
        }

        var list = filtered.ToList();
        var totalCount = list.Count;
        var items = list
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(j => new JobDto(
                j.Id, j.Title, j.CompanyName, j.Location, j.IsRemote,
                j.ExternalApplyUrl, j.SalaryMin, j.SalaryMax, j.SalaryCurrency,
                j.EmploymentType, j.ExperienceLevel, j.PostedAt, j.CreatedAt,
                j.CompanyId,
                IsDirect: j.IsDirectPlatformPost,
                SourceName: j.IsDirectPlatformPost ? "JobRadar Direct" : (j.Source?.Name ?? "External Crawl")))
            .ToList();

        return new GetJobPostingsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
