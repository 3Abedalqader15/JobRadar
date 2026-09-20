namespace JobRadar.Application.Messages;

/// <summary>
/// Published after a <see cref="Domain.Entities.Job"/> has been successfully
/// created and persisted from a processed RawPost.
/// </summary>
public record JobCreatedEvent(
    Guid JobId,
    Guid SourceId,
    Guid? RawPostId,
    string Title,
    string CompanyName,
    string? Location,
    bool IsRemote
);
