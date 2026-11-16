using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Features.Auth;
using TexasOperationalPortal.Contracts.Auth;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AuthLimiter")]
public sealed class AuthController : ControllerBase
{
    private readonly LoginUseCase _loginUseCase;
    private readonly RefreshTokenUseCase _refreshTokenUseCase;
    private readonly IOperationalDbContext _db;

    public AuthController(
        LoginUseCase loginUseCase,
        RefreshTokenUseCase refreshTokenUseCase,
        IOperationalDbContext db)
    {
        _loginUseCase = loginUseCase;
        _refreshTokenUseCase = refreshTokenUseCase;
        _db = db;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400), ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Bad Request",
                Detail = "Email and password are required."
            });
        }

        var result = await _loginUseCase.ExecuteAsync(request, ct);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Unauthorized",
                Detail = "Invalid email or password."
            });
        }

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Unauthorized",
                Detail = "Missing refresh token."
            });
        }

        var result = await _refreshTokenUseCase.ExecuteAsync(rawToken, ct);
        if (result is null)
        {
            ClearRefreshTokenCookie();
            return Unauthorized(new ProblemDetails
            {
                Status = 401,
                Title = "Unauthorized",
                Detail = "Invalid or expired refresh token."
            });
        }

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(result.Response);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue("refresh_token", out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            var tokenHash = Convert.ToBase64String(hashBytes);

            var tokenEntity = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenHash, ct);
            if (tokenEntity != null)
            {
                tokenEntity.IsRevoked = true;

                var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
                var auditLog = new AuditLog
                {
                    UserId = tokenEntity.UserId,
                    Action = "USER_LOGOUT",
                    Timestamp = DateTime.UtcNow,
                    Details = $"User logged out: {emailClaim}"
                };
                _db.AuditLogs.Add(auditLog);

                await _db.SaveChangesAsync(ct);
            }
        }

        ClearRefreshTokenCookie();

        return Ok(new { Message = "Logged out successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(subClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        var permissions = GetPermissionsList(user.PermissionsJson);

        var response = new LoginResponse(
            Id: user.Id,
            AccessToken: "",
            Name: user.Name,
            Email: user.Email,
            UserType: user.UserType.ToString(),
            Permissions: permissions,
            AvatarUrl: user.AvatarUrl
        );

        return Ok(response);
    }

    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400), ProducesResponseType(401)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Bad Request", Detail = "Name is required." });
        }

        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(subClaim, out var userId)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || !user.IsActive) return Unauthorized();

        user.Name = request.Name.Trim();
        user.AvatarUrl = request.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            UserId = user.Id,
            Action = "USER_PROFILE_UPDATED",
            Timestamp = DateTime.UtcNow,
            Details = $"User profile updated: Name={user.Name}, HasAvatar={!string.IsNullOrEmpty(user.AvatarUrl)}"
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(ct);
        return Ok(new { Message = "Profile updated successfully.", Name = user.Name, AvatarUrl = user.AvatarUrl });
    }

    [HttpPost("reset-password")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400), ProducesResponseType(401)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Bad Request", Detail = "Current and new password are required." });
        }

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Bad Request", Detail = "New password must be at least 6 characters long." });
        }

        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(subClaim, out var userId)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null || !user.IsActive) return Unauthorized();

        var passwordMatches = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);
        if (!passwordMatches)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Bad Request", Detail = "Current password is incorrect." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            UserId = user.Id,
            Action = "USER_PASSWORD_RESET",
            Timestamp = DateTime.UtcNow,
            Details = "User password reset successfully."
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(ct);
        return Ok(new { Message = "Password reset successfully." });
    }

    private void SetRefreshTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refresh_token", token, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }

    private static System.Collections.Generic.List<string> GetPermissionsList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
