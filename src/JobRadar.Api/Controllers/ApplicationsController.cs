using JobRadar.Application.Features.JobApplications.Queries.GetAllApplications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.Api.Controllers;

/// <summary>
/// Admin/HR endpoint for reviewing job applications.
/// </summary>
[ApiController]
[Route("api/applications")]
[Produces("application/json")]
[Authorize(Roles = "Admin,HR")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns a paginated list of all job applications.
    /// Optionally filtered by jobId.
    /// </summary>
    [HttpGet(Name = "GetAllApplications")]
    [ProducesResponseType(typeof(GetAllApplicationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetAllApplicationsQuery(page, pageSize, jobId),
            cancellationToken);
        return Ok(result);
    }
}
