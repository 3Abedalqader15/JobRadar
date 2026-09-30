using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Application.Common.Exceptions;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.ApplyForJob;

public sealed class ApplyForJobCommandHandler : IRequestHandler<ApplyForJobCommand, Guid>
{
    private readonly IJobRepository _jobRepository;
    private readonly IUserJobApplicationRepository _applicationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyForJobCommandHandler(
        IJobRepository jobRepository,
        IUserJobApplicationRepository applicationRepository,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository;
        _applicationRepository = applicationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(ApplyForJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null || !job.IsActive)
        {
            throw new NotFoundException(nameof(Job), request.JobId);
        }

        // Check if already applied
        var existingApplications = await _applicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existingApplications.Any(a => a.JobId == request.JobId))
        {
            throw new InvalidOperationException("User has already applied for this job.");
        }

        var application = UserJobApplication.Create(request.UserId, request.JobId);
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            application.UpdateStatus(application.Status, request.Notes);
        }

        job.IncrementApplicantClicks();

        await _applicationRepository.AddAsync(application, cancellationToken);
        await _jobRepository.UpdateAsync(job, cancellationToken);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return application.Id;
    }
}
