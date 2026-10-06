using FluentValidation;
using FluentValidation.Results;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Application.Common.Validators;
using JobRadar.Application.Messages;
using JobRadar.Domain.Entities;
using MassTransit;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication;

public sealed class SubmitJobApplicationCommandHandler : IRequestHandler<SubmitJobApplicationCommand, Guid>
{
    private readonly IJobRepository _jobRepository;
    private readonly IUserJobApplicationRepository _applicationRepository;
    private readonly IJobApplicationQuestionRepository _questionRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICvUploadValidator _cvUploadValidator;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitJobApplicationCommandHandler(
        IJobRepository jobRepository,
        IUserJobApplicationRepository applicationRepository,
        IJobApplicationQuestionRepository questionRepository,
        IFileStorageService fileStorageService,
        ICvUploadValidator cvUploadValidator,
        IPublishEndpoint publishEndpoint,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository;
        _applicationRepository = applicationRepository;
        _questionRepository = questionRepository;
        _fileStorageService = fileStorageService;
        _cvUploadValidator = cvUploadValidator;
        _publishEndpoint = publishEndpoint;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(SubmitJobApplicationCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Job exists and is active
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null || !job.IsActive)
        {
            throw new NotFoundException(nameof(Job), request.JobId);
        }

        // 2. Reject aggregated jobs — enhanced applications are only for officially posted company jobs
        if (!job.CompanyId.HasValue)
        {
            throw new InvalidOperationException("Enhanced applications with screening questions and CV upload are only supported for officially posted company jobs.");
        }

        // 3. Duplicate application check
        var existingApplications = await _applicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existingApplications.Any(a => a.JobId == request.JobId))
        {
            throw new InvalidOperationException("User has already applied for this job.");
        }

        // 4. Required-question validation listing missing question titles
        var questions = await _questionRepository.GetByJobIdAsync(request.JobId, cancellationToken);
        var answerDict = request.Answers?
            .Where(a => a != null)
            .ToDictionary(a => a.QuestionId, a => a.AnswerText) 
            ?? new Dictionary<Guid, string>();

        var missingRequiredQuestions = questions
            .Where(q => q.IsRequired && (!answerDict.TryGetValue(q.Id, out var ans) || string.IsNullOrWhiteSpace(ans)))
            .Select(q => q.QuestionText)
            .ToList();

        if (missingRequiredQuestions.Count > 0)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    nameof(request.Answers),
                    $"The following required screening questions were not answered: {string.Join(", ", missingRequiredQuestions)}")
            });
        }

        // 5. Validate CV upload stream & headers (size, extension, MIME, and magic-byte signature)
        var cvValidation = await _cvUploadValidator.ValidateAsync(
            new CvUploadInput(request.CvStream, request.CvFileName, request.CvContentType, request.CvFileSize),
            cancellationToken);

        if (!cvValidation.IsValid)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("CvFile", cvValidation.ErrorMessage!)
            });
        }

        // 6. Upload CV to Cloudflare R2
        string storedFileKey = await _fileStorageService.UploadAsync(
            request.CvStream,
            request.CvFileName,
            request.CvContentType,
            cancellationToken);

        // 7. Create UserJobApplication entity
        var application = UserJobApplication.CreateDetailed(
            request.UserId,
            request.JobId,
            request.ApplicantFullName,
            request.ApplicantEmail,
            request.ApplicantPhone,
            storedFileKey,
            request.CvFileName);

        // Add submitted answers
        if (request.Answers != null && request.Answers.Count > 0)
        {
            var validQuestionIds = questions.Select(q => q.Id).ToHashSet();
            foreach (var answerDto in request.Answers)
            {
                if (validQuestionIds.Contains(answerDto.QuestionId))
                {
                    var answerEntity = JobApplicationAnswer.Create(
                        application.Id,
                        answerDto.QuestionId,
                        answerDto.AnswerText);

                    application.AddAnswer(answerEntity);
                }
            }
        }

        // 8. Increment applicant count on Job (SAME field 'ApplicantsClickCount' used by frontend Live Applicant Counter)
        job.IncrementApplicantClicks();

        // 9. Stage entities
        await _applicationRepository.AddAsync(application, cancellationToken);
        await _jobRepository.UpdateAsync(job, cancellationToken);

        // 10. Publish CvAnalysisRequestedEvent via Outbox in the same transaction
        await _publishEndpoint.Publish(
            new CvAnalysisRequestedEvent(
                application.Id,
                job.Id,
                storedFileKey,
                request.CvFileName,
                DateTime.UtcNow),
            cancellationToken);

        // 11. Atomic transaction commit
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return application.Id;
    }
}
