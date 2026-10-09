using Microsoft.Extensions.Configuration;

namespace JobRadar.Infrastructure.Prompts;

public sealed class GeminiPromptProvider : IGeminiPromptProvider
{
    private readonly string _defaultCvMatchVersion;
    private readonly string _defaultExtractionVersion;

    public GeminiPromptProvider(IConfiguration configuration)
    {
        _defaultCvMatchVersion = configuration["Gemini:Prompts:CvMatchVersion"] ?? "v2";
        _defaultExtractionVersion = configuration["Gemini:Prompts:ExtractionVersion"] ?? "v1";
    }

    public string CurrentCvMatchVersion => _defaultCvMatchVersion;
    public string CurrentExtractionVersion => _defaultExtractionVersion;

    public string GetCvMatchPrompt(string? version = null)
    {
        var target = (version ?? _defaultCvMatchVersion).ToLowerInvariant();
        return target switch
        {
            "v1" => CvMatchPrompts.V1,
            "v2" => CvMatchPrompts.V2,
            _ => CvMatchPrompts.V2
        };
    }

    public string GetJobExtractionPrompt(string? version = null, float confidenceThreshold = 0.70f)
    {
        var target = (version ?? _defaultExtractionVersion).ToLowerInvariant();
        return target switch
        {
            "v1" => JobExtractionPrompts.V1,
            "v2" => JobExtractionPrompts.V2,
            _ => JobExtractionPrompts.V1
        };
    }
}
