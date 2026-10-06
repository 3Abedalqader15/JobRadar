using MediatR;

namespace JobRadar.Application.Features.Companies.Commands.CreateCompany;

public sealed record CreateCompanyCommand(
    string Name,
    string Slug,
    string? LogoUrl = null) : IRequest<Guid>;
