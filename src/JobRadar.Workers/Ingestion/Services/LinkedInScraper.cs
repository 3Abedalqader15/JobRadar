using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobRadar.Workers.Ingestion.Services;

public class LinkedInScraper : IDisposable
{
    private readonly ILogger<LinkedInScraper> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public LinkedInScraper(ILogger<LinkedInScraper> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_playwright == null)
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        }
    }

    public async Task<List<ScrapedJob>> FetchJobsAsync(string keyword, string location, int limit = 10)
    {
        await InitializeAsync();
        
        var jobs = new List<ScrapedJob>();
        var page = await _browser!.NewPageAsync();
        
        try
        {
            // Go to LinkedIn Jobs (no login required for this specific URL structure)
            var url = $"https://www.linkedin.com/jobs/search?keywords={Uri.EscapeDataString(keyword)}&location={Uri.EscapeDataString(location)}";
            _logger.LogInformation("Scraping LinkedIn Jobs: {Url}", url);
            
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Scroll down a bit to load lazy-loaded elements
            await page.EvaluateAsync("window.scrollBy(0, 1000)");
            await Task.Delay(2000); // Give it time to load

            var jobCards = await page.QuerySelectorAllAsync(".job-search-card");
            
            int count = 0;
            foreach (var card in jobCards)
            {
                if (count >= limit) break;

                var titleEl = await card.QuerySelectorAsync(".base-search-card__title");
                var companyEl = await card.QuerySelectorAsync(".base-search-card__subtitle");
                var locationEl = await card.QuerySelectorAsync(".job-search-card__location");
                var linkEl = await card.QuerySelectorAsync(".base-card__full-link");

                var title = titleEl != null ? (await titleEl.InnerTextAsync()).Trim() : string.Empty;
                var company = companyEl != null ? (await companyEl.InnerTextAsync()).Trim() : string.Empty;
                var loc = locationEl != null ? (await locationEl.InnerTextAsync()).Trim() : string.Empty;
                var link = linkEl != null ? await linkEl.GetAttributeAsync("href") : string.Empty;

                if (!string.IsNullOrEmpty(title))
                {
                    jobs.Add(new ScrapedJob(title, company, loc, link ?? string.Empty));
                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping LinkedIn");
        }
        finally
        {
            await page.CloseAsync();
        }

        return jobs;
    }

    public void Dispose()
    {
        _browser?.DisposeAsync().GetAwaiter().GetResult();
        _playwright?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record ScrapedJob(string Title, string Company, string Location, string Url);
