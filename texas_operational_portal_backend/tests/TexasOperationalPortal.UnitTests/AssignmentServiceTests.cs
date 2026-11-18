using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TexasOperationalPortal.Application.Common.Exceptions;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Features.Assignments;
using TexasOperationalPortal.Contracts.Assignments;
using TexasOperationalPortal.Domain.Entities;
using TexasOperationalPortal.Infrastructure.Persistence;
using Xunit;

namespace TexasOperationalPortal.UnitTests;

public class DummyAssignmentCacheService : IAssignmentCacheService
{
    public Task<IReadOnlyList<OperationalAssignmentResponse>?> GetActiveAssignmentsAsync() => Task.FromResult<IReadOnlyList<OperationalAssignmentResponse>?>(null);
    public Task SetActiveAssignmentsAsync(IReadOnlyList<OperationalAssignmentResponse> assignments) => Task.CompletedTask;
    public Task<MyAssignmentsSummaryResponse?> GetUserAssignmentsAsync(long userId) => Task.FromResult<MyAssignmentsSummaryResponse?>(null);
    public Task SetUserAssignmentsAsync(long userId, MyAssignmentsSummaryResponse summary) => Task.CompletedTask;
    public Task InvalidateAllCacheAsync() => Task.CompletedTask;
    public Task InvalidateUserCacheAsync(long userId) => Task.CompletedTask;
    public Task<IReadOnlyList<WorkloadLimitResponse>?> GetWorkloadLimitsAsync() => Task.FromResult<IReadOnlyList<WorkloadLimitResponse>?>(null);
    public Task SetWorkloadLimitsAsync(IReadOnlyList<WorkloadLimitResponse> limits) => Task.CompletedTask;
    public Task InvalidateWorkloadLimitsAsync() => Task.CompletedTask;
}

