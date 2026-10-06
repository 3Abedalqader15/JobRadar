using JobRadar.Application.Abstractions;
using JobRadar.Application.Features.Auth.Commands.Login;
using JobRadar.Application.Features.Auth.Commands.Logout;
using JobRadar.Application.Features.Auth.Commands.RefreshToken;
using JobRadar.Application.Features.Auth.Commands.Register;
using JobRadar.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICompanyRepository _companyRepository;

    public AuthController(
        IMediator mediator,
        UserManager<ApplicationUser> userManager,
        ICompanyRepository companyRepository)
    {
        _mediator = mediator;
        _userManager = userManager;
        _companyRepository = companyRepository;
    }

    [HttpPost("register")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
        {
            return BadRequest(new { result.Errors });
        }

        return Ok(new { Message = "Registration successful" });
    }

    [HttpPost("login")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password, GetIpAddress());
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return Unauthorized(new { result.Errors });
        }

        SetTokenCookie(result.RefreshToken);

        // Fetch user info to return roles, full name, and company details
        var user = await _userManager.FindByEmailAsync(request.Email);
        var roles = user != null ? await _userManager.GetRolesAsync(user) : Array.Empty<string>();
        string? companyName = null;
        if (user?.CompanyId != null)
        {
            var company = await _companyRepository.GetByIdAsync(user.CompanyId.Value);
            companyName = company?.Name;
        }

        return Ok(new
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            UserId = user?.Id.ToString(),
            Email = user?.Email,
            FullName = user?.FullName,
            Roles = roles,
            CompanyId = user?.CompanyId,
            CompanyName = companyName
        });
    }

    [HttpPost("refresh")]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request = null)
    {
        var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { Message = "Refresh token is missing." });
        }

        var command = new RefreshTokenCommand(request?.AccessToken, refreshToken, GetIpAddress());
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new { result.Errors });
        }

        SetTokenCookie(result.RefreshToken);

        Guid? companyId = null;
        string? companyName = null;
        if (Guid.TryParse(result.UserId, out var parsedUserId))
        {
            var user = await _userManager.FindByIdAsync(parsedUserId.ToString());
            if (user?.CompanyId != null)
            {
                companyId = user.CompanyId;
                var company = await _companyRepository.GetByIdAsync(user.CompanyId.Value);
                companyName = company?.Name;
            }
        }

        return Ok(new
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            UserId = result.UserId,
            Email = result.Email,
            FullName = result.FullName,
            Roles = result.Roles ?? Array.Empty<string>(),
            CompanyId = companyId,
            CompanyName = companyName
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
    {
        var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            Response.Cookies.Delete("refreshToken");
            return Ok(new { Message = "Logout successful" });
        }

        var command = new LogoutCommand(refreshToken, GetIpAddress());
        var result = await _mediator.Send(command);

        Response.Cookies.Delete("refreshToken");

        if (!result.Success)
        {
            return BadRequest(new { result.Errors });
        }

        return Ok(new { Message = "Logout successful" });
    }

    private void SetTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            Secure = true, // Set to true if using HTTPS
            SameSite = SameSiteMode.Strict
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }

    private string GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var value))
        {
            return value!;
        }
        else
        {
            return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "0.0.0.0";
        }
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; } // Optional, can be fetched from cookies
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; } // Optional, can be fetched from cookies
}
