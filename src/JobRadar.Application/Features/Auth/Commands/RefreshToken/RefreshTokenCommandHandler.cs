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

    private static readonly string[] InvalidAccessTokenError = ["Invalid access token."];
    private static readonly string[] InvalidTokenClaimsError = ["Invalid token claims."];
    private static readonly string[] UserNotFoundOrDeactivatedError = ["User not found or deactivated."];
    private static readonly string[] InvalidRefreshTokenError = ["Invalid refresh token."];
    private static readonly string[] InvalidOrExpiredRefreshTokenError = ["Invalid or expired refresh token."];

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hashedToken = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(hashedToken, cancellationToken);

        if (storedToken == null)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, InvalidRefreshTokenError);
        }

        var userId = storedToken.UserId;

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
            
            return new RefreshTokenResult(false, string.Empty, string.Empty, InvalidRefreshTokenError);
        }

        if (!storedToken.IsActive)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, InvalidOrExpiredRefreshTokenError);
        }

        // If an expired access token was provided, optionally verify it matches the stored token user
        if (!string.IsNullOrWhiteSpace(request.ExpiredAccessToken))
        {
            var principal = _jwtTokenService.GetPrincipalFromExpiredToken(request.ExpiredAccessToken);
            if (principal != null)
            {
                var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdString, out var tokenUserId) && tokenUserId != userId)
                {
                    return new RefreshTokenResult(false, string.Empty, string.Empty, InvalidTokenClaimsError);
                }
            }
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeactivated)
        {
            return new RefreshTokenResult(false, string.Empty, string.Empty, UserNotFoundOrDeactivatedError);
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
        var roles = await _userManager.GetRolesAsync(user);

        return new RefreshTokenResult(
            true,
            newAccessToken,
            newPlainRefreshToken,
            Array.Empty<string>(),
            user.Id.ToString(),
            user.Email,
            user.FullName,
            roles.ToArray());
    }
}
