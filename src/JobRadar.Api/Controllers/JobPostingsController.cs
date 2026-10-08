#nullable enable

using JobRadar.Api.Hubs;
using JobRadar.Application.Features.JobPostings.Commands.CreateJobPosting;
using JobRadar.Application.Features.JobPostings.Commands.DeleteJobPosting;
using JobRadar.Application.Features.JobPostings.Commands.UpdateJobPosting;
using JobRadar.Application.Features.JobPostings.Queries.GetJobPostingById;
using JobRadar.Application.Features.JobPostings.Queries.GetJobPostings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/job-postings")]
[Produces("application/json")]
public sealed class JobPostingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<JobHub> _hubContext;

    public JobPostingsController(IMediator mediator, IHubContext<JobHub> hubContext)
    {
        _mediator = mediator;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Returns a paginated list of job postings, optionally filtered by a search term.
    /// Accessible by everyone (public listing).
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
    /// Returns a single job posting by its ID. Publicly accessible.
    /// </summary>
    [HttpGet("{id:guid}", Name = "GetJobPostingById")]
    [ProducesResponseType(typeof(JobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetJobPostingByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new job posting. Requires Admin or HR role.
    /// Broadcasts the new job in real-time to active clients via SignalR.
    /// </summary>
    [HttpPost(Name = "CreateJobPosting")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobPostingCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = await _mediator.Send(command, cancellationToken);

        try
        {
            var matchingGroups = JobHub.ComputeMatchingGroups(
                command.IsRemote,
                command.Location,
                command.EmploymentType,
                command.ExperienceLevel);

            var notificationPayload = new
            {
                id = id,
                title = command.Title,
                companyName = command.CompanyName,
                location = command.Location,
                isRemote = command.IsRemote,
                employmentType = (int)command.EmploymentType,
                experienceLevel = (int)command.ExperienceLevel,
                externalApplyUrl = command.ExternalApplyUrl,
                postedAt = DateTime.UtcNow,
                isNew = true
            };

            await _hubContext.Clients.Groups(matchingGroups).SendAsync("ReceiveRelevantJob", notificationPayload, cancellationToken);
        }
        catch
        {
            // Non-blocking real-time notification
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            id);
    }

    /// <summary>
    /// Updates an existing job posting. Requires Admin or HR role.
    /// HR users may only update jobs belonging to their own company.
    /// </summary>
    [HttpPut("{id:guid}", Name = "UpdateJobPosting")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateJobPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateJobPostingCommand(
            id,
            request.Title,
            request.Location,
            request.IsRemote,
            request.ExternalApplyUrl,
            request.SalaryMin,
            request.SalaryMax,
            request.SalaryCurrency,
            request.EmploymentType,
            request.ExperienceLevel,
            request.Description,
            request.IsActive,
            request.CompanyId);

        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a job posting by its ID. Requires Admin or HR role.
    /// HR users may only delete jobs belonging to their own company.
    /// </summary>
    [HttpDelete("{id:guid}", Name = "DeleteJobPosting")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _mediator.Send(new DeleteJobPostingCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed class UpdateJobPostingRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsRemote { get; set; }
    public string? ExternalApplyUrl { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public JobRadar.Domain.Enums.EmploymentType EmploymentType { get; set; }
    public JobRadar.Domain.Enums.ExperienceLevel ExperienceLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? CompanyId { get; set; }
}
