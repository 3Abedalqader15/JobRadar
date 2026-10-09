using JobRadar.Application.Abstractions;
using JobRadar.Application.Common;
using JobRadar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.JobApplications.Commands.ProcessCvAnalysis;

public sealed class ProcessCvAnalysisCommandHandler : IRequestHandler<ProcessCvAnalysisCommand>
{
    private readonly IUserJobApplicationRepository _applicationRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentTextExtractionService _textExtractor;
    private readonly ICvMatchAnalysisService _cvMatchService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessCvAnalysisCommandHandler> _logger;

    public ProcessCvAnalysisCommandHandler(
        IUserJobApplicationRepository applicationRepository,
        IJobRepository jobRepository,
        IFileStorageService fileStorage,
        IDocumentTextExtractionService textExtractor,
        ICvMatchAnalysisService cvMatchService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<ProcessCvAnalysisCommandHandler> logger)
    {
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _fileStorage = fileStorage;
        _textExtractor = textExtractor;
        _cvMatchService = cvMatchService;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Handle(ProcessCvAnalysisCommand request, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (application is null)
        {
            _logger.LogWarning("Application {ApplicationId} not found for CV analysis.", request.ApplicationId);
            return;
        }

        // 1. Idempotency Check: skip if already completed unless forced
        if (!request.Force && (application.AiAnalysisStatus == AiAnalysisStatus.Completed ||
                               application.AiAnalysisStatus == AiAnalysisStatus.InsufficientJobDescription))
        {
            _logger.LogInformation("CV analysis for application {ApplicationId} is already in state {Status}. Skipping.",
                request.ApplicationId, application.AiAnalysisStatus);
            return;
        }

        // 2. Mark as Processing
        application.SetAiAnalysisProcessing();
        await _applicationRepository.UpdateAsync(application, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Process with fallback to FailAiAnalysis on error
        try
        {
            var job = await _jobRepository.GetByIdAsync(application.JobId, cancellationToken);
            var jobTitle = job?.Title ?? string.Empty;
            var jobDescription = job?.Description ?? string.Empty;

            // Guard A: Check if job is marked ineligible for CV matching (e.g. LinkedIn listing stub)
            if (job is not null && !job.IsCvMatchEligible)
            {
                _logger.LogInformation(
                    "Job {JobId} is marked as ineligible for CV matching. Setting status to InsufficientJobDescription.",
                    job.Id);

                application.SetInsufficientJobDescription("Job was ingested from a listing stub without full description and is not eligible for automated CV matching.");
                await _applicationRepository.UpdateAsync(application, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            // Guard B: Minimum description length check (after stripping HTML)
            var minDescriptionLength = int.TryParse(_configuration["Gemini:CvMatch:MinJobDescriptionLength"], out var parsedMin)
                ? parsedMin
                : 300;
            var strippedDescription = EvidenceVerificationService.StripHtml(jobDescription);
            if (strippedDescription.Length < minDescriptionLength)
            {
                _logger.LogInformation(
                    "Job {JobId} description length ({Length} chars stripped) is below minimum required ({MinLength} chars). Skipping Gemini call.",
                    application.JobId, strippedDescription.Length, minDescriptionLength);

                application.SetInsufficientJobDescription(
                    $"Job description is too short ({strippedDescription.Length} characters) to perform reliable AI CV matching analysis. Minimum required: {minDescriptionLength}.");
                await _applicationRepository.UpdateAsync(application, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(application.CvFilePath))
            {
                throw new InvalidOperationException("No CV file path found for application.");
            }

            await using var cvStream = await _fileStorage.DownloadAsync(application.CvFilePath, cancellationToken);
            if (cvStream == null)
            {
                throw new FileNotFoundException($"Unable to download CV file from storage: {application.CvFilePath}");
            }

            var fileName = application.CvOriginalFileName ?? "cv.pdf";
            var cvText = await _textExtractor.ExtractTextAsync(cvStream, fileName, cancellationToken);

            if (string.IsNullOrWhiteSpace(cvText))
            {
                throw new InvalidOperationException("Failed to extract any readable text from the uploaded CV file.");
            }

            var matchResult = await _cvMatchService.AnalyzeMatchAsync(
                cvText,
                jobTitle,
                jobDescription,
                application.Id,
                cancellationToken);

            if (matchResult == null)
            {
                throw new InvalidOperationException("AI match analysis returned null or empty result.");
            }

            application.CompleteAiAnalysis(
                score: matchResult.MatchScore,
                missingKeywords: matchResult.MissingKeywords,
                summary: matchResult.Summary,
                missingKeywordEvidenceJson: matchResult.MissingKeywordEvidenceJson,
                scoreBreakdownJson: matchResult.ScoreBreakdownJson,
                suspiciousInstructionsDetected: matchResult.SuspiciousInstructionsDetected,
                analysisPromptVersion: matchResult.PromptVersion);

            await _applicationRepository.UpdateAsync(application, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully completed AI match analysis for application {ApplicationId} (Score: {Score}, PromptVersion: {Version}, Suspicious: {Suspicious})",
                application.Id,
                matchResult.MatchScore,
                matchResult.PromptVersion,
                matchResult.SuspiciousInstructionsDetected);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing AI CV analysis for application {ApplicationId}. Marking status as Failed: {Message}",
                application.Id,
                ex.Message);

            // Fallback so application is never stuck in Processing indefinitely
            application.FailAiAnalysis(ex.Message);
            await _applicationRepository.UpdateAsync(application, CancellationToken.None);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }
}
