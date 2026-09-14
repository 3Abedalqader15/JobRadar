using JobRadar.Domain.Common;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents an external job board or data source that is periodically ingested.
/// </summary>
public sealed class JobSource : Entity<Guid>, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTime? LastFetchedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public IReadOnlyCollection<JobPosting> JobPostings => _jobPostings.AsReadOnly();
    private readonly List<JobPosting> _jobPostings = new();

    private JobSource() { }

    private JobSource(Guid id, string name, string baseUrl) : base(id)
    {
        Name = name;
        BaseUrl = baseUrl;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static JobSource Create(string name, string baseUrl) =>
        new(Guid.NewGuid(), name, baseUrl);

    public void RecordFetch() => LastFetchedAt = DateTime.UtcNow;

    public void Disable() => IsEnabled = false;

    public void Enable() => IsEnabled = true;
}
