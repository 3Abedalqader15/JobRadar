using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Stores the result of an LLM-powered ATS compatibility analysis between a user's CV and a specific job.
/// </summary>
public sealed class CvJobMatchAnalysis : Entity<Guid>
{
    public Guid CvId { get; private set; }
    public Guid JobId { get; private set; }

    /// <summary>ATS compatibility score from 0 to 100 as determined by the LLM.</summary>
    public int AtsScore { get; private set; }

    /// <summary>Keywords present in the job description but missing from the CV.</summary>
    public string[] MissingKeywords { get; private set; } = Array.Empty<string>();

    /// <summary>Actionable improvement suggestions returned by the LLM (stored as plain text or JSON).</summary>
    public string Suggestions { get; private set; } = string.Empty;

    public DateTime AnalyzedAt { get; private set; }

    // Navigations
    public Cv? Cv { get; private set; }
    public Job? Job { get; private set; }

    // EF Core constructor
    private CvJobMatchAnalysis() { }

    private CvJobMatchAnalysis(
        Guid id,
        Guid cvId,
        Guid jobId,
        int atsScore,
        string[] missingKeywords,
        string suggestions) : base(id)
    {
        CvId = cvId;
        JobId = jobId;
        AtsScore = atsScore;
        MissingKeywords = missingKeywords;
        Suggestions = suggestions;
        AnalyzedAt = DateTime.UtcNow;
    }

    public static CvJobMatchAnalysis Create(
        Guid cvId,
        Guid jobId,
        int atsScore,
        string[] missingKeywords,
        string suggestions)
        => new(Guid.NewGuid(), cvId, jobId, atsScore, missingKeywords, suggestions);
}
