namespace JobRadar.Application.Abstractions;

/// <summary>
/// Asynchronous channel for background semantic vector embedding generation.
/// </summary>
public interface IJobEmbeddingChannel
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
}
