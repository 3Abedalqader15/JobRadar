using JobRadar.Application.Features.JobApplications.Commands.ManageJobApplicationQuestions;
using JobRadar.Application.Features.JobApplications.Queries.GetJobApplicationQuestions;
using JobRadar.Application.Common.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/jobs/{jobId:guid}/questions")]
[Produces("application/json")]
public sealed class JobApplicationQuestionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public JobApplicationQuestionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Gets the application questions for a specific job. Publicly accessible.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JobQuestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobApplicationQuestions(
        [FromRoute] Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetJobApplicationQuestionsQuery(jobId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Manages the application questions for a specific job. Authorized for Admin or HR.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ManageJobApplicationQuestions(
        [FromRoute] Guid jobId,
        [FromBody] ManageJobApplicationQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new ManageJobApplicationQuestionsCommand(jobId, request.Questions), cancellationToken);
            return NoContent();
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { Error = "Validation failed.", Details = ex.Errors });
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Error = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
    }
}

public class ManageJobApplicationQuestionsRequest
{
    public List<QuestionDto> Questions { get; set; } = new();
}
