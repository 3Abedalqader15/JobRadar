using JobRadar.Application.Features.Companies.Commands.AssignHrToCompany;
using JobRadar.Application.Features.Companies.Commands.CreateCompany;
using JobRadar.Application.Features.Companies.Commands.UpdateCompany;
using JobRadar.Application.Features.Companies.Queries.ListCompanies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public sealed class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns a paginated list of companies. Admin-only.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListCompaniesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListCompaniesQuery(page, pageSize, search), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new company in the directory. Admin-only.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCompanyCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }

    /// <summary>
    /// Updates company details. Admin-only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateCompanyCommand(id, request.Name, request.Slug, request.LogoUrl);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Assigns an HR user to a company and grants the HR role. Admin-only.
    /// </summary>
    [HttpPost("assign-hr")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignHr(
        [FromBody] AssignHrToCompanyCommand command,
        CancellationToken cancellationToken = default)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}

public sealed class UpdateCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}
