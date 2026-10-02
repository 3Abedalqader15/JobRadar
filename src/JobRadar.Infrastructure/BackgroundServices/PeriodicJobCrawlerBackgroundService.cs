using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.BackgroundServices;

public sealed class PeriodicJobCrawlerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PeriodicJobCrawlerBackgroundService> _logger;

    public PeriodicJobCrawlerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PeriodicJobCrawlerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PeriodicJobCrawlerBackgroundService is starting...");

        // Initial delay to let DB migration / seeder complete
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var intervalMinutes = _configuration.GetValue<int>("JobCrawler:IntervalMinutes", 3);
        if (intervalMinutes < 1) intervalMinutes = 3;

        _logger.LogInformation("Periodic Job Crawler active. Schedule: every {Minutes} minutes.", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Periodic Job Crawler running cycle at {Time}", DateTimeOffset.UtcNow);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var ingestionService = scope.ServiceProvider.GetRequiredService<IAutomatedJobIngestionService>();
                    var count = await ingestionService.RunCrawlCycleAsync(stoppingToken);
                    _logger.LogInformation("Periodic crawl cycle completed. Ingested {Count} new job postings.", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PeriodicJobCrawlerBackgroundService cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("PeriodicJobCrawlerBackgroundService has stopped.");
    }
}
