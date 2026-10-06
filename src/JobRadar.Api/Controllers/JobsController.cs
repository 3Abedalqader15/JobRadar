using System.Security.Claims;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Features.JobSearch.Queries;
using JobRadar.Application.Models;
using JobRadar.Domain.Entities;
using JobRadar.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _dbContext;

    public JobsController(IMediator mediator, AppDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpPost("sync-now")]
    public async Task<IActionResult> SyncNow(CancellationToken cancellationToken)
    {
        var count = await _mediator.Send(new JobRadar.Application.Features.Crawlers.Commands.IngestCrawledJobs.IngestCrawledJobsCommand(), cancellationToken);
        return Ok(new
        {
            success = true,
            newJobsCreated = count,
            message = $"Crawler completed successfully. {count} new jobs were ingested."
        });
    }

    [HttpPost("search")]
    [EnableRateLimiting("SearchJobsPolicy")]
    public async Task<ActionResult<PagedResult<JobSearchResultDto>>> Search(
        [FromBody] SearchJobsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/apply")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [EnableRateLimiting("ApplicationSubmissionPolicy")]
    public async Task<IActionResult> Apply(
        Guid id,
        [FromForm] JobApplicationSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        // Extract the user ID from the JWT claims
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { Error = "Invalid user identity." });
        }

        if (request.CvFile == null || request.CvFile.Length == 0)
        {
            return BadRequest(new { Error = "CV file is required." });
        }

        using var cvStream = request.CvFile.OpenReadStream();
        
        var answers = request.Answers != null 
            ? System.Text.Json.JsonSerializer.Deserialize<List<JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication.AnswerSubmissionDto>>(request.Answers) 
              ?? new List<JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication.AnswerSubmissionDto>()
            : new List<JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication.AnswerSubmissionDto>();

        var command = new JobRadar.Application.Features.JobApplications.Commands.SubmitJobApplication.SubmitJobApplicationCommand(
            id,
            userId,
            request.ApplicantFullName ?? string.Empty,
            request.ApplicantEmail ?? string.Empty,
            request.ApplicantPhone ?? string.Empty,
            cvStream,
            request.CvFile.FileName,
            request.CvFile.ContentType,
            request.CvFile.Length,
            answers
        );

        try
        {
            var applicationId = await _mediator.Send(command, cancellationToken);
            return Ok(new { ApplicationId = applicationId });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { Error = "Validation failed.", Details = ex.Errors });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("saved")]
    [Authorize]
    public async Task<IActionResult> GetSavedJobs(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var jobIds = await _dbContext.UserSavedJobs
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => s.JobId)
            .ToListAsync(cancellationToken);

        return Ok(jobIds);
    }

    [HttpPost("saved/batch")]
    [Authorize]
    public async Task<IActionResult> BatchSyncSavedJobs([FromBody] BatchSaveJobsRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var incomingIds = request?.JobIds?.Distinct().ToList() ?? new List<Guid>();

        var existingIds = await _dbContext.UserSavedJobs
            .Where(s => s.UserId == userId)
            .Select(s => s.JobId)
            .ToListAsync(cancellationToken);

        var toAdd = incomingIds.Except(existingIds).ToList();
        if (toAdd.Count > 0)
        {
            var validJobIds = await _dbContext.Jobs
                .Where(j => toAdd.Contains(j.Id))
                .Select(j => j.Id)
                .ToListAsync(cancellationToken);

            foreach (var jobId in validJobIds)
            {
                _dbContext.UserSavedJobs.Add(new UserSavedJob(userId, jobId));
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            existingIds.AddRange(validJobIds);
        }

        return Ok(existingIds);
    }

    [HttpPost("{id:guid}/save")]
    [Authorize]
    public async Task<IActionResult> SaveJob(Guid id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var exists = await _dbContext.UserSavedJobs.AnyAsync(s => s.UserId == userId && s.JobId == id, cancellationToken);
        if (!exists)
        {
            var jobExists = await _dbContext.Jobs.AnyAsync(j => j.Id == id, cancellationToken);
            if (!jobExists) return NotFound(new { Error = "Job not found." });

            _dbContext.UserSavedJobs.Add(new UserSavedJob(userId, id));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, jobId = id });
    }

    [HttpDelete("{id:guid}/save")]
    [Authorize]
    public async Task<IActionResult> UnsaveJob(Guid id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var entry = await _dbContext.UserSavedJobs.FirstOrDefaultAsync(s => s.UserId == userId && s.JobId == id, cancellationToken);
        if (entry != null)
        {
            _dbContext.UserSavedJobs.Remove(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, jobId = id });
    }
}

public class JobApplicationSubmissionRequest
{
    public string? ApplicantFullName { get; set; }
    public string? ApplicantEmail { get; set; }
    public string? ApplicantPhone { get; set; }
    public IFormFile CvFile { get; set; } = null!;
    
    /// <summary>
    /// JSON serialized array of answers, e.g. [{"QuestionId":"...", "AnswerText":"..."}]
    /// </summary>
    public string? Answers { get; set; }
}

public class BatchSaveJobsRequest
{
    public List<Guid>? JobIds { get; set; }
}

