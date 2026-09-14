using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Queries.GetJobPostingById;

public sealed class GetJobPostingByIdQueryHandler
    : IRequestHandler<GetJobPostingByIdQuery, JobPostingDetailDto>
{
    private readonly IJobPostingRepository _repository;

    public GetJobPostingByIdQueryHandler(IJobPostingRepository repository)
    {
        _repository = repository;
    }

    public async Task<JobPostingDetailDto> Handle(
        GetJobPostingByIdQuery request,
        CancellationToken cancellationToken)
    {
        var posting = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.JobPosting), request.Id);

        return new JobPostingDetailDto(
            posting.Id,
            posting.Title,
            posting.Company,
            posting.Location,
            posting.IsRemote,
            posting.Description,
            posting.SourceUrl,
            posting.SalaryMin,
            posting.SalaryMax,
            posting.SalaryCurrency,
            posting.JobType.ToString(),
            posting.ExperienceLevel.ToString(),
            posting.PostedAt,
            posting.CreatedAt,
            posting.UpdatedAt);
    }
}
