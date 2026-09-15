using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed record CreateJobPostingCommand(
    Guid SourceId,
    string Title,
    string CompanyName,
    string Description,
    string? Location,
    bool IsRemote,
    string? ExternalApplyUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    EmploymentType EmploymentType,
    ExperienceLevel ExperienceLevel,
    DateTime? PostedAt,
    Guid? RawPostId = null) : IRequest<Guid>;
