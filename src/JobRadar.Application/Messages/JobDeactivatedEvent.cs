namespace JobRadar.Application.Messages;

/// <summary>
/// Published when a job posting has been deactivated, expired, or deleted.
/// </summary>
public record JobDeactivatedEvent(
    Guid JobId,
    DateTime DeactivatedAt
);
