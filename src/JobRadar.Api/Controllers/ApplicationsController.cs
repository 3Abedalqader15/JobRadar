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
[Authorize]
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
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? jobId = null,
        [FromQuery] string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetAllApplicationsQuery(page, pageSize, jobId, sortBy),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Downloads the CV for a specific application.
    /// Authorized for Admin, the HR of the company that owns the job, or the applicant themselves.
    /// </summary>
    [HttpGet("{id:guid}/cv", Name = "DownloadApplicationCv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DownloadCv([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new JobRadar.Application.Features.JobApplications.Queries.DownloadApplicationCv.DownloadApplicationCvQuery(id), cancellationToken);
        return File(result.FileStream, result.ContentType, result.FileName);
    }
}
