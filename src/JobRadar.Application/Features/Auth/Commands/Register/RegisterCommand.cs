using MediatR;

namespace JobRadar.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string FullName,
    string Email,
    string Password
) : IRequest<RegisterResult>;

public record RegisterResult(
    bool Success,
    string[] Errors
);
