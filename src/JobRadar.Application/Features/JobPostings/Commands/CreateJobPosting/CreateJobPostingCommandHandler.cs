using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed class CreateJobPostingCommandHandler
    : IRequestHandler<CreateJobPostingCommand, Guid>
{
    private readonly IJobRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;

    public CreateJobPostingCommandHandler(
        IJobRepository repository,
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
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

        try
        {
            var textToEmbed = $"{job.Title} {job.CompanyName} {job.Location} {job.Description}";
            var embedding = await _embeddingService.GenerateAsync(textToEmbed, cancellationToken);
            job.SetEmbedding(embedding);
        }
        catch
        {
            // AI embedding generation failure should not block job creation
        }

        await _repository.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
