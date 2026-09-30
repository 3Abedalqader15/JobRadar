using MediatR;
using JobRadar.Application.Abstractions;

namespace JobRadar.Application.Features.Auth.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, LogoutResult>
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        IJwtTokenService jwtTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _jwtTokenService = jwtTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<LogoutResult> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hashedToken = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(hashedToken, cancellationToken);

        if (storedToken == null || !storedToken.IsActive)
        {
            // Already invalid, just return success
            return new LogoutResult(true, Array.Empty<string>());
        }

        storedToken.Revoke(request.IpAddress, "User logged out", null);

        _refreshTokenRepository.Update(storedToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LogoutResult(true, Array.Empty<string>());
    }
}
