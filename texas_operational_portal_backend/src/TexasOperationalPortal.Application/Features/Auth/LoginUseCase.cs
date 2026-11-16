using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Auth;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Application.Features.Auth;

public sealed class LoginUseCase
{
    private readonly IOperationalDbContext _db;
    private readonly ITokenService _tokenService;

    public LoginUseCase(
        IOperationalDbContext db,
        ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResult?> ExecuteAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user != null)
        {
            if (!user.IsActive) return null;

            var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!passwordMatches) return null;

            var permissions = GetPermissionsList(user.PermissionsJson);
            var tokenResult = _tokenService.GenerateTokens(user, permissions);

            var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokenResult.RefreshToken));
            var tokenHash = Convert.ToBase64String(hashBytes);

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                IsUsed = false
            };
            _db.RefreshTokens.Add(refreshTokenEntity);

            user.LastLoginAt = DateTime.UtcNow;

            var auditLog = new AuditLog
            {
                UserId = user.Id,
                Action = "USER_LOGIN_SUCCESS",
                Timestamp = DateTime.UtcNow,
                Details = $"User logged in successfully: {email}"
            };
            _db.AuditLogs.Add(auditLog);

            await _db.SaveChangesAsync(ct);

            var loginResponse = new LoginResponse(
                user.Id,
                tokenResult.AccessToken,
                user.Name,
                user.Email,
                user.UserType.ToString(),
                permissions,
                user.AvatarUrl);

            return new AuthResult(loginResponse, tokenResult.RefreshToken);
        }

        return null;
    }

    private static List<string> GetPermissionsList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
