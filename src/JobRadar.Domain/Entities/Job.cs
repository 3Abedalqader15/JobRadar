using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents a structured job posting after LLM extraction from a RawPost.
/// Contains the pgvector embedding for semantic similarity search.
/// </summary>
public sealed class Job : Entity<Guid>, IAggregateRoot
{
    /// <summary>The raw post this job was extracted from. Null for manually entered jobs.</summary>
    public Guid? RawPostId { get; private set; }

    public Guid SourceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string CompanyName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public bool IsRemote { get; private set; }
    public EmploymentType EmploymentType { get; private set; }
    public ExperienceLevel ExperienceLevel { get; private set; }

    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string? SalaryCurrency { get; private set; }

    public string? ExternalApplyUrl { get; private set; }
    public DateTime PostedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsActive { get; private set; }

    public int ViewsCount { get; private set; }
    public int ApplicantsClickCount { get; private set; }

    /// <summary>
    /// Semantic embedding vector (1536 dimensions by default, compatible with OpenAI
    /// text-embedding-3-small and similar models). Stored as a pgvector column.
    /// After migration, create an HNSW index manually:
    ///   CREATE INDEX ON jobs USING hnsw (embedding vector_cosine_ops);
    /// </summary>
    public float[]? Embedding { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigations
    public Source? Source { get; private set; }
    public RawPost? RawPost { get; private set; }

    public IReadOnlyCollection<JobSkillMap> JobSkills => _jobSkills.AsReadOnly();
    private readonly List<JobSkillMap> _jobSkills = new();

    public IReadOnlyCollection<UserSavedJob> SavedByUsers => _savedByUsers.AsReadOnly();
    private readonly List<UserSavedJob> _savedByUsers = new();

    public IReadOnlyCollection<UserJobApplication> Applications => _applications.AsReadOnly();
    private readonly List<UserJobApplication> _applications = new();

    public IReadOnlyCollection<CvJobMatchAnalysis> MatchAnalyses => _matchAnalyses.AsReadOnly();
    private readonly List<CvJobMatchAnalysis> _matchAnalyses = new();

    // EF Core constructor
    private Job() { }

    private Job(
        Guid id,
        Guid sourceId,
        string title,
        string companyName,
        string description,
        string? location,
        bool isRemote,
        EmploymentType employmentType,
        ExperienceLevel experienceLevel,
        string? externalApplyUrl,
        DateTime postedAt,
        Guid? rawPostId) : base(id)
    {
        SourceId = sourceId;
        Title = title;
        CompanyName = companyName;
        Description = description;
        Location = location;
        IsRemote = isRemote;
        EmploymentType = employmentType;
        ExperienceLevel = experienceLevel;
        ExternalApplyUrl = externalApplyUrl;
        PostedAt = postedAt;
        RawPostId = rawPostId;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Job Create(
        Guid sourceId,
        string title,
        string companyName,
        string description,
        string? location = null,
        bool isRemote = false,
        EmploymentType employmentType = EmploymentType.FullTime,
        ExperienceLevel experienceLevel = ExperienceLevel.MidLevel,
        string? externalApplyUrl = null,
        DateTime? postedAt = null,
        Guid? rawPostId = null)
        => new(
            Guid.NewGuid(),
            sourceId,
            title,
            companyName,
            description,
            location,
            isRemote,
            employmentType,
            experienceLevel,
            externalApplyUrl,
            postedAt ?? DateTime.UtcNow,
            rawPostId);

    public void SetEmbedding(float[] vector)
    {
        Embedding = vector;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSalaryRange(decimal min, decimal max, string currency = "USD")
    {
        SalaryMin = min;
        SalaryMax = max;
        SalaryCurrency = currency;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetExpiry(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementViews() => ViewsCount++;
    public void IncrementApplicantClicks() => ApplicantsClickCount++;
}
