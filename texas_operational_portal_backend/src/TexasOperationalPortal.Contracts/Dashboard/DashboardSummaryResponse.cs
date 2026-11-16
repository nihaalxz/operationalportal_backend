namespace TexasOperationalPortal.Contracts.Dashboard;

public sealed record DashboardSummaryResponse(
    int PendingVisasCount,
    int UnderReviewVisasCount,
    int PendingVfsCount,
    int ProcessingVfsCount,
    int PendingHolidaysCount,
    int ProcessingHolidaysCount,
    int PendingUnassignedCount,
    int MyOperationsCount,
    int CompletedTodayCount
);
