using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Tracks the user's application to a specific job posting.
/// </summary>
public sealed class ApplicationRecord : Entity<Guid>, IAggregateRoot
{
    public Guid JobPostingId { get; private set; }
    public JobPosting? JobPosting { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime AppliedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ApplicationRecord() { }

    private ApplicationRecord(Guid id, Guid jobPostingId) : base(id)
    {
        JobPostingId = jobPostingId;
        Status = ApplicationStatus.Applied;
        AppliedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static ApplicationRecord Create(Guid jobPostingId) =>
        new(Guid.NewGuid(), jobPostingId);

    public void UpdateStatus(ApplicationStatus newStatus, string? notes = null)
    {
        Status = newStatus;
        Notes = notes ?? Notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddNotes(string notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}
