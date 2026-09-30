using JobRadar.Domain.Entities;
using System.Security.Claims;

namespace JobRadar.Application.Abstractions;

public interface IJwtTokenService
{
    Task<string> GenerateAccessTokenAsync(ApplicationUser user);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
