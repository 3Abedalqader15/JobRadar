using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostingById;

public sealed record GetJobPostingByIdQuery(Guid Id) : IRequest<JobDetailDto>;

public sealed record JobDetailDto(
    Guid Id,
    string Title,
    string CompanyName,
    string? Location,
    bool IsRemote,
    string Description,
    string? ExternalApplyUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string EmploymentType,
    string ExperienceLevel,
    DateTime PostedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
