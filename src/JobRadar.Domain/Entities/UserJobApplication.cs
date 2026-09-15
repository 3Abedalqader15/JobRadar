using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Tracks a user's application to a specific job, including pipeline status progression.
/// </summary>
public sealed class UserJobApplication : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid JobId { get; private set; }
    public DateTime AppliedAt { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigations
    public User? User { get; private set; }
    public Job? Job { get; private set; }

    // EF Core constructor
    private UserJobApplication() { }

    private UserJobApplication(Guid id, Guid userId, Guid jobId) : base(id)
    {
        UserId = userId;
        JobId = jobId;
        Status = ApplicationStatus.Applied;
        AppliedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static UserJobApplication Create(Guid userId, Guid jobId)
        => new(Guid.NewGuid(), userId, jobId);

    public void UpdateStatus(ApplicationStatus newStatus, string? notes = null)
    {
        Status = newStatus;
        if (notes is not null) Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}
