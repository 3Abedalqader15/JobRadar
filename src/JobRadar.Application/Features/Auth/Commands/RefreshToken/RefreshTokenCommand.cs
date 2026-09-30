using MediatR;
using FluentValidation;

namespace JobRadar.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(
    string ExpiredAccessToken,
    string RefreshToken,
    string IpAddress
) : IRequest<RefreshTokenResult>;

public record RefreshTokenResult(
    bool Success,
    string AccessToken,
    string RefreshToken,
    string[] Errors
);

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.ExpiredAccessToken).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
