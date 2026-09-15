using JobRadar.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Jobs;

/// <summary>
/// Hangfire job that removes job postings older than a configurable number of days.
/// Runs daily at midnight UTC by default.
/// </summary>
public sealed class StaleJobCleanupJob
{
    private readonly IJobRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaleJobCleanupJob> _logger;

    // Postings older than this many days will be deleted
    private const int StaleAfterDays = 90;

    public StaleJobCleanupJob(
        IJobRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<StaleJobCleanupJob> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-StaleAfterDays);

        _logger.LogInformation(
            "Running stale job cleanup. Removing postings with PostedAt < {Cutoff}",
            cutoff);

        try
        {
            var all = await _repository.GetAllAsync(cancellationToken);
            var stale = all.Where(j => j.PostedAt < cutoff).ToList();

            if (stale.Count == 0)
            {
                _logger.LogInformation("No stale job postings found.");
                return;
            }

            foreach (var posting in stale)
            {
                await _repository.DeleteAsync(posting, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} stale job postings.", stale.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stale job cleanup failed.");
            throw;
        }
    }
}
