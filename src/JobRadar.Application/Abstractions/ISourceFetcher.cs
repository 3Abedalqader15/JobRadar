using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;

namespace JobRadar.Application.Abstractions;

public record FetchedPostInfo(string RawUrl, string RawContent, string? SyncIdentifier, DateTime FetchedAt);

public interface ISourceFetcher
{
    bool CanHandle(SourceType type);
    Task<IReadOnlyList<FetchedPostInfo>> FetchPostsAsync(Source source, CancellationToken cancellationToken);
}
