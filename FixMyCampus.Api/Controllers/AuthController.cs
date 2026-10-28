using FixMyCampus.Application.DTOs.Auth;
using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FixMyCampus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private const string RefreshTokenCookieName = "refreshToken";

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, GetIpAddress());
            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                SetRefreshTokenCookie(response.RefreshToken);
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [EnableRateLimiting("AuthLimiter")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request, GetIpAddress());
            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                SetRefreshTokenCookie(response.RefreshToken);
            }
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        try
        {
            var tokenToRefresh = string.IsNullOrEmpty(request?.RefreshToken)
                ? Request.Cookies["fmc_refresh"] ?? Request.Cookies["refreshToken"] ?? Request.Cookies["ims_refresh"]
                : request.RefreshToken;

            if (string.IsNullOrWhiteSpace(tokenToRefresh))
            {
                return Unauthorized(new { message = "Refresh token is missing." });
            }

            var response = await _authService.RefreshTokenAsync(tokenToRefresh, GetIpAddress());
            if (!string.IsNullOrEmpty(response.RefreshToken))
            {
                SetRefreshTokenCookie(response.RefreshToken);
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex) when (ex.Message.Contains("theft"))
        {
            DeleteRefreshTokenCookie();
            return Unauthorized(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest? request = null)
    {
        var refreshToken = string.IsNullOrEmpty(request?.RefreshToken)
            ? Request.Cookies["fmc_refresh"] ?? Request.Cookies["refreshToken"] ?? Request.Cookies["ims_refresh"]
            : request.RefreshToken;

        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeTokenAsync(refreshToken, GetIpAddress(), "Revoked on logout");
        }

        DeleteRefreshTokenCookie();
        return Ok(new { message = "Logout successful." });
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequest request)
    {
        var refreshToken = string.IsNullOrEmpty(request?.RefreshToken)
            ? Request.Cookies["fmc_refresh"] ?? Request.Cookies["refreshToken"] ?? Request.Cookies["ims_refresh"]
            : request.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new { message = "Refresh token is required." });
        }

        await _authService.RevokeTokenAsync(refreshToken, GetIpAddress(), "Revoked by user");
        DeleteRefreshTokenCookie();
        return Ok(new { message = "Token revoked successfully." });
    }

    [HttpPut("profile")]
    public IActionResult UpdateProfile([FromBody] dynamic dto)
    {
        return Ok(dto);
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var isHttps = Request.IsHttps;
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("fmc_refresh", refreshToken, cookieOptions);
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void DeleteRefreshTokenCookie()
    {
        var isHttps = Request.IsHttps;
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/"
        };
        Response.Cookies.Delete("fmc_refresh", options);
        Response.Cookies.Delete("refreshToken", options);
        Response.Cookies.Delete("ims_refresh", options);
        Response.Cookies.Delete("fmc_auth", options);
        Response.Cookies.Delete("XSRF-TOKEN", options);

        var apiOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/api"
        };
        Response.Cookies.Delete("fmc_refresh", apiOptions);
        Response.Cookies.Delete("refreshToken", apiOptions);
    }

    private string? GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedHeader))
        {
            var ip = forwardedHeader.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip)) return ip;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
