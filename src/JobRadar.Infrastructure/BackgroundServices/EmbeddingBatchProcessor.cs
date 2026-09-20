using System.Threading.Channels;
using JobRadar.Application.Abstractions;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.BackgroundServices;

/// <summary>
/// A long-running <see cref="BackgroundService"/> that dequeues <see cref="Domain.Entities.Job"/>
/// IDs from an in-memory channel and generates their embedding vectors via
/// <see cref="IEmbeddingService"/>, updating the DB after each batch.
/// This keeps embedding generation out of the consumer's critical path.
/// </summary>
public sealed class EmbeddingBatchProcessor : BackgroundService
{
    // Static channel so the consumer can write to it without a direct service reference
    internal static readonly Channel<Guid> JobIdChannel =
        Channel.CreateBounded<Guid>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddingBatchProcessor> _logger;

    // How many job IDs to process in a single batch
    private const int BatchSize = 10;

    public EmbeddingBatchProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<EmbeddingBatchProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmbeddingBatchProcessor started.");

        var reader = JobIdChannel.Reader;
        var batch = new List<Guid>(BatchSize);

        await foreach (var jobId in reader.ReadAllAsync(stoppingToken))
        {
            batch.Add(jobId);

            // Drain additional items that are immediately available
            while (batch.Count < BatchSize && reader.TryRead(out var extra))
                batch.Add(extra);

            await ProcessBatchAsync(batch, stoppingToken);
            batch.Clear();
        }

        _logger.LogInformation("EmbeddingBatchProcessor stopped.");
    }

    private async Task ProcessBatchAsync(IReadOnlyList<Guid> jobIds, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

        foreach (var jobId in jobIds)
        {
            try
            {
                var job = await db.Jobs.FindAsync(new object[] { jobId }, ct);
                if (job is null)
                {
                    _logger.LogWarning("Job {JobId} not found for embedding generation.", jobId);
                    continue;
                }

                // Build the text to embed: combine key fields for rich semantic representation
                var embeddingText = $"Title: {job.Title}\nCompany: {job.CompanyName}\n{job.Description}";

                var vector = await embeddingService.GenerateAsync(embeddingText, ct);
                job.SetEmbedding(vector);

                await db.SaveChangesAsync(ct);
                _logger.LogDebug("Embedding generated for Job {JobId}.", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for Job {JobId}.", jobId);
                // Don't rethrow — continue with the rest of the batch
            }
        }
    }
}
