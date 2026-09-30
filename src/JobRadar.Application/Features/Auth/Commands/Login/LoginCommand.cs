using MediatR;
using FluentValidation;

namespace JobRadar.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string IpAddress
) : IRequest<LoginResult>;

public record LoginResult(
    bool Success,
    string AccessToken,
    string RefreshToken,
    string[] Errors
);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
