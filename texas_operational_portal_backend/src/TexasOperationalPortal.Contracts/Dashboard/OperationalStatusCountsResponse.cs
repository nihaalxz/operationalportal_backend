namespace TexasOperationalPortal.Contracts.Dashboard;

public sealed record OperationalStatusCountsResponse(
    int PendingVisasCount,
    int UnderReviewVisasCount,
    int PendingVfsCount,
    int UnderReviewVfsCount,
    int PendingHolidaysCount,
    int UnderReviewHolidaysCount
);
