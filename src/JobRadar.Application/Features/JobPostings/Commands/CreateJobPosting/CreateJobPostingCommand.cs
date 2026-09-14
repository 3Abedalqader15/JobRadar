using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed record CreateJobPostingCommand(
    string Title,
    string Company,
    string Description,
    string? Location,
    bool IsRemote,
    string? SourceUrl,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    JobType JobType,
    ExperienceLevel ExperienceLevel,
    DateTime? PostedAt,
    Guid? JobSourceId) : IRequest<Guid>;
