using MediatR;

namespace JobRadar.Application.Features.Companies.Commands.UpdateCompany;

public sealed record UpdateCompanyCommand(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl = null) : IRequest;
