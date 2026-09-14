using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed class CreateJobPostingCommandHandler
    : IRequestHandler<CreateJobPostingCommand, Guid>
{
    private readonly IJobPostingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateJobPostingCommandHandler(
        IJobPostingRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(
        CreateJobPostingCommand request,
        CancellationToken cancellationToken)
    {
        var jobPosting = JobPosting.Create(
            request.Title,
            request.Company,
            request.Description,
            request.Location,
            request.IsRemote,
            request.SourceUrl,
            request.JobType,
            request.ExperienceLevel,
            request.PostedAt,
            request.JobSourceId);

        if (request.SalaryMin.HasValue && request.SalaryMax.HasValue)
        {
            jobPosting.UpdateSalaryRange(
                request.SalaryMin.Value,
                request.SalaryMax.Value,
                request.SalaryCurrency ?? "USD");
        }

        await _repository.AddAsync(jobPosting, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return jobPosting.Id;
    }
}
