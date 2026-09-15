using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostingById;

public sealed class GetJobPostingByIdQueryHandler
    : IRequestHandler<GetJobPostingByIdQuery, JobDetailDto>
{
    private readonly IJobRepository _repository;

    public GetJobPostingByIdQueryHandler(IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<JobDetailDto> Handle(
        GetJobPostingByIdQuery request,
        CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Job), request.Id);

        return new JobDetailDto(
            job.Id,
            job.Title,
            job.CompanyName,
            job.Location,
            job.IsRemote,
            job.Description,
            job.ExternalApplyUrl,
            job.SalaryMin,
            job.SalaryMax,
            job.SalaryCurrency,
            job.EmploymentType.ToString(),
            job.ExperienceLevel.ToString(),
            job.PostedAt,
            job.CreatedAt,
            job.UpdatedAt);
    }
}
