# Operational Dashboard Failure Semantics

This document records the design decision regarding how failures are handled when querying parallel downstream metrics for the Operational Dashboard.

---

## 1. Context

The Operational Dashboard summarizes pending and active queues by fanning out six parallel requests to the Sales Portal's internal APIs:
- Visa (Pending, Under Review)
- VFS Appointments (Pending, Processing)
- Holiday Packages (Pending, Under Review)

These requests execute concurrently using `Task.WhenAll`.

---

## 2. Decision: FAIL ALL Policy

We will enforce a strict **FAIL ALL** policy:
- If **any** of the six parallel downstream requests fail (due to connection failure, timeout, rate limits, or bad response), the dashboard request **must fail entirely**.
- The endpoint will return a **503 Service Unavailable** (or appropriate mapped status code), indicating that booking metrics cannot be fully loaded.

### Rationale:
1. **Prevent Misleading Statistics**: Returning zero counts or partial counts for a failed service (e.g. displaying 0 pending visas because that specific call failed, while displaying 5 holidays) is highly misleading to operations managers.
2. **Data Integrity**: Managers make resource allocation decisions based on dashboard metrics. Partial or incorrect counts are worse than an explicit, safe error state.
3. **Simplicity**: Eliminates complex partial warning tracking in SvelteKit state management.

---

## 3. Parallel Async/Await Implementation

We will ensure proper cancellation token propagation so that if one task fails, the other pending HTTP requests are cancelled immediately to prevent resource waste (using standard cancellation token links if necessary, or propagating the request's token).

```csharp
public async Task<DashboardSummaryResponse> ExecuteAsync(CancellationToken ct)
{
    var pendingVisasTask = _salesClient.GetVisaApplicationsAsync(1, 1, "pending", null, ct);
    var underReviewVisasTask = _salesClient.GetVisaApplicationsAsync(1, 1, "under_review", null, ct);
    var pendingVfsTask = _salesClient.GetVfsAppointmentsAsync(1, 1, "pending", null, ct);
    var processingVfsTask = _salesClient.GetVfsAppointmentsAsync(1, 1, "under_review", null, ct);
    var pendingHolidaysTask = _salesClient.GetHolidayPackagesAsync(1, 1, "pending", null, ct);
    var processingHolidaysTask = _salesClient.GetHolidayPackagesAsync(1, 1, "under_review", null, ct);

    // Propagated exception will be the first one that failed, bubble up to DownstreamExceptionFilter
    await Task.WhenAll(
        pendingVisasTask,
        underReviewVisasTask,
        pendingVfsTask,
        processingVfsTask,
        pendingHolidaysTask,
        processingHolidaysTask);

    return new DashboardSummaryResponse(
        PendingVisasCount: (await pendingVisasTask).TotalCount,
        UnderReviewVisasCount: (await underReviewVisasTask).TotalCount,
        PendingVfsCount: (await pendingVfsTask).TotalCount,
        ProcessingVfsCount: (await processingVfsTask).TotalCount,
        PendingHolidaysCount: (await pendingHolidaysTask).TotalCount,
        ProcessingHolidaysCount: (await processingHolidaysTask).TotalCount
    );
}
```
This ensures high efficiency while executing parallel network queries and enforces a safe failure state.
