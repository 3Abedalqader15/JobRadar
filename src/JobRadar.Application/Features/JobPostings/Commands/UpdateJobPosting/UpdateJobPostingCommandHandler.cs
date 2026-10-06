using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.JobPostings.Commands.UpdateJobPosting;

public sealed class UpdateJobPostingCommandHandler : IRequestHandler<UpdateJobPostingCommand>
{
    private readonly IJobRepository _jobRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateJobPostingCommandHandler(
        IJobRepository jobRepository,
        ICompanyRepository companyRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository;
        _companyRepository = companyRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateJobPostingCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), request.Id);

        if (_currentUserService.IsHr)
        {
            if (!_currentUserService.CompanyId.HasValue || job.CompanyId != _currentUserService.CompanyId.Value)
            {
                throw new ForbiddenException("You cannot modify a job posting belonging to another company.");
            }
        }
        else if (_currentUserService.IsAdmin)
        {
            if (request.CompanyId.HasValue && request.CompanyId != job.CompanyId)
            {
                var company = await _companyRepository.GetByIdAsync(request.CompanyId.Value, cancellationToken)
                    ?? throw new NotFoundException(nameof(Company), request.CompanyId.Value);
                job.AssignCompany(company.Id);
            }
            else if (request.CompanyId == null && job.CompanyId != null)
            {
                job.AssignCompany(null);
            }
        }
        else
        {
            throw new ForbiddenException("You do not have permission to modify this job posting.");
        }

        job.UpdateDetails(
            request.Title,
            request.Description,
            request.Location,
            request.IsRemote,
            request.EmploymentType,
            request.ExperienceLevel,
            request.ExternalApplyUrl,
            request.IsActive);

        if (request.SalaryMin.HasValue && request.SalaryMax.HasValue)
        {
            job.UpdateSalaryRange(
                request.SalaryMin.Value,
                request.SalaryMax.Value,
                request.SalaryCurrency ?? "USD");
        }

        await _jobRepository.UpdateAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
