using JobRadar.Application.Abstractions;

namespace JobRadar.Infrastructure.BackgroundServices;

public sealed class JobEmbeddingChannel : IJobEmbeddingChannel
{
    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return EmbeddingBatchProcessor.JobIdChannel.Writer.WriteAsync(jobId, cancellationToken);
    }
}