public class DummyLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class AssignmentServiceTests
{
    private OperationalDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<OperationalDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var db = new OperationalDbContext(options);
        return db;
    }

    [Fact]
    public async Task ClaimOperationAsync_Should_Create_Assignment_And_History_When_Unassigned()
    {
        // Arrange
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var user = new OperationalUser
        {
            Id = 1,
            Name = "Operator John",
            Email = "john@texas.com",
            PasswordHash = "hash",
            UserType = UserType.Operations
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        var request = new ClaimAssignmentRequest("Visa", 101, "TXSVISA101", "Claiming for review");

        // Act
        var response = await service.ClaimOperationAsync(request, 1, default);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Visa", response.EntityType);
        Assert.Equal(101, response.EntityId);
        Assert.Equal("TXSVISA101", response.EntityDocumentNumber);
        Assert.Equal(1, response.AssignedToUserId);
        Assert.Equal("Operator John", response.AssignedToUserName);
        Assert.Equal("Assigned", response.Status);

        var history = await db.OperationalAssignmentHistories.FirstOrDefaultAsync();
        Assert.NotNull(history);
        Assert.Equal("Claimed", history.Action);
        Assert.Equal(1, history.PerformedByUserId);
    }

    [Fact]
    public async Task ClaimOperationAsync_Should_Throw_AssignmentConflictException_When_Already_Assigned()
    {
        // Arrange
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var user1 = new OperationalUser { Id = 1, Name = "Op 1", Email = "op1@texas.com", PasswordHash = "h", UserType = UserType.Operations };
        var user2 = new OperationalUser { Id = 2, Name = "Op 2", Email = "op2@texas.com", PasswordHash = "h", UserType = UserType.Operations };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        var request = new ClaimAssignmentRequest("Visa", 200, "TXSVISA200", null);

        // First operator claims
        await service.ClaimOperationAsync(request, 1, default);

        // Act & Assert (Second operator attempts to claim)
        await Assert.ThrowsAsync<AssignmentConflictException>(async () =>
        {
            await service.ClaimOperationAsync(request, 2, default);
        });
    }

    [Fact]
    public async Task ReleaseOperationAsync_Should_Release_Assignment()
    {
        // Arrange
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var user = new OperationalUser { Id = 1, Name = "Op 1", Email = "op1@texas.com", PasswordHash = "h", UserType = UserType.Operations };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        var claimRes = await service.ClaimOperationAsync(new ClaimAssignmentRequest("VFS", 300, "TXSVFS300", null), 1, default);

        // Act
        var releaseRes = await service.ReleaseOperationAsync(claimRes.Id, "End of shift", 1, false, default);

        // Assert
        Assert.Equal("Released", releaseRes.Status);
        var activeCount = await db.OperationalAssignments.CountAsync(a => a.Status == "Assigned");
        Assert.Equal(0, activeCount);
    }

    [Fact]
    public async Task ReassignOperationAsync_Should_Reassign_User_When_Admin()
    {
        // Arrange
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var admin = new OperationalUser { Id = 1, Name = "Admin User", Email = "admin@texas.com", PasswordHash = "h", UserType = UserType.Admin };
        var op1 = new OperationalUser { Id = 2, Name = "Op 1", Email = "op1@texas.com", PasswordHash = "h", UserType = UserType.Operations };
        var op2 = new OperationalUser { Id = 3, Name = "Op 2", Email = "op2@texas.com", PasswordHash = "h", UserType = UserType.Operations };
        db.Users.AddRange(admin, op1, op2);
        await db.SaveChangesAsync();

        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        var claimRes = await service.ClaimOperationAsync(new ClaimAssignmentRequest("Holiday", 400, "TXSHOL400", null), 2, default);

        // Act
        var reassignRes = await service.ReassignOperationAsync(claimRes.Id, 3, "Work balance", 1, true, default);

        // Assert
        Assert.Equal(3, reassignRes.AssignedToUserId);
        Assert.Equal("Op 2", reassignRes.AssignedToUserName);
    }

    [Fact]
    public async Task CompleteOperationAsync_Should_Mark_Assignment_As_Completed()
    {
        // Arrange
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var op = new OperationalUser { Id = 1, Name = "Op 1", Email = "op1@texas.com", PasswordHash = "h", UserType = UserType.Operations };
        db.Users.Add(op);
        await db.SaveChangesAsync();

        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        await service.ClaimOperationAsync(new ClaimAssignmentRequest("Visa", 500, "TXSVISA500", null), 1, default);

        // Act
        await service.CompleteOperationAsync("Visa", 500, 1, default);

        // Assert
        var assignment = await db.OperationalAssignments.FirstOrDefaultAsync(a => a.EntityType == "Visa" && a.EntityId == 500);
        Assert.NotNull(assignment);
        Assert.Equal("Completed", assignment.Status);
        Assert.Equal(1, assignment.CompletedByUserId);
        Assert.NotNull(assignment.CompletedAt);
    }

    [Fact]
    public async Task ClaimOperationAsync_Should_Allow_EntityId_Zero_When_Legitimate()
    {
        // Arrange
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var user = new OperationalUser
        {
            Id = 1,
            Name = "Operator John",
            Email = "john@texas.com",
            PasswordHash = "hash",
            UserType = UserType.Operations
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        var request = new ClaimAssignmentRequest("VFS", 0, "TXSVFS069785", "Claiming VFS 0");

        // Act
        var response = await service.ClaimOperationAsync(request, 1, default);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("VFS", response.EntityType);
        Assert.Equal(0, response.EntityId);
        Assert.Equal("TXSVFS069785", response.EntityDocumentNumber);
        Assert.Equal("Assigned", response.Status);
    }

    [Fact]
    public async Task ClaimOperationAsync_Should_Throw_When_EntityId_Negative()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new AssignmentService(db, new DummyAssignmentCacheService(), new DummyLogger<AssignmentService>());
        var request = new ClaimAssignmentRequest("VFS", -1, "TXSVFS069785", null);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await service.ClaimOperationAsync(request, 1, default);
        });
    }
}
