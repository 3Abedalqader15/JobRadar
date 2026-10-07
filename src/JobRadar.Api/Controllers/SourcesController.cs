using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<SourcesController> _logger;

    public SourcesController(AppDbContext db, ILogger<SourcesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,CompanyAdmin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var sources = await _db.Sources
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                Type = s.Type.ToString(),
                s.Url,
                Status = s.Status.ToString(),
                s.FetchIntervalMinutes,
                s.LastFetchedAt,
                s.ConsecutiveFailureCount,
                s.LastSyncIdentifier,
                s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(sources);
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Roles = "Admin,CompanyAdmin")]
    public async Task<IActionResult> ResumeSource(Guid id, CancellationToken cancellationToken)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source == null)
            return NotFound(new { message = $"Source {id} not found." });

        if (source.Type == SourceType.LinkedIn)
        {
            return BadRequest(new
            {
                message = "LinkedIn automated background fetch is disabled pending browser-extension capture flow. Source cannot be auto-resumed."
            });
        }

        source.Resume();
        // Reset failure count via reflection or entity method if needed
        typeof(Source).GetProperty(nameof(Source.ConsecutiveFailureCount))?
            .SetValue(source, 0);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Source {SourceName} ({SourceId}) was resumed by admin.", source.Name, source.Id);

        return Ok(new { message = $"Source '{source.Name}' has been resumed and is now Active." });
    }

    [HttpPost("resume-all")]
    [Authorize(Roles = "Admin,CompanyAdmin")]
    public async Task<IActionResult> ResumeAll(CancellationToken cancellationToken)
    {
        // Explicitly exclude SourceType.LinkedIn to prevent failure loops
        var pausedSources = await _db.Sources
            .Where(s => s.Status == SourceStatus.Paused && s.Type != SourceType.LinkedIn)
            .ToListAsync(cancellationToken);

        int count = 0;
        foreach (var source in pausedSources)
        {
            source.Resume();
            typeof(Source).GetProperty(nameof(Source.ConsecutiveFailureCount))?
                .SetValue(source, 0);
            count++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Bulk resumed {Count} eligible sources (excluding LinkedIn).", count);

        return Ok(new
        {
            message = $"Successfully resumed {count} paused sources. LinkedIn sources remained safely excluded.",
            resumedCount = count
        });
    }
}
