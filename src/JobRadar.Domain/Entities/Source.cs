using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents an ingestion source (Telegram channel, RSS feed, company careers page, or manual share)
/// that the system monitors for new job postings.
/// </summary>
public sealed class Source : Entity<Guid>, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public SourceType Type { get; private set; }

    /// <summary>The URL or identifier for the source (e.g. channel username, RSS URL, careers page URL).</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>The user who submitted this source. Null if added by an admin.</summary>
    public Guid? AddedByUserId { get; private set; }

    public SourceStatus Status { get; private set; }

    /// <summary>How often (in minutes) the ingestion worker should fetch this source.</summary>
    public int FetchIntervalMinutes { get; private set; }

    public DateTime? LastFetchedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigations
    public User? AddedByUser { get; private set; }

    public IReadOnlyCollection<RawPost> RawPosts => _rawPosts.AsReadOnly();
    private readonly List<RawPost> _rawPosts = new();

    public IReadOnlyCollection<Job> Jobs => _jobs.AsReadOnly();
    private readonly List<Job> _jobs = new();

    // EF Core constructor
    private Source() { }

    private Source(
        Guid id,
        string name,
        SourceType type,
        string url,
        Guid? addedByUserId,
        int fetchIntervalMinutes) : base(id)
    {
        Name = name;
        Type = type;
        Url = url;
        AddedByUserId = addedByUserId;
        FetchIntervalMinutes = fetchIntervalMinutes;
        Status = addedByUserId.HasValue ? SourceStatus.Pending : SourceStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public static Source Create(
        string name,
        SourceType type,
        string url,
        Guid? addedByUserId = null,
        int fetchIntervalMinutes = 60)
        => new(Guid.NewGuid(), name, type, url, addedByUserId, fetchIntervalMinutes);

    public void Approve() => Status = SourceStatus.Active;
    public void Reject() => Status = SourceStatus.Rejected;
    public void Pause() => Status = SourceStatus.Paused;
    public void Resume() => Status = SourceStatus.Active;

    public void RecordFetch() => LastFetchedAt = DateTime.UtcNow;

    public void UpdateFetchInterval(int minutes)
    {
        if (minutes < 1) throw new ArgumentOutOfRangeException(nameof(minutes), "Interval must be at least 1 minute.");
        FetchIntervalMinutes = minutes;
    }
}
