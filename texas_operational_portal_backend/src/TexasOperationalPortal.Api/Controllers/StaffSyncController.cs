using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Features.Staff;
using TexasOperationalPortal.Contracts.Staff;

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TexasOperationalPortal.Application.Common.Interfaces;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/internal")]
[Produces("application/json")]
public sealed class StaffSyncController : ControllerBase
{
    private readonly StaffSyncUseCase _syncUseCase;
    private readonly IOperationalDbContext _db;

    public StaffSyncController(StaffSyncUseCase syncUseCase, IOperationalDbContext db)
    {
        _syncUseCase = syncUseCase;
        _db = db;
    }

    [HttpPost("staff-sync")]
    [HttpPost("/api/public/staff/sync")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SyncStaff([FromBody] StaffSyncRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Bad Request",
                Detail = "Required request body parameters are missing or invalid."
            });
        }

        Request.Headers.TryGetValue("X-SYNC-KEY", out var syncKeyHeader);

        try
        {
            var success = await _syncUseCase.ExecuteAsync(request, syncKeyHeader, ct);
            if (!success)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "Missing or invalid X-SYNC-KEY header."
                });
            }

            return Ok(new { Message = "Staff synced successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Bad Request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("staff-list")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStaffList(CancellationToken ct)
    {
        Request.Headers.TryGetValue("X-SYNC-KEY", out var syncKeyHeader);
        if (string.IsNullOrWhiteSpace(syncKeyHeader))
            return Unauthorized();

        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        var result = users.Select(u => new
        {
            u.Id,
            u.Name,
            u.Email,
            UserType = u.UserType.ToString(),
            u.IsActive,
            Permissions = string.IsNullOrWhiteSpace(u.PermissionsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(u.PermissionsJson) ?? new List<string>(),
            u.LastLoginAt,
            u.LastLoginIp,
            u.CreatedAt,
            u.UpdatedAt
        });

        return Ok(result);
    }

    [HttpPut("staff/{id:long}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> ToggleStaffStatus([FromRoute] long id, [FromBody] Dictionary<string, bool> body, CancellationToken ct)
    {
        Request.Headers.TryGetValue("X-SYNC-KEY", out var syncKeyHeader);
        if (string.IsNullOrWhiteSpace(syncKeyHeader))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return NotFound(new { Message = "User not found in Operational Portal." });

        if (body.TryGetValue("isActive", out var isActive))
        {
            user.IsActive = isActive;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { Message = $"Operational user status updated to {(user.IsActive ? "active" : "inactive")}." });
    }

    [HttpPut("staff/{id:long}/password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetStaffPassword([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        Request.Headers.TryGetValue("X-SYNC-KEY", out var syncKeyHeader);
        if (string.IsNullOrWhiteSpace(syncKeyHeader))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return NotFound(new { Message = "User not found in Operational Portal." });

        if (body.TryGetValue("newPassword", out var newPassword) && !string.IsNullOrWhiteSpace(newPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { Message = "Operational user password reset successfully." });
    }

    [HttpPut("staff/{id:long}/email")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateStaffEmail([FromRoute] long id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        Request.Headers.TryGetValue("X-SYNC-KEY", out var syncKeyHeader);
        if (string.IsNullOrWhiteSpace(syncKeyHeader))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return NotFound(new { Message = "User not found in Operational Portal." });

        if (body.TryGetValue("newEmail", out var newEmail) && !string.IsNullOrWhiteSpace(newEmail))
        {
            var cleanEmail = newEmail.Trim().ToLowerInvariant();
            var existing = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail && u.Id != id, ct);
            if (existing != null)
            {
                return Conflict(new { Message = $"Operational user with email '{newEmail}' already exists." });
            }

            user.Email = cleanEmail;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { Message = "Operational user email updated successfully." });
    }
}

