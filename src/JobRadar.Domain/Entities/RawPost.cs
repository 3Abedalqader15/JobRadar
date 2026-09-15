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

    // Navigations
    public Source? Source { get; private set; }
    public Job? Job { get; private set; }

    // EF Core constructor
    private RawPost() { }

    private RawPost(Guid id, Guid sourceId, string rawContent, string? rawUrl) : base(id)
    {
        SourceId = sourceId;
        RawContent = rawContent;
        RawUrl = rawUrl;
        FetchedAt = DateTime.UtcNow;
        ProcessingStatus = RawPostStatus.New;
    }

    public static RawPost Create(Guid sourceId, string rawContent, string? rawUrl = null)
        => new(Guid.NewGuid(), sourceId, rawContent, rawUrl);

    public void MarkProcessing() => ProcessingStatus = RawPostStatus.Processing;
    public void MarkProcessed() => ProcessingStatus = RawPostStatus.Processed;
    public void MarkRejected() => ProcessingStatus = RawPostStatus.Rejected;
}
