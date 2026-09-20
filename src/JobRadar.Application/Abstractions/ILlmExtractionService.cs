using JobRadar.Application.Models;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Calls the LLM to extract structured job data from raw post text.
/// Implementations live in Infrastructure (Gemini, etc.).
/// </summary>
public interface ILlmExtractionService
{
    /// <summary>
    /// Extracts structured job data from the given raw text.
    /// Returns <c>null</c> if the content is not a job posting or confidence is too low.
    /// </summary>
    Task<JobExtractionResult?> ExtractJobAsync(
        string rawText,

        CancellationToken cancellationToken = default);
}
