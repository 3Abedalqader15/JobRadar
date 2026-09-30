using JobRadar.Application.Features.JobSearch.Queries;
using JobRadar.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IMediator _mediator;

    public JobsController(IMediator mediator)
    {
        _mediator = mediator;
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
    public async Task<IActionResult> Apply(
        Guid id,
        [FromBody] ApplyRequest request,
        CancellationToken cancellationToken)
    {
        // Ideally the user ID should come from authentication claims.
        // Since auth isn't fully set up for this request, we fallback to a generated ID
        // or one passed from the frontend for testing.
        var userId = request.UserId ?? Guid.NewGuid();

        var command = new JobRadar.Application.Features.JobApplications.Commands.ApplyForJob.ApplyForJobCommand(
            id,
            userId,
            request.Notes
        );

        try
        {
            var applicationId = await _mediator.Send(command, cancellationToken);
            return Ok(new { ApplicationId = applicationId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}

public class ApplyRequest
{
    public Guid? UserId { get; set; }
    public string? Notes { get; set; }
}
