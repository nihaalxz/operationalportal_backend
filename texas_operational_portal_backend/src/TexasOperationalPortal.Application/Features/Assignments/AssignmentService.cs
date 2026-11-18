using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Exceptions;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Assignments;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Application.Features.Assignments;

public sealed class AssignmentService : IAssignmentService
{
    private readonly IOperationalDbContext _db;
    private readonly IAssignmentCacheService _cacheService;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IOperationalDbContext db,
        IAssignmentCacheService cacheService,
        ILogger<AssignmentService> logger)
    {
        _db = db;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<OperationalAssignmentResponse> ClaimOperationAsync(
        ClaimAssignmentRequest request, long userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EntityType))
            throw new ArgumentException("EntityType is required.", nameof(request));
        if (request.EntityId < 0)
            throw new ArgumentException("Valid EntityId is required.", nameof(request));

        var entityTypeUpper = request.EntityType.Trim().ToUpperInvariant();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.IsActive, ct);
            if (user == null)
                throw new KeyNotFoundException("Logged-in operational user not found.");
            userId = user.Id;
        }

        // Fetch workload limits (cached in-memory)
        var limits = await GetWorkloadLimitsAsync(ct);
        var limitConfig = limits
            .Where(w => w.EntityType.Equals(entityTypeUpper, StringComparison.OrdinalIgnoreCase) && (w.UserId == userId || w.UserId == null))
            .OrderByDescending(w => w.UserId)
            .FirstOrDefault();
        var maxLimit = limitConfig?.MaxActiveLimit ?? 3;

        // Combined single query for active assignments for this entity OR for this operator (SARGable)
        var activeAssignments = await _db.OperationalAssignments
            .AsNoTracking()
            .Where(a => a.Status == "Assigned" && (a.AssignedToUserId == userId || (a.EntityId == request.EntityId && (a.EntityType == request.EntityType || a.EntityType == entityTypeUpper))))
            .Select(a => new { a.Id, a.EntityType, a.EntityId, a.EntityDocumentNumber, a.AssignedToUserId, a.AssignedAt, a.Status, a.Remarks })
            .ToListAsync(ct);

        var existingActive = activeAssignments.FirstOrDefault(a => 
            a.EntityId == request.EntityId && 
            a.EntityType.Equals(request.EntityType, StringComparison.OrdinalIgnoreCase));

        if (existingActive != null)
        {
            if (existingActive.AssignedToUserId == userId)
            {
                return MapToResponse(
                    new OperationalAssignment
                    {
                        Id = existingActive.Id,
                        EntityType = existingActive.EntityType,
                        EntityId = existingActive.EntityId,
                        EntityDocumentNumber = existingActive.EntityDocumentNumber,
                        AssignedToUserId = existingActive.AssignedToUserId,
                        AssignedAt = existingActive.AssignedAt,
                        Status = existingActive.Status,
                        LastModifiedByUserId = userId,
                        LastModifiedAt = existingActive.AssignedAt,
                        Remarks = existingActive.Remarks
                    },
                    user.Name, null, user.Name);
            }
            throw new AssignmentConflictException("This operation has already been assigned to another operator.");
        }

        var currentActiveCount = activeAssignments.Count(a => 
            a.AssignedToUserId == userId && 
            a.EntityType.Equals(request.EntityType, StringComparison.OrdinalIgnoreCase));

        if (currentActiveCount >= maxLimit)
        {
            throw new AssignmentConflictException(
                $"Workload limit reached: You currently have {currentActiveCount} active {request.EntityType} applications in progress (limit: {maxLimit}). Please complete an active application before starting a new one.");
        }

        // Single batch creation of Assignment + History in 1 atomic SaveChangesAsync call
        var assignment = new OperationalAssignment
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            EntityDocumentNumber = request.EntityDocumentNumber,
            AssignedToUserId = userId,
            AssignedAt = DateTime.UtcNow,
            Status = "Assigned",
            LastModifiedByUserId = userId,
            LastModifiedAt = DateTime.UtcNow,
            Remarks = request.Remarks
        };

        var history = new OperationalAssignmentHistory
        {
            Assignment = assignment,
            Action = "Claimed",
            PerformedByUserId = userId,
            PerformedAt = DateTime.UtcNow,
            NewAssignedUserId = userId,
            Reason = request.Remarks ?? "Operation claimed by operator."
        };

        _db.OperationalAssignments.Add(assignment);
        _db.OperationalAssignmentHistories.Add(history);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new AssignmentConflictException("This operation has already been assigned to another operator.", ex);
        }

        await InvalidateCacheAsync(userId);

        return MapToResponse(assignment, user.Name, null, user.Name);
    }

    public async Task<OperationalAssignmentResponse> ReleaseOperationAsync(
        long assignmentId, string? reason, long performedByUserId, bool isAdminOrAccounts, CancellationToken ct)
    {
        var assignment = await _db.OperationalAssignments
            .Include(a => a.AssignedToUser)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);

        if (assignment == null)
            throw new KeyNotFoundException($"Assignment with ID {assignmentId} not found.");

        if (assignment.Status != "Assigned")
            throw new InvalidOperationException($"Cannot release assignment with status '{assignment.Status}'.");

        if (!isAdminOrAccounts && assignment.AssignedToUserId != performedByUserId)
            throw new UnauthorizedAccessException("You are not authorized to release an assignment owned by another operator.");

        var oldUserId = assignment.AssignedToUserId;
        var performer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == performedByUserId, ct);

        assignment.Status = "Released";
        assignment.LastModifiedByUserId = performedByUserId;
        assignment.LastModifiedAt = DateTime.UtcNow;
        assignment.Remarks = reason ?? "Released back to queue.";

        var history = new OperationalAssignmentHistory
        {
            AssignmentId = assignment.Id,
            Action = "Released",
            PerformedByUserId = performedByUserId,
            PerformedAt = DateTime.UtcNow,
            OldAssignedUserId = oldUserId,
            NewAssignedUserId = null,
            Reason = reason ?? "Released back to shared queue."
        };

        _db.OperationalAssignmentHistories.Add(history);
        await _db.SaveChangesAsync(ct);

        await InvalidateCacheAsync(oldUserId, performedByUserId);

        return MapToResponse(assignment, assignment.AssignedToUser?.Name ?? "Unknown", null, performer?.Name ?? "Unknown");
    }

    public async Task<OperationalAssignmentResponse> ReassignOperationAsync(
        long assignmentId, long targetUserId, string? reason, long performedByUserId, bool isAdminOrAccounts, CancellationToken ct)
    {
        if (!isAdminOrAccounts)
            throw new UnauthorizedAccessException("Only Administrators and Master Accountants can reassign operations.");

        var assignment = await _db.OperationalAssignments
            .Include(a => a.AssignedToUser)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);

        if (assignment == null)
            throw new KeyNotFoundException($"Assignment with ID {assignmentId} not found.");

        if (assignment.Status != "Assigned")
            throw new InvalidOperationException($"Cannot reassign an operation with status '{assignment.Status}'.");

        var targetUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
        if (targetUser == null)
            throw new KeyNotFoundException($"Target operational user with ID {targetUserId} not found.");

        var oldUserId = assignment.AssignedToUserId;
        var performer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == performedByUserId, ct);

        assignment.AssignedToUserId = targetUserId;
        assignment.LastModifiedByUserId = performedByUserId;
        assignment.LastModifiedAt = DateTime.UtcNow;
        assignment.Remarks = reason ?? $"Reassigned to {targetUser.Name}.";

        var history = new OperationalAssignmentHistory
        {
            AssignmentId = assignment.Id,
            Action = "Reassigned",
            PerformedByUserId = performedByUserId,
            PerformedAt = DateTime.UtcNow,
            OldAssignedUserId = oldUserId,
            NewAssignedUserId = targetUserId,
            Reason = reason ?? $"Reassigned to {targetUser.Name}."
        };

        _db.OperationalAssignmentHistories.Add(history);
        await _db.SaveChangesAsync(ct);

        await InvalidateCacheAsync(oldUserId, targetUserId, performedByUserId);

        return MapToResponse(assignment, targetUser.Name, null, performer?.Name ?? "Unknown");
    }

    public async Task CompleteOperationAsync(
        string entityType, long entityId, long completedByUserId, CancellationToken ct)
    {
        var assignment = await _db.OperationalAssignments
            .FirstOrDefaultAsync(a => a.EntityType.ToLower() == entityType.ToLower()
                                   && a.EntityId == entityId
                                   && a.Status == "Assigned", ct);

        if (assignment == null) return; // No active assignment to close

        var performer = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == completedByUserId, ct);

        assignment.Status = "Completed";
        assignment.CompletedByUserId = completedByUserId;
        assignment.CompletedAt = DateTime.UtcNow;
        assignment.LastModifiedByUserId = completedByUserId;
        assignment.LastModifiedAt = DateTime.UtcNow;

        var history = new OperationalAssignmentHistory
        {
            AssignmentId = assignment.Id,
            Action = "Completed",
            PerformedByUserId = completedByUserId,
            PerformedAt = DateTime.UtcNow,
            OldAssignedUserId = assignment.AssignedToUserId,
            NewAssignedUserId = null,
            Reason = "Operation processing completed."
        };

        _db.OperationalAssignmentHistories.Add(history);
        await _db.SaveChangesAsync(ct);

        await InvalidateCacheAsync(assignment.AssignedToUserId, completedByUserId);
    }

    public async Task<MyAssignmentsSummaryResponse> GetMyAssignmentsAsync(long userId, CancellationToken ct)
    {
        var cached = await _cacheService.GetUserAssignmentsAsync(userId);
        if (cached != null)
        {
            return cached;
        }

        var sw = Stopwatch.StartNew();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var userName = user?.Name ?? "Operator";

        var assignments = await _db.OperationalAssignments
            .AsNoTracking()
            .Where(a => a.AssignedToUserId == userId && a.Status == "Assigned")
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new OperationalAssignmentResponse(
                a.Id,
                a.EntityType,
                a.EntityId,
                a.EntityDocumentNumber,
                a.AssignedToUserId,
                a.AssignedToUser != null ? a.AssignedToUser.Name : userName,
                a.AssignedAt,
                a.CompletedByUserId,
                null,
                a.CompletedAt,
                a.Status,
                a.LastModifiedByUserId,
                a.LastModifiedByUser != null ? a.LastModifiedByUser.Name : userName,
                a.LastModifiedAt,
                a.Remarks
            ))
            .ToListAsync(ct);

        sw.Stop();
        _logger.LogInformation("Query database for operator assignments (userId: {UserId}) took {DurationMs}ms.", userId, sw.ElapsedMilliseconds);

        var summary = new MyAssignmentsSummaryResponse(assignments.Count, assignments);
        await _cacheService.SetUserAssignmentsAsync(userId, summary);
        return summary;
    }

    public async Task<IReadOnlyList<OperationalAssignmentResponse>> GetAllActiveAssignmentsAsync(CancellationToken ct)
    {
        var cached = await _cacheService.GetActiveAssignmentsAsync();
        if (cached != null)
        {
            return cached;
        }

        var sw = Stopwatch.StartNew();

        var assignments = await _db.OperationalAssignments
            .AsNoTracking()
            .Where(a => a.Status == "Assigned")
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new OperationalAssignmentResponse(
                a.Id,
                a.EntityType,
                a.EntityId,
                a.EntityDocumentNumber,
                a.AssignedToUserId,
                a.AssignedToUser != null ? a.AssignedToUser.Name : "Unknown",
                a.AssignedAt,
                a.CompletedByUserId,
                a.CompletedByUser != null ? a.CompletedByUser.Name : null,
                a.CompletedAt,
                a.Status,
                a.LastModifiedByUserId,
                a.LastModifiedByUser != null ? a.LastModifiedByUser.Name : "Unknown",
                a.LastModifiedAt,
                a.Remarks
            ))
            .ToListAsync(ct);

        sw.Stop();
        _logger.LogInformation("Query database for all active assignments took {DurationMs}ms.", sw.ElapsedMilliseconds);

        await _cacheService.SetActiveAssignmentsAsync(assignments);
        return assignments;
    }

    public async Task<IReadOnlyList<OperationalAssignmentHistoryResponse>> GetAssignmentHistoryAsync(long assignmentId, CancellationToken ct)
    {
        var histories = await _db.OperationalAssignmentHistories
            .AsNoTracking()
            .Include(h => h.PerformedByUser)
            .Include(h => h.OldAssignedUser)
            .Include(h => h.NewAssignedUser)
            .Where(h => h.AssignmentId == assignmentId)
            .OrderByDescending(h => h.PerformedAt)
            .ToListAsync(ct);

        return histories.Select(h => new OperationalAssignmentHistoryResponse(
            h.Id,
            h.AssignmentId,
            h.Action,
            h.PerformedByUserId,
            h.PerformedByUser?.Name ?? "Unknown",
            h.PerformedAt,
            h.OldAssignedUserId,
            h.OldAssignedUser?.Name,
            h.NewAssignedUserId,
            h.NewAssignedUser?.Name,
            h.Reason
        )).ToList();
    }

    public async Task<IReadOnlyList<OperationalAssignmentResponse>> GetActiveAssignmentsForEntitiesAsync(
        string entityType, IEnumerable<long> entityIds, CancellationToken ct)
    {
        var ids = entityIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<OperationalAssignmentResponse>();

        var typeLower = entityType.ToLower().Trim();
        var isVisa = typeLower.StartsWith("visa");
        var isVfs = typeLower.StartsWith("vfs");
        var isHoliday = typeLower.StartsWith("holiday");

        var assignments = await _db.OperationalAssignments
            .AsNoTracking()
            .Include(a => a.AssignedToUser)
            .Where(a => a.Status == "Assigned" && ids.Contains(a.EntityId) && (
                isVisa ? a.EntityType.ToLower().StartsWith("visa") :
                isVfs ? a.EntityType.ToLower().StartsWith("vfs") :
                isHoliday ? a.EntityType.ToLower().StartsWith("holiday") :
                a.EntityType.ToLower() == typeLower
            ))
            .ToListAsync(ct);

        return assignments.Select(a => MapToResponse(
            a,
            a.AssignedToUser?.Name ?? "Unknown",
            null,
            a.AssignedToUser?.Name ?? "Unknown"
        )).ToList();
    }

    private static OperationalAssignmentResponse MapToResponse(
        OperationalAssignment assignment,
        string assignedToUserName,
        string? completedByUserName,
        string lastModifiedByUserName)
    {
        return new OperationalAssignmentResponse(
            assignment.Id,
            assignment.EntityType,
            assignment.EntityId,
            assignment.EntityDocumentNumber,
            assignment.AssignedToUserId,
            assignedToUserName,
            assignment.AssignedAt,
            assignment.CompletedByUserId,
            completedByUserName,
            assignment.CompletedAt,
            assignment.Status,
            assignment.LastModifiedByUserId,
            lastModifiedByUserName,
            assignment.LastModifiedAt,
            assignment.Remarks
        );
    }

    private async Task InvalidateCacheAsync(params long[] userIds)
    {
        var sw = Stopwatch.StartNew();
        await _cacheService.InvalidateAllCacheAsync();
        foreach (var userId in userIds)
        {
            if (userId > 0)
            {
                await _cacheService.InvalidateUserCacheAsync(userId);
            }
        }
        sw.Stop();
        _logger.LogInformation("Cache invalidation completed in {DurationMs}ms.", sw.ElapsedMilliseconds);
    }

    public async Task<IReadOnlyList<OperationalAssignmentResponse>> GetCompletedAssignmentsAsync(long userId, CancellationToken ct)
    {
        var assignments = await _db.OperationalAssignments
            .AsNoTracking()
            .Where(a => a.Status == "Completed" && (a.CompletedByUserId == userId || a.AssignedToUserId == userId))
            .OrderByDescending(a => a.CompletedAt)
            .Select(a => new OperationalAssignmentResponse(
                a.Id,
                a.EntityType,
                a.EntityId,
                a.EntityDocumentNumber,
                a.AssignedToUserId,
                a.AssignedToUser != null ? a.AssignedToUser.Name : "Unknown",
                a.AssignedAt,
                a.CompletedByUserId,
                a.CompletedByUser != null ? a.CompletedByUser.Name : null,
                a.CompletedAt,
                a.Status,
                a.LastModifiedByUserId,
                a.LastModifiedByUser != null ? a.LastModifiedByUser.Name : "Unknown",
                a.LastModifiedAt,
                a.Remarks
            ))
            .ToListAsync(ct);

        return assignments;
    }

    public async Task<IReadOnlyList<WorkloadLimitResponse>> GetWorkloadLimitsAsync(CancellationToken ct)
    {
        var cached = await _cacheService.GetWorkloadLimitsAsync();
        if (cached != null)
        {
            return cached;
        }

        var limits = await _db.WorkloadLimits
            .AsNoTracking()
            .OrderBy(w => w.EntityType)
            .ThenBy(w => w.UserId)
            .Select(w => new WorkloadLimitResponse(
                w.Id,
                w.EntityType,
                w.MaxActiveLimit,
                w.UserId,
                w.UpdatedAt
            ))
            .ToListAsync(ct);

        if (!limits.Any())
        {
            _db.WorkloadLimits.AddRange(
                new WorkloadLimit { EntityType = "VISA", MaxActiveLimit = 3, UpdatedAt = DateTime.UtcNow },
                new WorkloadLimit { EntityType = "VFS", MaxActiveLimit = 3, UpdatedAt = DateTime.UtcNow },
                new WorkloadLimit { EntityType = "HOLIDAY", MaxActiveLimit = 3, UpdatedAt = DateTime.UtcNow }
            );
            await _db.SaveChangesAsync(ct);

            return await GetWorkloadLimitsAsync(ct);
        }

        await _cacheService.SetWorkloadLimitsAsync(limits);
        return limits;
    }

    public async Task<IReadOnlyList<WorkloadLimitResponse>> UpdateWorkloadLimitsAsync(
        UpdateWorkloadLimitsBatchRequest request, long performedByUserId, CancellationToken ct)
    {
        if (request?.Limits == null || !request.Limits.Any())
            throw new ArgumentException("Limits list cannot be empty.", nameof(request));

        foreach (var req in request.Limits)
        {
            var typeUpper = (req.EntityType ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(typeUpper)) continue;
            var maxVal = Math.Max(1, req.MaxActiveLimit);

            var existing = await _db.WorkloadLimits
                .FirstOrDefaultAsync(w => w.EntityType.ToUpper() == typeUpper && w.UserId == req.UserId, ct);

            if (existing != null)
            {
                existing.MaxActiveLimit = maxVal;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedByUserId = performedByUserId;
            }
            else
            {
                _db.WorkloadLimits.Add(new WorkloadLimit
                {
                    EntityType = typeUpper,
                    MaxActiveLimit = maxVal,
                    UserId = req.UserId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedByUserId = performedByUserId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await _cacheService.InvalidateWorkloadLimitsAsync();
        return await GetWorkloadLimitsAsync(ct);
    }

    public async Task<OperatorWorkloadSummaryResponse> GetOperatorWorkloadSummaryAsync(long userId, CancellationToken ct)
    {
        var limits = await GetWorkloadLimitsAsync(ct);

        var visaLimit = limits.FirstOrDefault(l => l.EntityType.ToUpper() == "VISA" && (l.UserId == userId || l.UserId == null))?.MaxActiveLimit ?? 3;
        var vfsLimit = limits.FirstOrDefault(l => l.EntityType.ToUpper() == "VFS" && (l.UserId == userId || l.UserId == null))?.MaxActiveLimit ?? 3;
        var holidayLimit = limits.FirstOrDefault(l => l.EntityType.ToUpper() == "HOLIDAY" && (l.UserId == userId || l.UserId == null))?.MaxActiveLimit ?? 3;

        var activeAssignments = await _db.OperationalAssignments
            .AsNoTracking()
            .Where(a => a.AssignedToUserId == userId && a.Status == "Assigned")
            .Select(a => a.EntityType.ToUpper())
            .ToListAsync(ct);

        var visaActive = activeAssignments.Count(t => t.Contains("VISA"));
        var vfsActive = activeAssignments.Count(t => t.Contains("VFS"));
        var holidayActive = activeAssignments.Count(t => t.Contains("HOLIDAY"));

        return new OperatorWorkloadSummaryResponse(
            VisaActiveCount: visaActive,
            VisaMaxLimit: visaLimit,
            VfsActiveCount: vfsActive,
            VfsMaxLimit: vfsLimit,
            HolidayActiveCount: holidayActive,
            HolidayMaxLimit: holidayLimit
        );
    }
}
