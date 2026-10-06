using MediatR;

namespace JobRadar.Application.Features.Companies.Commands.AssignHrToCompany;

public sealed record AssignHrToCompanyCommand(
    Guid UserId,
    Guid CompanyId) : IRequest;
