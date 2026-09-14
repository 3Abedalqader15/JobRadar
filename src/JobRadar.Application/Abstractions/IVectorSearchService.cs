using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Service for vector similarity search on job postings using pgvector.
/// Implementations live in Infrastructure.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Returns job postings whose embedding vector is nearest to the query vector,
    /// ordered by cosine distance ascending.
    /// </summary>
    Task<IReadOnlyList<JobPosting>> SearchSimilarAsync(
        float[] queryVector,
        int topK = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding vector from the provided text.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
