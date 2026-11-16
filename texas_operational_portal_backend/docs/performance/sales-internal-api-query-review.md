# Sales Internal API Query Review

This document reviews the performance, query patterns, and resource usage of the internal EF Core queries in the Sales Portal.

---

## 1. Query Patterns Review

All query paths in `OperationsReadService.cs` were reviewed against standard database performance criteria:

- **AsNoTracking Usage**:
  - **Verdict**: **PASS**. Every query explicitly calls `.AsNoTracking()`. This disables EF entity tracking, saving CPU cycles and memory.
- **Direct DTO Projection**:
  - **Verdict**: **PASS**. Queries project database fields directly using `.Select(...)` rather than fetching entities and mapping them in-memory. This prevents over-fetching and reduces memory allocations.
- **No N+1 Queries**:
  - **Verdict**: **PASS**. Collections (like `x.Applicants`) are projected directly inside the `.Select(...)` mapping block, which translates to a single efficient JOIN query at database level rather than triggering N separate calls.
- **No Unnecessary Includes**:
  - **Verdict**: **PASS**. `.Include(...)` is not used in the read service because projections populate child objects inline, avoiding unnecessary duplicate loads.
- **Bounded Pagination**:
  - **Verdict**: **PASS**. `InternalOperationsController` checks and clamps page parameters: `if (page < 1 || pageSize is < 1 or > 100) return BadRequest(...)`.
- **CancellationToken Propagation**:
  - **Verdict**: **PASS**. All async methods (`CountAsync`, `ToListAsync`, `FirstOrDefaultAsync`) accept and respect the `CancellationToken` parameter.
- **Sorting Performance**:
  - **Verdict**: **PASS**. Results are sorted using `OrderByDescending(x => x.CreatedAt)`. An index on `CreatedAt` in the booking tables will ensure $O(\log N)$ performance.
