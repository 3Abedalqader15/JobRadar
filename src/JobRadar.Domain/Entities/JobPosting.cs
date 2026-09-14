using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents a job posting ingested from an external source.
/// </summary>
public sealed class JobPosting : Entity<Guid>, IAggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public string Company { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public bool IsRemote { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? SourceUrl { get; private set; }
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string? SalaryCurrency { get; private set; }
    public JobType JobType { get; private set; }
    public ExperienceLevel ExperienceLevel { get; private set; }

    /// <summary>
    /// Embedding vector for semantic similarity search (1536-dim by default).
    /// Stored as pgvector type in PostgreSQL.
    /// </summary>
    public float[]? EmbeddingVector { get; private set; }

    public DateTime PostedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Guid? JobSourceId { get; private set; }
    public JobSource? JobSource { get; private set; }

    // EF Core constructor
    private JobPosting() { }

    private JobPosting(
        Guid id,
        string title,
        string company,
        string description,
        string? location,
        bool isRemote,
        string? sourceUrl,
        JobType jobType,
        ExperienceLevel experienceLevel,
        DateTime postedAt,
        Guid? jobSourceId = null) : base(id)
    {
        Title = title;
        Company = company;
        Description = description;
        Location = location;
        IsRemote = isRemote;
        SourceUrl = sourceUrl;
        JobType = jobType;
        ExperienceLevel = experienceLevel;
        PostedAt = postedAt;
        JobSourceId = jobSourceId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static JobPosting Create(
        string title,
        string company,
        string description,
        string? location = null,
        bool isRemote = false,
        string? sourceUrl = null,
        JobType jobType = JobType.FullTime,
        ExperienceLevel experienceLevel = ExperienceLevel.MidLevel,
        DateTime? postedAt = null,
        Guid? jobSourceId = null)
    {
        return new JobPosting(
            Guid.NewGuid(),
            title,
            company,
            description,
            location,
            isRemote,
            sourceUrl,
            jobType,
            experienceLevel,
            postedAt ?? DateTime.UtcNow,
            jobSourceId);
    }

    public void UpdateSalaryRange(decimal min, decimal max, string currency = "USD")
    {
        SalaryMin = min;
        SalaryMax = max;
        SalaryCurrency = currency;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEmbeddingVector(float[] vector)
    {
        EmbeddingVector = vector;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string title, string company, string description, string? location, bool isRemote)
    {
        Title = title;
        Company = company;
        Description = description;
        Location = location;
        IsRemote = isRemote;
        UpdatedAt = DateTime.UtcNow;
    }
}
