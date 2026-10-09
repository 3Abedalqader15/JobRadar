namespace JobRadar.Infrastructure.Prompts;

public interface IGeminiPromptProvider
{
    string GetCvMatchPrompt(string? version = null);
    string GetJobExtractionPrompt(string? version = null, float confidenceThreshold = 0.70f);
    string CurrentCvMatchVersion { get; }
    string CurrentExtractionVersion { get; }
}
