using JobRadar.Application.Abstractions;
using JobRadar.Domain.Enums;
using MediatR;
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
    private readonly ILogger<ProcessCvAnalysisCommandHandler> _logger;

    public ProcessCvAnalysisCommandHandler(
        IUserJobApplicationRepository applicationRepository,
        IJobRepository jobRepository,
        IFileStorageService fileStorage,
        IDocumentTextExtractionService textExtractor,
        ICvMatchAnalysisService cvMatchService,
        IUnitOfWork unitOfWork,
        ILogger<ProcessCvAnalysisCommandHandler> logger)
    {
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _fileStorage = fileStorage;
        _textExtractor = textExtractor;
        _cvMatchService = cvMatchService;
        _unitOfWork = unitOfWork;
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

        // 1. Idempotency Check: skip if already completed (call Gemini zero times)
        if (application.AiAnalysisStatus == AiAnalysisStatus.Completed)
        {
            _logger.LogInformation("CV analysis for application {ApplicationId} is already completed. Skipping.", request.ApplicationId);
            return;
        }

        // 2. Mark as Processing
        application.SetAiAnalysisProcessing();
        await _applicationRepository.UpdateAsync(application, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Process with fallback to FailAiAnalysis on error
        try
        {
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

            var job = await _jobRepository.GetByIdAsync(application.JobId, cancellationToken);
            var jobTitle = job?.Title ?? string.Empty;
            var jobDescription = job?.Description ?? string.Empty;

            var matchResult = await _cvMatchService.AnalyzeMatchAsync(cvText, jobTitle, jobDescription, cancellationToken);
            if (matchResult == null)
            {
                throw new InvalidOperationException("AI match analysis returned null or empty result.");
            }

            application.CompleteAiAnalysis(matchResult.MatchScore, matchResult.MissingKeywords, matchResult.Summary);
            await _applicationRepository.UpdateAsync(application, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully completed AI match analysis for application {ApplicationId} (Score: {Score})",
                application.Id,
                matchResult.MatchScore);
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
