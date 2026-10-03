using JobRadar.Application.Abstractions;

namespace JobRadar.Infrastructure.BackgroundServices;

/// <summary>
/// Enqueues job IDs to the background embedding batch processor channel.
/// </summary>
public sealed class JobEmbeddingQueue : IJobEmbeddingQueue
{
    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await EmbeddingBatchProcessor.JobIdChannel.Writer.WriteAsync(jobId, cancellationToken);
    }
}
