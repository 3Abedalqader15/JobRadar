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
}
