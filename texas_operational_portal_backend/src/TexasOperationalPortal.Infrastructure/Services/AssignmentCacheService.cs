using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Assignments;

namespace TexasOperationalPortal.Infrastructure.Services;

public sealed class AssignmentCacheService : IAssignmentCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<AssignmentCacheService> _logger;

    private const string AllAssignmentsKey = "assignments:all";
    private const string WorkloadLimitsKey = "workload:limits:all";
    private static string UserAssignmentsKey(long userId) => $"assignments:user:{userId}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CacheTtlWorkloadLimits = TimeSpan.FromMinutes(5);

    public AssignmentCacheService(IMemoryCache cache, ILogger<AssignmentCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<IReadOnlyList<OperationalAssignmentResponse>?> GetActiveAssignmentsAsync()
    {
        if (_cache.TryGetValue<IReadOnlyList<OperationalAssignmentResponse>>(AllAssignmentsKey, out var assignments))
        {
            _logger.LogInformation("Cache HIT for key: {CacheKey}. Returned {Count} assignments.", AllAssignmentsKey, assignments?.Count ?? 0);
            return Task.FromResult(assignments);
        }

        _logger.LogInformation("Cache MISS for key: {CacheKey}.", AllAssignmentsKey);
        return Task.FromResult<IReadOnlyList<OperationalAssignmentResponse>?>(null);
    }

    public Task SetActiveAssignmentsAsync(IReadOnlyList<OperationalAssignmentResponse> assignments)
    {
        _cache.Set(AllAssignmentsKey, assignments, CacheTtl);
        _logger.LogInformation("Cache SET for key: {CacheKey} with TTL {Ttl}s. Stored {Count} assignments.", AllAssignmentsKey, CacheTtl.TotalSeconds, assignments.Count);
        return Task.CompletedTask;
    }

    public Task<MyAssignmentsSummaryResponse?> GetUserAssignmentsAsync(long userId)
    {
        var key = UserAssignmentsKey(userId);
        if (_cache.TryGetValue<MyAssignmentsSummaryResponse>(key, out var summary))
        {
            _logger.LogInformation("Cache HIT for key: {CacheKey}. Returned {Count} user assignments.", key, summary?.TotalActiveCount ?? 0);
            return Task.FromResult(summary);
        }

        _logger.LogInformation("Cache MISS for key: {CacheKey}.", key);
        return Task.FromResult<MyAssignmentsSummaryResponse?>(null);
    }

    public Task SetUserAssignmentsAsync(long userId, MyAssignmentsSummaryResponse summary)
    {
        var key = UserAssignmentsKey(userId);
        _cache.Set(key, summary, CacheTtl);
        _logger.LogInformation("Cache SET for key: {CacheKey} with TTL {Ttl}s. Stored {Count} user assignments.", key, CacheTtl.TotalSeconds, summary.TotalActiveCount);
        return Task.CompletedTask;
    }

    public Task InvalidateAllCacheAsync()
    {
        _cache.Remove(AllAssignmentsKey);
        _logger.LogInformation("Cache INVALIDATED for key: {CacheKey}.", AllAssignmentsKey);
        return Task.CompletedTask;
    }

    public Task InvalidateUserCacheAsync(long userId)
    {
        var key = UserAssignmentsKey(userId);
        _cache.Remove(key);
        _logger.LogInformation("Cache INVALIDATED for key: {CacheKey}.", key);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkloadLimitResponse>?> GetWorkloadLimitsAsync()
    {
        if (_cache.TryGetValue<IReadOnlyList<WorkloadLimitResponse>>(WorkloadLimitsKey, out var limits))
        {
            _logger.LogInformation("Cache HIT for key: {CacheKey}. Returned {Count} workload limits.", WorkloadLimitsKey, limits?.Count ?? 0);
            return Task.FromResult(limits);
        }

        _logger.LogInformation("Cache MISS for key: {CacheKey}.", WorkloadLimitsKey);
        return Task.FromResult<IReadOnlyList<WorkloadLimitResponse>?>(null);
    }

    public Task SetWorkloadLimitsAsync(IReadOnlyList<WorkloadLimitResponse> limits)
    {
        _cache.Set(WorkloadLimitsKey, limits, CacheTtlWorkloadLimits);
        _logger.LogInformation("Cache SET for key: {CacheKey} with TTL {Ttl}s. Stored {Count} workload limits.", WorkloadLimitsKey, CacheTtlWorkloadLimits.TotalSeconds, limits.Count);
        return Task.CompletedTask;
    }

    public Task InvalidateWorkloadLimitsAsync()
    {
        _cache.Remove(WorkloadLimitsKey);
        _logger.LogInformation("Cache INVALIDATED for key: {CacheKey}.", WorkloadLimitsKey);
        return Task.CompletedTask;
    }
}
