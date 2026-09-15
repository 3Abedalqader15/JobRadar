using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed class CreateJobPostingCommandHandler
    : IRequestHandler<CreateJobPostingCommand, Guid>
{
    private readonly IJobRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateJobPostingCommandHandler(
        IJobRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(
        CreateJobPostingCommand request,
        CancellationToken cancellationToken)
    {
        var job = Job.Create(
            request.SourceId,
            request.Title,
            request.CompanyName,
            request.Description,
            request.Location,
            request.IsRemote,
            request.EmploymentType,
            request.ExperienceLevel,
            request.ExternalApplyUrl,
            request.PostedAt,
            request.RawPostId);

        if (request.SalaryMin.HasValue && request.SalaryMax.HasValue)
        {
            job.UpdateSalaryRange(
                request.SalaryMin.Value,
                request.SalaryMax.Value,
                request.SalaryCurrency ?? "USD");
        }

        await _repository.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
