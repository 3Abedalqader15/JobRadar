using JobRadar.Application.Abstractions;
using JobRadar.Application.Messages;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.Sources.Commands.IngestSource;

public sealed class IngestSourceCommandHandler : IRequestHandler<IngestSourceCommand>
{
    private readonly ISourceRepository _sourceRepository;
    private readonly IRawPostRepository _rawPostRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<ISourceFetcher> _fetchers;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<IngestSourceCommandHandler> _logger;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public IngestSourceCommandHandler(
        ISourceRepository sourceRepository,
        IRawPostRepository rawPostRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<ISourceFetcher> fetchers,
        IPublishEndpoint publishEndpoint,
        ILogger<IngestSourceCommandHandler> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _sourceRepository = sourceRepository;
        _rawPostRepository = rawPostRepository;
        _unitOfWork = unitOfWork;
        _fetchers = fetchers;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Handle(IngestSourceCommand request, CancellationToken cancellationToken)
    {
        var source = await _sourceRepository.GetByIdAsync(request.SourceId, cancellationToken);
        if (source == null)
            throw new Exception("Source not found.");

        if (source.Status != SourceStatus.Active)
            throw new Exception($"Source is in status {source.Status}");

        var fetcher = _fetchers.FirstOrDefault(f => f.CanHandle(source.Type));
        if (fetcher == null)
        {
            _logger.LogError("No fetcher registered for source type {Type}", source.Type);
            throw new Exception($"No fetcher for {source.Type}");
        }

        try
        {
            var posts = await fetcher.FetchPostsAsync(source, cancellationToken);
            var newPostsCount = 0;
            string? latestSyncId = source.LastSyncIdentifier;

            foreach (var post in posts)
            {
                var exists = await _rawPostRepository.ExistsAsync(source.Id, post.RawUrl, cancellationToken);
                if (!exists)
                {
                    var rawPost = RawPost.Create(source.Id, post.RawContent, post.RawUrl);
                    // We can't set FetchedAt explicitly since RawPost.Create uses DateTime.UtcNow.
                    await _rawPostRepository.AddAsync(rawPost, cancellationToken);
                    
                    var ev = new RawPostCreatedEvent(rawPost.Id, source.Id, rawPost.RawUrl, rawPost.FetchedAt);
                    await _publishEndpoint.Publish(ev, cancellationToken);
                    
                    newPostsCount++;
                }

                // Update sync id to the last post we process, assuming posts are ordered correctly by fetcher
                if (!string.IsNullOrEmpty(post.SyncIdentifier))
                {
                    latestSyncId = post.SyncIdentifier;
                }
            }

            source.RecordFetch(latestSyncId);
            await _sourceRepository.UpdateAsync(source, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Ingested {Count} new posts from source {SourceId}", newPostsCount, source.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest source {SourceId}", source.Id);
            source.RecordFailure();

            // Threshold could be config driven. For now, hardcode to 5 as requested, or read from config if passed.
            var failureThreshold = int.TryParse(_configuration["Ingestion:FailureThreshold"], out var t) ? t : 5;
            if (source.ConsecutiveFailureCount >= failureThreshold)
            {
                _logger.LogWarning("Source {SourceId} reached failure threshold. Pausing.", source.Id);
                source.Pause();
            }

            await _sourceRepository.UpdateAsync(source, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw;
        }
    }
}
