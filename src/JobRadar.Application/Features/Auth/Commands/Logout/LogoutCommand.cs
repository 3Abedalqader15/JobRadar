using MediatR;
using FluentValidation;

namespace JobRadar.Application.Features.Auth.Commands.Logout;

public record LogoutCommand(
    string RefreshToken,
    string IpAddress
) : IRequest<LogoutResult>;

public record LogoutResult(
    bool Success,
    string[] Errors
);

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
