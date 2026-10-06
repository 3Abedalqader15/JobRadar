using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.UpdateJobPosting;

public sealed record UpdateJobPostingCommand(
    Guid Id,
    string Title,
    string? Location,
    bool IsRemote,
    string? ExternalApplyUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    EmploymentType EmploymentType,
    ExperienceLevel ExperienceLevel,
    string Description,
    bool IsActive,
    Guid? CompanyId = null) : IRequest;
