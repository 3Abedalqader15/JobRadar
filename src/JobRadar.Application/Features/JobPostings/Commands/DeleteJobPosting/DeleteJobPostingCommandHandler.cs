using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;

public sealed class DeleteJobPostingCommandHandler : IRequestHandler<DeleteJobPostingCommand>
{
    private readonly IJobRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobRealtimeNotifier _notifier;

    public DeleteJobPostingCommandHandler(
        IJobRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IJobRealtimeNotifier notifier)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task Handle(DeleteJobPostingCommand request, CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Job), request.Id);

        if (_currentUserService.IsHr)
        {
            if (!_currentUserService.CompanyId.HasValue || job.CompanyId != _currentUserService.CompanyId.Value)
            {
                throw new ForbiddenException("You cannot delete a job posting belonging to another company.");
            }
        }
        else if (_currentUserService.IsAdmin)
        {
            throw new ForbiddenException("You do not have permission to delete this job posting.");
        }

        await _repository.DeleteAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _notifier.NotifyJobDeactivatedAsync(job.Id, cancellationToken);
        }
        catch
        {
            // Real-time broadcast failure should not block deletion
        }
    }
}
