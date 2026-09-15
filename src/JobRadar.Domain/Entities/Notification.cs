using JobRadar.Domain.Common;
using JobRadar.Domain.Enums;

namespace JobRadar.Domain.Entities;

/// <summary>
/// Represents a notification sent (or pending) to a user about a matching job or system event.
/// </summary>
public sealed class Notification : Entity<Guid>
{
    public Guid UserId { get; private set; }

    /// <summary>The job this notification is about. Null for non-job system notifications.</summary>
    public Guid? JobId { get; private set; }

    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateTime? SentAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigations
    public User? User { get; private set; }
    public Job? Job { get; private set; }

    // EF Core constructor
    private Notification() { }

    private Notification(
        Guid id,
        Guid userId,
        NotificationChannel channel,
        string message,
        Guid? jobId) : base(id)
    {
        UserId = userId;
        Channel = channel;
        Message = message;
        JobId = jobId;
        Status = NotificationStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static Notification Create(
        Guid userId,
        NotificationChannel channel,
        string message,
        Guid? jobId = null)
        => new(Guid.NewGuid(), userId, channel, message, jobId);

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkFailed() => Status = NotificationStatus.Failed;
}
