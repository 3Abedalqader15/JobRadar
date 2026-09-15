namespace JobRadar.Application.Messages;

public record RawPostCreatedEvent(
    Guid RawPostId,
    Guid SourceId,
    string? RawUrl,
    DateTime FetchedAt
);
