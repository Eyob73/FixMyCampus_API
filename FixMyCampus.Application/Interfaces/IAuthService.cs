using FixMyCampus.Application.DTOs.Auth;

namespace FixMyCampus.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthResponseDto> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthResponseDto> RefreshTokenAsync(string rawRefreshToken, string? ipAddress = null);
    Task RevokeTokenAsync(string rawRefreshToken, string? ipAddress = null, string reason = "Revoked manually");
}

