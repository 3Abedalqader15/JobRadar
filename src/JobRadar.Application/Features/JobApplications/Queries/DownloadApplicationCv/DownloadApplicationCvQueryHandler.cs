using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Queries.DownloadApplicationCv;

public sealed class DownloadApplicationCvQueryHandler : IRequestHandler<DownloadApplicationCvQuery, CvDownloadResult>
{
    private readonly IUserJobApplicationRepository _applicationRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;

    public DownloadApplicationCvQueryHandler(
        IUserJobApplicationRepository applicationRepository,
        IJobRepository jobRepository,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService)
    {
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public async Task<CvDownloadResult> Handle(DownloadApplicationCvQuery request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application is null)
        {
            throw new NotFoundException(nameof(UserJobApplication), request.ApplicationId);
        }

        if (string.IsNullOrWhiteSpace(application.CvFilePath))
        {
            throw new NotFoundException("No CV file is attached to this job application.");
        }

        // Three-way authorization check:
        // 1. Admin
        // 2. Same-company HR
        // 3. The applicant themselves
        bool isAuthorized = false;

        if (_currentUserService.IsAdmin)
        {
            isAuthorized = true;
        }
        else if (_currentUserService.UserId.HasValue && _currentUserService.UserId.Value == application.UserId)
        {
            isAuthorized = true;
        }
        else if (_currentUserService.IsHr && _currentUserService.CompanyId.HasValue)
        {
            var job = await _jobRepository.GetByIdAsync(application.JobId, cancellationToken);
            if (job is not null && job.CompanyId.HasValue && job.CompanyId.Value == _currentUserService.CompanyId.Value)
            {
                isAuthorized = true;
            }
        }

        if (!isAuthorized)
        {
            throw new ForbiddenException("You are not authorized to download this applicant's CV.");
        }

        var stream = await _fileStorageService.DownloadAsync(application.CvFilePath, cancellationToken);
        if (stream is null)
        {
            throw new NotFoundException("The CV document could not be found in storage.");
        }

        var fileName = application.CvOriginalFileName ?? "applicant_cv.pdf";
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

        return new CvDownloadResult(stream, contentType, fileName);
    }
}
