using MediatR;
using Microsoft.AspNetCore.Identity;
using JobRadar.Domain.Entities;
using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace JobRadar.Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.ExpiredAccessToken);
        if (principal == null)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, new[] { "Invalid access token." });
        }

        var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, new[] { "Invalid token claims." });
        }

        var user = await _userManager.FindByIdAsync(userIdString);
        if (user == null || user.IsDeactivated)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, new[] { "User not found or deactivated." });
        }

        var hashedToken = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(hashedToken, cancellationToken);

        if (storedToken == null || storedToken.UserId != userId)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, new[] { "Invalid refresh token." });
        }

        // Reuse detection
        if (storedToken.IsRevoked)
        {
            _logger.LogWarning("Attempted reuse of revoked refresh token for user {UserId}. Revoking all tokens.", userId);
            var activeTokens = await _refreshTokenRepository.GetAllActiveTokensForUserAsync(userId, cancellationToken);
            foreach (var token in activeTokens)
            {
                token.Revoke(request.IpAddress, "Attempted reuse of revoked ancestor token", null);
                _refreshTokenRepository.Update(token);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            return new RefreshTokenResult(false, string.Empty, string.Empty, new[] { "Invalid refresh token." });
        }

        if (!storedToken.IsActive)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, new[] { "Invalid or expired refresh token." });
        }

        // Revoke current token
        var newPlainRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newHashedToken = _jwtTokenService.HashRefreshToken(newPlainRefreshToken);

        storedToken.Revoke(request.IpAddress, "Replaced by new token", newHashedToken);
        _refreshTokenRepository.Update(storedToken);

        // Generate new tokens
        var newRefreshTokenEntity = JobRadar.Domain.Entities.RefreshToken.Create(
            userId: user.Id,
            tokenHash: newHashedToken,
            expiresAt: DateTime.UtcNow.AddDays(7),
            createdByIp: request.IpAddress
        );
        
        _refreshTokenRepository.Add(newRefreshTokenEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var newAccessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);

        return new RefreshTokenResult(true, newAccessToken, newPlainRefreshToken, Array.Empty<string>());
    }
}
