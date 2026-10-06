namespace JobRadar.Application.Models;

public sealed record CvMatchAnalysisResult(
    int MatchScore,
    string[] MissingKeywords,
    string Summary
);
