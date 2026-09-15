using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;

public sealed class DeleteJobPostingCommandHandler : IRequestHandler<DeleteJobPostingCommand>
{
    private readonly IJobRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteJobPostingCommandHandler(
        IJobRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteJobPostingCommand request, CancellationToken cancellationToken)
    {
        var job = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Job), request.Id);

        await _repository.DeleteAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
