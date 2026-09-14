using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;

public sealed record GetJobPostingsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<GetJobPostingsResponse>;

public sealed record JobPostingDto(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    bool IsRemote,
    string? SourceUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    JobType JobType,
    ExperienceLevel ExperienceLevel,
    DateTime PostedAt,
    DateTime CreatedAt);

public sealed record GetJobPostingsResponse(
    IReadOnlyList<JobPostingDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
