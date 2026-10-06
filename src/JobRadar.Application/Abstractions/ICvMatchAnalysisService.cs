using JobRadar.Application.Models;

namespace JobRadar.Application.Abstractions;

public interface ICvMatchAnalysisService
{
    /// <summary>
    /// Evaluates applicant CV text against a job title and description using Gemini AI structured output.
    /// </summary>
    Task<CvMatchAnalysisResult?> AnalyzeMatchAsync(
        string cvText,
        string jobTitle,
        string jobDescription,
        CancellationToken cancellationToken = default);
}
