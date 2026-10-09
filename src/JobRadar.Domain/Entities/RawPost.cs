using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents the raw, unprocessed content fetched from a Source before LLM extraction.
/// </summary>
public sealed class RawPost : Entity<Guid>, IAggregateRoot
{
    public Guid SourceId { get; private set; }

    /// <summary>The full raw text of the post/message as fetched from the source.</summary>
    public string RawContent { get; private set; } = string.Empty;

    /// <summary>The direct URL to the original post, if available.</summary>
    public string? RawUrl { get; private set; }

    public DateTime FetchedAt { get; private set; }
    public RawPostStatus ProcessingStatus { get; private set; }

    /// <summary>SHA-256 hash of the cleaned text to detect content changes between crawls.</summary>
    public string? ContentHash { get; private set; }

    /// <summary>Optional reason code when a raw post is rejected (e.g. InsufficientLength, ErrorOrMaintenancePage).</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Version of the extraction prompt used to process this post.</summary>
    public string? ExtractionPromptVersion { get; private set; }

    // Navigations
    public Source? Source { get; private set; }
    public Job? Job { get; private set; }

    // EF Core constructor
    private RawPost() { }

    private RawPost(Guid id, Guid sourceId, string rawContent, string? rawUrl, string? contentHash = null) : base(id)
    {
        SourceId = sourceId;
        RawContent = rawContent;
        RawUrl = rawUrl;
        ContentHash = contentHash;
        FetchedAt = DateTime.UtcNow;
        ProcessingStatus = RawPostStatus.New;
    }

    public static RawPost Create(Guid sourceId, string rawContent, string? rawUrl = null, string? contentHash = null)
        => new(Guid.NewGuid(), sourceId, rawContent, rawUrl, contentHash);

    public void MarkProcessing() => ProcessingStatus = RawPostStatus.Processing;
    public void MarkProcessed() => ProcessingStatus = RawPostStatus.Processed;
    public void MarkRejected(string? reason = null)
    {
        ProcessingStatus = RawPostStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            RejectionReason = reason;
        }
    }
    public void ResetToNew() => ProcessingStatus = RawPostStatus.New;
    public void SetContentHash(string hash) => ContentHash = hash;
    public void SetExtractionPromptVersion(string version) => ExtractionPromptVersion = version;
}

