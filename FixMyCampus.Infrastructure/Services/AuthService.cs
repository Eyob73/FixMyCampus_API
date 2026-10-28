using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FixMyCampus.Application.DTOs.Auth;
using FixMyCampus.Application.DTOs.Users;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FixMyCampus.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Ensure Reporter role exists and assign it
        await _userManager.AddToRoleAsync(user, "Reporter");

        var (rawRefreshToken, refreshTokenEntity) = CreateRefreshToken(user.Id, ipAddress);
        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync();

        return await GenerateAuthResponse(user, rawRefreshToken, refreshTokenEntity.ExpiresAt);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account has been deactivated or disabled.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedAccessException("Account is locked out. Please try again later.");
        }

        if (_userManager.Options.SignIn.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
        {
            throw new UnauthorizedAccessException("Please confirm your email address before logging in.");
        }
        
        if (_userManager.Options.SignIn.RequireConfirmedAccount && !await _userManager.IsEmailConfirmedAsync(user))
        {
            throw new UnauthorizedAccessException("Please confirm your account before logging in.");
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                throw new UnauthorizedAccessException("Account is locked out. Please try again later.");
            }
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var (rawRefreshToken, refreshTokenEntity) = CreateRefreshToken(user.Id, ipAddress);
        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync();

        return await GenerateAuthResponse(user, rawRefreshToken, refreshTokenEntity.ExpiresAt);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string rawRefreshToken, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var tokenHash = HashToken(rawRefreshToken);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var committed = false;
        try
        {
            var token = await _dbContext.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

            if (token == null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            if (token.IsRevoked || token.RevokedAt != null)
            {
                throw new UnauthorizedAccessException("Refresh token expired or revoked.");
            }

            // If the token was already rotated/used:
            if (token.IsUsed || token.ReplacedByTokenHash != null)
            {
                // Check if it's within the rotation grace period (ExpiresAt stores the grace period deadline: 60 seconds)
                if (token.ExpiresAt > DateTime.UtcNow)
                {
                    var graceUser = token.User ?? await _userManager.FindByIdAsync(token.UserId.ToString());
                    if (graceUser == null)
                    {
                        throw new UnauthorizedAccessException("User not found.");
                    }

                    if (await _userManager.IsLockedOutAsync(graceUser))
                    {
                        throw new UnauthorizedAccessException("Account locked due to multiple failed login attempts.");
                    }

                    if (!graceUser.IsActive)
                    {
                        throw new UnauthorizedAccessException("This account has been deactivated or disabled.");
                    }

                    var latestToken = await _dbContext.RefreshTokens
                        .Where(rt => rt.UserId == token.UserId && !rt.IsUsed && !rt.IsRevoked && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
                        .OrderByDescending(rt => rt.CreatedAt)
                        .FirstOrDefaultAsync();

                    var graceResponse = await GenerateAuthResponse(graceUser, null, latestToken?.ExpiresAt ?? token.ExpiresAt);
                    await transaction.CommitAsync();
                    committed = true;
                    return graceResponse;
                }

                // Outside the grace period: genuine reuse / token theft
                var userTokens = await _dbContext.RefreshTokens
                    .Where(rt => rt.UserId == token.UserId)
                    .ToListAsync();

                foreach (var t in userTokens)
                {
                    t.IsRevoked = true;
                    t.RevokedAt = DateTime.UtcNow;
                    t.RevokedByIp = ipAddress;
                    t.ReasonRevoked = "Token theft detected. All user sessions revoked.";
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                throw new UnauthorizedAccessException("Token theft detected. All user sessions revoked.");
            }

            // Check if expired
            if (token.ExpiresAt < DateTime.UtcNow)
            {
                throw new UnauthorizedAccessException("Refresh token expired or revoked.");
            }

            // Validate user status
            var user = token.User;
            if (user == null || !user.IsActive)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = "Account is inactive or disabled";
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                throw new UnauthorizedAccessException("This account has been deactivated or disabled.");
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = "Account is locked out";
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                committed = true;

                throw new UnauthorizedAccessException("Account is locked out. Please try again later.");
            }

            // Mark current token as used and set grace period expiration to 60 seconds from now
            token.IsUsed = true;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(60);

            var (newRawRefreshToken, newRefreshTokenEntity) = CreateRefreshToken(user.Id, ipAddress);
            token.ReplacedByTokenHash = newRefreshTokenEntity.TokenHash;

            _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            committed = true;

            return await GenerateAuthResponse(user, newRawRefreshToken, newRefreshTokenEntity.ExpiresAt);
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
    }

    public async Task RevokeTokenAsync(string rawRefreshToken, string? ipAddress = null, string reason = "Revoked manually")
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;

        var tokenHash = HashToken(rawRefreshToken);
        var token = await _dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
        if (token == null) return;

        token.IsRevoked = true;
        token.RevokedAt = DateTime.UtcNow;
        token.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        token.RevokedByIp = ipAddress;
        token.ReasonRevoked = reason;

        await _dbContext.SaveChangesAsync();
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private (string rawToken, RefreshToken entity) CreateRefreshToken(Guid userId, string? ipAddress)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(rawToken);

        var expirationDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7);
        if (expirationDays <= 0) expirationDays = 7;

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedByIp = ipAddress,
            IsUsed = false,
            IsRevoked = false
        };

        return (rawToken, entity);
    }

    private async Task<AuthResponseDto> GenerateAuthResponse(
        ApplicationUser user,
        string? rawRefreshToken = null,
        DateTime? refreshTokenExpiresAt = null)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.FullName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var jwtKey = _configuration["Jwt:Key"] ?? "VerySecretKeyForDevelopmentOnlyDoNotUseInProd123!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expirationMinutes = _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 15);
        if (expirationMinutes <= 0) expirationMinutes = 15;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "FixMyCampus",
            audience: _configuration["Jwt:Audience"] ?? "FixMyCampusUsers",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds
        );

        return new AuthResponseDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            ExpiresIn = expirationMinutes * 60,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName
            },
            Roles = roles
        };
    }
}
