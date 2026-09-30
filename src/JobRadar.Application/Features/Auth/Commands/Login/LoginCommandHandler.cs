using MediatR;
using Microsoft.AspNetCore.Identity;
using JobRadar.Domain.Entities;
using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<LoginCommandHandler> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return new LoginResult(false, string.Empty, string.Empty, new[] { "Invalid credentials." });
        }

        if (user.IsDeactivated)
        {
            return new LoginResult(false, string.Empty, string.Empty, new[] { "Account is deactivated." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return new LoginResult(false, string.Empty, string.Empty, new[] { "Account is locked out." });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isPasswordValid)
        {
            await _userManager.AccessFailedAsync(user);
            return new LoginResult(false, string.Empty, string.Empty, new[] { "Invalid credentials." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);
        var plainRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var hashedToken = _jwtTokenService.HashRefreshToken(plainRefreshToken);

        var refreshToken = JobRadar.Domain.Entities.RefreshToken.Create(
            userId: user.Id,
            tokenHash: hashedToken,
            expiresAt: DateTime.UtcNow.AddDays(7), // Should be configurable
            createdByIp: request.IpAddress
        );

        _refreshTokenRepository.Add(refreshToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResult(true, accessToken, plainRefreshToken, Array.Empty<string>());
    }
}
