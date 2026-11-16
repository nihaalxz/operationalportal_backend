using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Staff;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Application.Features.Staff;

public sealed class StaffSyncUseCase
{
    private readonly IOperationalDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<StaffSyncUseCase> _logger;

    private static readonly HashSet<string> AllowedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "visas", "vfs", "holidays", "manage_users"
    };

    public StaffSyncUseCase(
        IOperationalDbContext db,
        IConfiguration config,
        ILogger<StaffSyncUseCase> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(StaffSyncRequest request, string? syncKeyHeader, CancellationToken ct)
    {
        var expectedKey = _config["StaffSync:SyncKey"] ?? "730f173df8e40c2284961c1b7e1333610f4158e2a2954fc3c2cfd908b43ffb5b";
        if (string.IsNullOrWhiteSpace(syncKeyHeader) || 
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(syncKeyHeader), 
                System.Text.Encoding.UTF8.GetBytes(expectedKey)))
        {
            _logger.LogWarning("Unauthorized staff sync attempt detected.");
            return false;
        }

        if (!Enum.TryParse<UserType>(request.UserType, true, out var userType))
        {
            throw new ArgumentException($"Invalid UserType: '{request.UserType}'. Allowed values are Admin, Accounts, Operations.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var filteredPermissions = request.Permissions?
            .Where(p => AllowedPermissions.Contains(p))
            .ToList() ?? new List<string>();

        var permissionsJson = JsonSerializer.Serialize(filteredPermissions);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        var isNew = user == null;

        string? newPasswordHash = null;
        if (!string.IsNullOrWhiteSpace(request.PasswordHash))
        {
            if (request.PasswordHash.StartsWith("$2a$") || request.PasswordHash.StartsWith("$2b$") || request.PasswordHash.StartsWith("$2y$"))
            {
                newPasswordHash = request.PasswordHash;
            }
            else
            {
                newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.PasswordHash, workFactor: 12);
            }
        }

        if (isNew)
        {
            user = new OperationalUser
            {
                Email = email,
                Name = request.Name,
                PasswordHash = newPasswordHash ?? BCrypt.Net.BCrypt.HashPassword("password123", workFactor: 12),
                UserType = userType,
                IsActive = request.IsActive,
                PermissionsJson = permissionsJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            user!.Name = request.Name;
            if (!string.IsNullOrWhiteSpace(newPasswordHash))
            {
                user.PasswordHash = newPasswordHash;
            }
            user.UserType = userType;
            user.IsActive = request.IsActive;
            user.PermissionsJson = permissionsJson;
            user.UpdatedAt = DateTime.UtcNow;
        }

        var auditLog = new AuditLog
        {
            UserId = user.Id > 0 ? user.Id : null,
            Action = isNew ? "STAFF_PROVISIONED" : "STAFF_UPDATED",
            Timestamp = DateTime.UtcNow,
            Details = $"Synced staff account for email: {email}, type: {userType}, active: {request.IsActive}"
        };
        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Successfully synced staff account for {Email}.", email);
        return true;
    }
}
