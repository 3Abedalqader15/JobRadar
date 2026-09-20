namespace JobRadar.Application.Abstractions;

/// <summary>
/// Generates embedding vectors for text using an AI model.
/// Implementations live in Infrastructure (Gemini, etc.).
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a dense embedding vector from the given text.
    /// </summary>
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default);
}
