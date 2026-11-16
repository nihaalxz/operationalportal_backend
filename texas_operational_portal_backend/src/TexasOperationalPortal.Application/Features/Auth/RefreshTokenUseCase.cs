using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Auth;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Application.Features.Auth;

public sealed class RefreshTokenUseCase
{
    private readonly IOperationalDbContext _db;
    private readonly ITokenService _tokenService;

    public RefreshTokenUseCase(IOperationalDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResult?> ExecuteAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return null;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));
        var tokenHash = Convert.ToBase64String(hashBytes);

        var tokenEntity = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenHash, ct);

        if (tokenEntity == null)
        {
            return null;
        }

        var user = tokenEntity.User;

        if (tokenEntity.IsUsed || tokenEntity.IsRevoked || tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            if (tokenEntity.IsUsed)
            {
                var activeTokens = await _db.RefreshTokens
                    .Where(t => t.UserId == tokenEntity.UserId && !t.IsRevoked)
                    .ToListAsync(ct);

                foreach (var t in activeTokens)
                {
                    t.IsRevoked = true;
                }

                var auditLog = new AuditLog
                {
                    UserId = tokenEntity.UserId,
                    Action = "REFRESH_TOKEN_REUSE_BREACH",
                    Timestamp = DateTime.UtcNow,
                    Details = $"Token reuse breach detected. Revoked all active sessions for User: {user.Email}. IP or Client reused token hash: {tokenHash}"
                };
                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync(ct);
            }
            return null;
        }

        tokenEntity.IsUsed = true;
        tokenEntity.IsRevoked = true;

        if (!user.IsActive)
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }

        var permissions = GetPermissionsList(user.PermissionsJson);
        var tokenResult = _tokenService.GenerateTokens(user, permissions);

        var newHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(tokenResult.RefreshToken));
        var newHash = Convert.ToBase64String(newHashBytes);

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            IsUsed = false
        };
        _db.RefreshTokens.Add(newRefreshTokenEntity);

        var successLog = new AuditLog
        {
            UserId = user.Id,
            Action = "SESSION_REFRESHED",
            Timestamp = DateTime.UtcNow,
            Details = $"Successfully rotated refresh token for user: {user.Email}"
        };
        _db.AuditLogs.Add(successLog);

        await _db.SaveChangesAsync(ct);

        var response = new LoginResponse(
            user.Id,
            tokenResult.AccessToken,
            user.Name,
            user.Email,
            user.UserType.ToString(),
            permissions,
            user.AvatarUrl
        );

        return new AuthResult(response, tokenResult.RefreshToken);
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
