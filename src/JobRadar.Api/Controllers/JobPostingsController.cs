using JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;
using JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;
using JobRadar.Application.Features.JobPostings.Queries.GetJobPostingById;
using JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/job-postings")]
[Produces("application/json")]
public sealed class JobPostingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public JobPostingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns a paginated list of job postings, optionally filtered by a search term.
    /// </summary>
    [HttpGet(Name = "GetJobPostings")]
    [ProducesResponseType(typeof(GetJobPostingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetJobPostingsQuery(page, pageSize, search),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single job posting by its ID.
    /// </summary>
    [HttpGet("{id:guid}", Name = "GetJobPostingById")]
    [ProducesResponseType(typeof(JobPostingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetJobPostingByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new job posting. Returns the ID of the created resource.
    /// </summary>
    [HttpPost(Name = "CreateJobPosting")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobPostingCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id },
            id);
    }

    /// <summary>
    /// Deletes a job posting by its ID.
    /// </summary>
    [HttpDelete("{id:guid}", Name = "DeleteJobPosting")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new DeleteJobPostingCommand(id), cancellationToken);
        return NoContent();
    }
}
