using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Tracks a user's application to a specific job, including pipeline status progression,
/// candidate contact details, uploaded CV, screening answers, and AI match scoring.
/// </summary>
public sealed class UserJobApplication : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid JobId { get; private set; }
    public DateTime AppliedAt { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // ── Candidate Contact Details (captured at submission time) ───────────────
    public string ApplicantFullName { get; private set; } = string.Empty;
    public string ApplicantEmail { get; private set; } = string.Empty;
    public string ApplicantPhone { get; private set; } = string.Empty;

    // ── Uploaded CV Metadata ─────────────────────────────────────────────────
    public string? CvFilePath { get; private set; }
    public string? CvOriginalFileName { get; private set; }

    // ── AI Match Analysis ────────────────────────────────────────────────────
    public int? AiMatchScore { get; private set; }
    public string[]? AiMissingKeywords { get; private set; }
    public string? AiMissingKeywordEvidence { get; private set; }
    public string? AiScoreBreakdown { get; private set; }
    public bool SuspiciousInstructionsDetected { get; private set; }
    public string? AnalysisPromptVersion { get; private set; }
    public string? AiAnalysisSummary { get; private set; }
    public AiAnalysisStatus AiAnalysisStatus { get; private set; } = AiAnalysisStatus.Pending;


    // ── Navigations ──────────────────────────────────────────────────────────
    public ApplicationUser? User { get; private set; }
    public Job? Job { get; private set; }

    public IReadOnlyCollection<JobApplicationAnswer> Answers => _answers.AsReadOnly();
    private readonly List<JobApplicationAnswer> _answers = new();

    // EF Core constructor
    private UserJobApplication() { }

    private UserJobApplication(Guid id, Guid userId, Guid jobId) : base(id)
    {
        UserId = userId;
        JobId = jobId;
        Status = ApplicationStatus.Applied;
        AiAnalysisStatus = AiAnalysisStatus.Pending;
        AppliedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static UserJobApplication Create(Guid userId, Guid jobId)
        => new(Guid.NewGuid(), userId, jobId);

    public static UserJobApplication CreateDetailed(
        Guid userId,
        Guid jobId,
        string applicantFullName,
        string applicantEmail,
        string applicantPhone,
        string? cvFilePath = null,
        string? cvOriginalFileName = null,
        string? notes = null)
    {
        var app = new UserJobApplication(Guid.NewGuid(), userId, jobId)
        {
            ApplicantFullName = applicantFullName ?? string.Empty,
            ApplicantEmail = applicantEmail ?? string.Empty,
            ApplicantPhone = applicantPhone ?? string.Empty,
            CvFilePath = cvFilePath,
            CvOriginalFileName = cvOriginalFileName,
            Notes = notes,
            AiAnalysisStatus = AiAnalysisStatus.Pending
        };

        return app;
    }

    public void UpdateStatus(ApplicationStatus newStatus, string? notes = null)
    {
        Status = newStatus;
        if (notes is not null) Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAiAnalysisProcessing()
    {
        AiAnalysisStatus = AiAnalysisStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteAiAnalysis(
        int score,
        string[]? missingKeywords,
        string? summary,
        string? missingKeywordEvidenceJson = null,
        string? scoreBreakdownJson = null,
        bool suspiciousInstructionsDetected = false,
        string? analysisPromptVersion = null)
    {
        AiMatchScore = Math.Clamp(score, 0, 100);
        AiMissingKeywords = missingKeywords ?? Array.Empty<string>();
        AiMissingKeywordEvidence = missingKeywordEvidenceJson;
        AiScoreBreakdown = scoreBreakdownJson;
        SuspiciousInstructionsDetected = suspiciousInstructionsDetected;
        AnalysisPromptVersion = analysisPromptVersion;
        AiAnalysisSummary = summary;
        AiAnalysisStatus = AiAnalysisStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetInsufficientJobDescription(string reason = "Job description is too short to perform reliable AI CV matching analysis.")
    {
        AiAnalysisStatus = AiAnalysisStatus.InsufficientJobDescription;
        AiAnalysisSummary = reason;
        AiMatchScore = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void FailAiAnalysis(string failureReason)
    {
        AiAnalysisStatus = AiAnalysisStatus.Failed;
        AiAnalysisSummary = failureReason;
        UpdatedAt = DateTime.UtcNow;
    }


    public void AddAnswer(JobApplicationAnswer answer)
    {
        _answers.Add(answer);
        UpdatedAt = DateTime.UtcNow;
    }
}
