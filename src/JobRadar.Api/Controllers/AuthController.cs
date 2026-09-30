using JobRadar.Application.Features.Auth.Commands.Login;
using JobRadar.Application.Features.Auth.Commands.Logout;
using JobRadar.Application.Features.Auth.Commands.RefreshToken;
using JobRadar.Application.Features.Auth.Commands.Register;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace JobRadar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
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
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password, GetIpAddress());
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return Unauthorized(new { result.Errors });
        }

        SetTokenCookie(result.RefreshToken);

        return Ok(new
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken // Optional: return in body or just keep in cookie
        });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var refreshToken = request.RefreshToken ?? Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return BadRequest(new { Message = "Token is required" });
        }

        var command = new RefreshTokenCommand(request.AccessToken, refreshToken, GetIpAddress());
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Errors });
        }

        SetTokenCookie(result.RefreshToken);

        return Ok(new
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var refreshToken = request.RefreshToken ?? Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return BadRequest(new { Message = "Token is required" });
        }

        var command = new LogoutCommand(refreshToken, GetIpAddress());
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Errors });
        }

        Response.Cookies.Delete("refreshToken");

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
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; } // Optional, can be fetched from cookies
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; } // Optional, can be fetched from cookies
}
