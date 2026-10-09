namespace JobRadar.Application.Models;

public sealed record CvMatchAnalysisResult(
    int MatchScore,
    string[] MissingKeywords,
    string Summary,
    string? MissingKeywordEvidenceJson = null,
    string? ScoreBreakdownJson = null,
    bool SuspiciousInstructionsDetected = false,
    string? PromptVersion = null,
    int? RequiredSkillsScore = null,
    int? ExperienceSeniorityScore = null,
    int? NiceToHaveScore = null,
    int? DomainScore = null
);
