using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;

public sealed class DeleteJobPostingCommandHandler : IRequestHandler<DeleteJobPostingCommand>
{
    private readonly IJobPostingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteJobPostingCommandHandler(
        IJobPostingRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteJobPostingCommand request, CancellationToken cancellationToken)
    {
        var jobPosting = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.JobPosting), request.Id);

        await _repository.DeleteAsync(jobPosting, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
