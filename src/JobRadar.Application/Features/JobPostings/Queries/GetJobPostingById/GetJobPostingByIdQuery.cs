using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostingById;

public sealed record GetJobPostingByIdQuery(Guid Id) : IRequest<JobPostingDetailDto>;

public sealed record JobPostingDetailDto(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    bool IsRemote,
    string Description,
    string? SourceUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string JobType,
    string ExperienceLevel,
    DateTime PostedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
