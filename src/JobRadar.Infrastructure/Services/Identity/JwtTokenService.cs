using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

using Microsoft.AspNetCore.Identity;

namespace JobRadar.Infrastructure.Services.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public async Task<string> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var secret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is missing from configuration.");
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");
        
        var expirationMinutesString = _configuration["Jwt:ExpirationMinutes"] ?? "15";
        if (!int.TryParse(expirationMinutesString, out var expirationMinutes))
        {
            expirationMinutes = 15;
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName ?? user.Email!)
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (user.CompanyId.HasValue)
        {
            claims.Add(new Claim("CompanyId", user.CompanyId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds
        );

        return await Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public string HashRefreshToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret)) return null;

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"],
            ValidateLifetime = false // Here we are saying that we don't care about the token's expiration date
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;
            
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
