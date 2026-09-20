using JobRadar.Domain.Enums;

namespace JobRadar.Application.Models;

/// <summary>
/// The structured result returned by the LLM extraction service.
/// Maps directly to the JSON schema sent to Gemini.
/// </summary>
public sealed class JobExtractionResult
{
    /// <summary>Whether the content is a real job posting at all.</summary>
    public bool IsJobPosting { get; set; }

    /// <summary>
    /// Confidence score from 0.0 to 1.0 reflecting how certain the model is
    /// that this is a valid, complete job posting.
    /// </summary>
    public float Confidence { get; set; }

    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsRemote { get; set; }

    /// <summary>Maps to the <see cref="EmploymentType"/> enum string value.</summary>
    public string EmploymentType { get; set; } = "FullTime";

    /// <summary>Maps to the <see cref="ExperienceLevel"/> enum string value.</summary>
    public string ExperienceLevel { get; set; } = "MidLevel";

    /// <summary>Skills required for the job (names only, will be upserted).</summary>
    public List<string> SkillsRequired { get; set; } = new();

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "USD", "EUR".</summary>
    public string? SalaryCurrency { get; set; }

    public string? ExternalApplyUrl { get; set; }
}
