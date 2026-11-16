using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Dashboard;
using TexasOperationalPortal.Domain.Common;

namespace TexasOperationalPortal.Application.Features.Dashboard;

public sealed class GetOperationalDashboardUseCase
{
    private readonly ISalesOperationsClient _salesClient;
    private readonly IOperationalDbContext _db;

    public GetOperationalDashboardUseCase(ISalesOperationsClient salesClient, IOperationalDbContext db)
    {
        _salesClient = salesClient;
        _db = db;
    }
    public async Task<DashboardSummaryResponse> ExecuteAsync(long currentUserId, CancellationToken ct)
    {
        // Use Bahrain calendar day boundaries (UTC+3) — NOT DateTime.UtcNow.Date which would
        // cut off at 21:00 Bahrain time (UTC midnight).
        var todayBahrainStartUtc = BahrainTime.ToUtc(BahrainTime.Today);
        var countsTask = _salesClient.GetOperationalStatusCountsAsync(ct);
        var assignmentsTask = _db.OperationalAssignments
            .AsNoTracking()
            .Where(a => (a.AssignedToUserId == currentUserId && a.Status == "Assigned") || 
                        ((a.CompletedByUserId == currentUserId || a.AssignedToUserId == currentUserId) && a.Status == "Completed" && a.CompletedAt >= todayBahrainStartUtc) ||
                        a.Status == "Assigned")
            .ToListAsync(ct);

        await Task.WhenAll(countsTask, assignmentsTask);

        var counts = countsTask.Result;
        var assignments = assignmentsTask.Result ?? new System.Collections.Generic.List<Domain.Entities.OperationalAssignment>();

        var totalActiveAssignments = assignments.Count(a => a.Status == "Assigned");
        var myActiveAssignments = assignments.Where(a => a.Status == "Assigned" && a.AssignedToUserId == currentUserId).ToList();

        var myVisaActive = myActiveAssignments.Count(a => a.EntityType.ToUpper().Contains("VISA"));
        var myVfsActive = myActiveAssignments.Count(a => a.EntityType.ToUpper().Contains("VFS"));
        var myHolidayActive = myActiveAssignments.Count(a => a.EntityType.ToUpper().Contains("HOLIDAY"));

        var completedTodayCount = assignments.Count(a => a.Status == "Completed" && a.CompletedAt >= todayBahrainStartUtc);

        var totalPendingFromSales = counts.PendingVisasCount + counts.PendingVfsCount + counts.PendingHolidaysCount;
        var unassignedCount = Math.Max(0, totalPendingFromSales - totalActiveAssignments);

        return new DashboardSummaryResponse(
            PendingVisasCount: counts.PendingVisasCount,
            UnderReviewVisasCount: myVisaActive,
            PendingVfsCount: counts.PendingVfsCount,
            ProcessingVfsCount: myVfsActive,
            PendingHolidaysCount: counts.PendingHolidaysCount,
            ProcessingHolidaysCount: myHolidayActive,
            PendingUnassignedCount: unassignedCount,
            MyOperationsCount: myActiveAssignments.Count,
            CompletedTodayCount: completedTodayCount
        );
    }
}
