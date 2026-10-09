using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobRadar.Infrastructure.Services;

public sealed class CvMatchWeightsOptions
{
    public const string SectionName = "Gemini:CvMatch:Weights";

    public int RequiredSkills { get; set; } = 50;
    public int RequiredSkillsMatch { get => RequiredSkills; set => RequiredSkills = value; }

    public int ExperienceSeniority { get; set; } = 25;
    public int ExperienceLevelMatch { get => ExperienceSeniority; set => ExperienceSeniority = value; }

    public int NiceToHave { get; set; } = 15;
    public int NiceToHaveSkillsMatch { get => NiceToHave; set => NiceToHave = value; }

    public int Domain { get; set; } = 10;
    public int DomainRelevance { get => Domain; set => Domain = value; }

    public bool ValidateWeights()
    {
        return RequiredSkills >= 0 && ExperienceSeniority >= 0 && NiceToHave >= 0 && Domain >= 0 &&
               (RequiredSkills + ExperienceSeniority + NiceToHave + Domain == 100);
    }

    public void Validate()
    {
        if (RequiredSkills < 0 || ExperienceSeniority < 0 || NiceToHave < 0 || Domain < 0)
        {
            throw new InvalidOperationException("Scoring weights cannot be negative.");
        }

        var sum = RequiredSkills + ExperienceSeniority + NiceToHave + Domain;
        if (sum != 100)
        {
            throw new InvalidOperationException(
                $"CV match scoring weights must sum to exactly 100 at startup, but summed to {sum} " +
                $"(RequiredSkills={RequiredSkills}, ExperienceSeniority={ExperienceSeniority}, NiceToHave={NiceToHave}, Domain={Domain}).");
        }
    }
}

public static class CvMatchScoreCalculator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Computes deterministic weighted score from model sub-scores.
    /// If any sub-score is null (e.g. no nice-to-have requirements in the job),
    /// weights are renormalized over the non-null sub-scores.
    /// </summary>
    public static (int Score, string BreakdownJson) CalculateWeightedScore(
        int? requiredSkillsScore,
        int? experienceSeniorityScore,
        int? niceToHaveScore,
        int? domainScore,
        CvMatchWeightsOptions weights)
    {
        weights.Validate();

        double weightedSum = 0;
        int nonNullWeightSum = 0;

        if (requiredSkillsScore.HasValue)
        {
            var clamped = Math.Clamp(requiredSkillsScore.Value, 0, 100);
            weightedSum += clamped * weights.RequiredSkills;
            nonNullWeightSum += weights.RequiredSkills;
        }

        if (experienceSeniorityScore.HasValue)
        {
            var clamped = Math.Clamp(experienceSeniorityScore.Value, 0, 100);
            weightedSum += clamped * weights.ExperienceSeniority;
            nonNullWeightSum += weights.ExperienceSeniority;
        }

        if (niceToHaveScore.HasValue)
        {
            var clamped = Math.Clamp(niceToHaveScore.Value, 0, 100);
            weightedSum += clamped * weights.NiceToHave;
            nonNullWeightSum += weights.NiceToHave;
        }

        if (domainScore.HasValue)
        {
            var clamped = Math.Clamp(domainScore.Value, 0, 100);
            weightedSum += clamped * weights.Domain;
            nonNullWeightSum += weights.Domain;
        }

        int finalScore = 0;
        if (nonNullWeightSum > 0)
        {
            finalScore = (int)Math.Round(weightedSum / nonNullWeightSum, MidpointRounding.AwayFromZero);
            finalScore = Math.Clamp(finalScore, 0, 100);
        }

        var breakdown = new ScoreBreakdownDto
        {
            RequiredSkillsScore = requiredSkillsScore.HasValue ? Math.Clamp(requiredSkillsScore.Value, 0, 100) : null,
            ExperienceSeniorityScore = experienceSeniorityScore.HasValue ? Math.Clamp(experienceSeniorityScore.Value, 0, 100) : null,
            NiceToHaveScore = niceToHaveScore.HasValue ? Math.Clamp(niceToHaveScore.Value, 0, 100) : null,
            DomainScore = domainScore.HasValue ? Math.Clamp(domainScore.Value, 0, 100) : null,
            CalculatedMatchScore = finalScore,
            WeightsConfigured = new WeightsDto
            {
                RequiredSkills = weights.RequiredSkills,
                ExperienceSeniority = weights.ExperienceSeniority,
                NiceToHave = weights.NiceToHave,
                Domain = weights.Domain
            },
            RenormalizedWeightSum = nonNullWeightSum
        };

        var breakdownJson = JsonSerializer.Serialize(breakdown, JsonOptions);
        return (finalScore, breakdownJson);
    }

    private sealed class ScoreBreakdownDto
    {
        [JsonPropertyName("required_skills")]
        public int? RequiredSkillsScore { get; set; }

        [JsonPropertyName("experience_seniority")]
        public int? ExperienceSeniorityScore { get; set; }

        [JsonPropertyName("nice_to_have")]
        public int? NiceToHaveScore { get; set; }

        [JsonPropertyName("domain")]
        public int? DomainScore { get; set; }

        [JsonPropertyName("calculated_match_score")]
        public int CalculatedMatchScore { get; set; }

        [JsonPropertyName("weights_configured")]
        public WeightsDto WeightsConfigured { get; set; } = new();

        [JsonPropertyName("renormalized_weight_sum")]
        public int RenormalizedWeightSum { get; set; }
    }

    private sealed class WeightsDto
    {
        [JsonPropertyName("required_skills")]
        public int RequiredSkills { get; set; }

        [JsonPropertyName("experience_seniority")]
        public int ExperienceSeniority { get; set; }

        [JsonPropertyName("nice_to_have")]
        public int NiceToHave { get; set; }

        [JsonPropertyName("domain")]
        public int Domain { get; set; }
    }
}
