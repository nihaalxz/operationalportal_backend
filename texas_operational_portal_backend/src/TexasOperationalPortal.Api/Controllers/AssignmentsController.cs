using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Exceptions;
using TexasOperationalPortal.Application.Features.Assignments;
using TexasOperationalPortal.Contracts.Assignments;

namespace TexasOperationalPortal.Api.Controllers;

[ApiController]
[Route("api/assignments")]
[Authorize(Policy = "OperationalPortalAccess")]
[Produces("application/json")]
public sealed class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;

    public AssignmentsController(IAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpPost("claim")]
    [ProducesResponseType(typeof(OperationalAssignmentResponse), 200)]
    [ProducesResponseType(409)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Claim([FromBody] ClaimAssignmentRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var assignment = await _assignmentService.ClaimOperationAsync(request, userId, ct);
            return Ok(assignment);
        }
        catch (AssignmentConflictException ex)
        {
            return StatusCode(409, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(MyAssignmentsSummaryResponse), 200)]
    public async Task<IActionResult> GetMyAssignments(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _assignmentService.GetMyAssignmentsAsync(userId, ct);
        return Ok(result);
    }

    [HttpGet("completed")]
    [ProducesResponseType(typeof(IReadOnlyList<OperationalAssignmentResponse>), 200)]
    public async Task<IActionResult> GetCompleted(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _assignmentService.GetCompletedAssignmentsAsync(userId, ct);
        return Ok(result);
    }

    [HttpGet("workload-limits")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<WorkloadLimitResponse>), 200)]
    public async Task<IActionResult> GetWorkloadLimits(CancellationToken ct)
    {
        var result = await _assignmentService.GetWorkloadLimitsAsync(ct);
        return Ok(result);
    }

    [HttpPut("workload-limits")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<WorkloadLimitResponse>), 200)]
    public async Task<IActionResult> UpdateWorkloadLimits([FromBody] UpdateWorkloadLimitsBatchRequest request, CancellationToken ct)
    {
        long userId = 1;
        try { userId = GetCurrentUserId(); } catch { }
        var result = await _assignmentService.UpdateWorkloadLimitsAsync(request, userId, ct);
        return Ok(result);
    }

    [HttpGet("workload-summary")]
    [ProducesResponseType(typeof(OperatorWorkloadSummaryResponse), 200)]
    public async Task<IActionResult> GetWorkloadSummary(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _assignmentService.GetOperatorWorkloadSummaryAsync(userId, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = "CanViewVisas")] // Admin or Master Accountant or Staff with permission
    [ProducesResponseType(typeof(IReadOnlyList<OperationalAssignmentResponse>), 200)]
    public async Task<IActionResult> GetAllActiveAssignments(CancellationToken ct)
    {
        var result = await _assignmentService.GetAllActiveAssignmentsAsync(ct);
        return Ok(result);
    }

    [HttpPost("{id:long}/release")]
    [ProducesResponseType(typeof(OperationalAssignmentResponse), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Release(
        [FromRoute] long id,
        [FromBody] ReleaseAssignmentRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdminOrAccounts = IsAdminOrAccounts();
            var result = await _assignmentService.ReleaseOperationAsync(id, request.Reason, userId, isAdminOrAccounts, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/reassign")]
    [ProducesResponseType(typeof(OperationalAssignmentResponse), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Reassign(
        [FromRoute] long id,
        [FromBody] ReassignAssignmentRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdminOrAccounts = IsAdminOrAccounts();
            var result = await _assignmentService.ReassignOperationAsync(id, request.TargetUserId, request.Reason, userId, isAdminOrAccounts, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<OperationalAssignmentHistoryResponse>), 200)]
    public async Task<IActionResult> GetHistory([FromRoute] long id, CancellationToken ct)
    {
        var result = await _assignmentService.GetAssignmentHistoryAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("/api/internal/assignments/batch")]
    [HttpGet("check-batch")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<OperationalAssignmentResponse>), 200)]
    public async Task<IActionResult> CheckBatch(
        [FromQuery] string entityType,
        [FromQuery] string? ids,
        [FromQuery] string? entityIds,
        CancellationToken ct)
    {
        var rawIds = ids ?? entityIds;
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(rawIds))
            return Ok(Array.Empty<OperationalAssignmentResponse>());

        var idList = new List<long>();
        foreach (var parts in rawIds.Split(','))
        {
            if (long.TryParse(parts.Trim(), out var parsedId))
                idList.Add(parsedId);
        }

        var result = await _assignmentService.GetActiveAssignmentsForEntitiesAsync(entityType, idList, ct);
        return Ok(result);
    }

    private long GetCurrentUserId()
    {
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("id")?.Value
                       ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (long.TryParse(subClaim, out var userId) && userId > 0)
        {
            return userId;
        }

        throw new UnauthorizedAccessException("User ID claim not found in authentication token.");
    }

    private bool IsAdminOrAccounts()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "Accounts", StringComparison.OrdinalIgnoreCase);
    }
}
