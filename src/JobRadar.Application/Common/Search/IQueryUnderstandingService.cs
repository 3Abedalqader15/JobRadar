namespace JobRadar.Application.Common.Search;

public sealed record QueryUnderstandingResult(
    string OriginalQuery,
    string NormalizedQuery,
    IReadOnlyList<string> ExpandedTerms,
    bool? InferredIsRemote,
    IReadOnlyList<string> InferredSkills,
    bool IsAmbiguousNaturalLanguage,
    string? CanonicalRole = null);

public interface IQueryUnderstandingService
{
    Task<QueryUnderstandingResult> UnderstandQueryAsync(string? rawQuery, CancellationToken cancellationToken = default);
}
