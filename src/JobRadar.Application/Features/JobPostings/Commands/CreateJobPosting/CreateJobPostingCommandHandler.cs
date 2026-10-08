using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;

public sealed class CreateJobPostingCommandHandler
    : IRequestHandler<CreateJobPostingCommand, Guid>
{
    private readonly IJobRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmbeddingService _embeddingService;
    private readonly IJobRealtimeNotifier _notifier;

    public CreateJobPostingCommandHandler(
        IJobRepository repository,
        ICompanyRepository companyRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IEmbeddingService embeddingService,
        IJobRealtimeNotifier notifier)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _embeddingService = embeddingService;
        _notifier = notifier;
    }

    public async Task<Guid> Handle(
        CreateJobPostingCommand request,
        CancellationToken cancellationToken)
    {
        Guid? assignedCompanyId = null;
        string companyName = request.CompanyName;

        if (_currentUserService.IsHr)
        {
            if (!_currentUserService.CompanyId.HasValue)
            {
                throw new ForbiddenException("HR user has no assigned company. Action forbidden.");
            }

            assignedCompanyId = _currentUserService.CompanyId.Value;
            var company = await _companyRepository.GetByIdAsync(assignedCompanyId.Value, cancellationToken);
            if (company != null)
            {
                companyName = company.Name;
            }
        }
        else if (_currentUserService.IsAdmin)
        {
            assignedCompanyId = request.CompanyId;
            if (assignedCompanyId.HasValue)
            {
                var company = await _companyRepository.GetByIdAsync(assignedCompanyId.Value, cancellationToken);
                if (company != null)
                {
                    companyName = company.Name;
                }
            }
        }
        else
        {
            throw new ForbiddenException("You do not have permission to create job postings.");
        }

        var job = Job.Create(
            request.SourceId,
            request.Title,
            companyName,
            request.Description,
            request.Location,
            request.IsRemote,
            request.EmploymentType,
            request.ExperienceLevel,
            request.ExternalApplyUrl,
            request.PostedAt,
            request.RawPostId,
            assignedCompanyId);

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

        try
        {
            await _notifier.NotifyJobCreatedAsync(
                job.Id,
                job.Title,
                job.CompanyName,
                job.Location,
                job.IsRemote,
                job.EmploymentType,
                job.ExperienceLevel,
                job.SalaryMin,
                job.SalaryMax,
                job.SalaryCurrency,
                Array.Empty<string>(),
                job.ExternalApplyUrl,
                job.PostedAt,
                cancellationToken);
        }
        catch
        {
            // Real-time broadcast failure should not block job creation
        }

        return job.Id;
    }
}
