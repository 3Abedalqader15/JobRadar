using MediatR;

namespace JobRadar.Application.Features.JobApplications.Queries.GetAllApplications;

public record GetAllApplicationsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? JobId = null,
    string? SortBy = null
) : IRequest<GetAllApplicationsResponse>;

public record GetAllApplicationsResponse(
    List<ApplicationSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record ApplicationSummaryDto(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string CompanyName,
    Guid UserId,
    string UserEmail,
    string UserFullName,
    string Status,
    DateTime AppliedAt,
    string ApplicantFullName,
    string ApplicantEmail,
    string ApplicantPhone,
    string? CvOriginalFileName,
    string? CvFilePath,
    int? AiMatchScore,
    string AiAnalysisStatus,
    string[]? AiMissingKeywords,
    string? AiAnalysisSummary,
    string? AiMissingKeywordEvidence = null,
    string? AiScoreBreakdown = null,
    bool SuspiciousInstructionsDetected = false,
    string? AnalysisPromptVersion = null
);
