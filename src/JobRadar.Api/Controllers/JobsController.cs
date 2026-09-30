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
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Apply(
        Guid id,
        [FromBody] ApplyRequest request,
        CancellationToken cancellationToken)
    {
        // Extract the user ID from the JWT claims
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { Error = "Invalid user identity." });
        }

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
