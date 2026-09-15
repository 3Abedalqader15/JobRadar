namespace JobRadar.Domain.Entities;

/// <summary>
/// Join entity representing a job bookmarked/saved by a user.
/// Uses a composite primary key (UserId, JobId).
/// </summary>
public sealed class UserSavedJob
{
    public Guid UserId { get; private set; }
    public Guid JobId { get; private set; }
    public DateTime SavedAt { get; private set; }

    // Navigations
    public User? User { get; private set; }
    public Job? Job { get; private set; }

    // EF Core constructor
    private UserSavedJob() { }

    public UserSavedJob(Guid userId, Guid jobId)
    {
        UserId = userId;
        JobId = jobId;
        SavedAt = DateTime.UtcNow;
    }
}
