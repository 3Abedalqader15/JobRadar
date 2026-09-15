using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;

public sealed record GetJobPostingsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<GetJobPostingsResponse>;

public sealed record JobDto(
    Guid Id,
    string Title,
    string CompanyName,
    string? Location,
    bool IsRemote,
    string? ExternalApplyUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    EmploymentType EmploymentType,
    ExperienceLevel ExperienceLevel,
    DateTime PostedAt,
    DateTime CreatedAt);

public sealed record GetJobPostingsResponse(
    IReadOnlyList<JobDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
