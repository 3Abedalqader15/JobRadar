namespace JobRadar.Application.Abstractions;

/// <summary>
/// Asynchronous queue for background semantic vector embedding generation.
/// </summary>
public interface IJobEmbeddingQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
}
