# Operational Read Query Review

This document outlines the performance optimizations applied to database and internal API queries to ensure rapid dashboard loads and list updates.

---

## 1. EF Core Query Optimizations

To maximize query speed on both Sales and Operational backends:
1.  **Strict Use of `AsNoTracking()`**: Applied to all read-only dashboard and search queries. Prevents EF Core change tracker overhead.
2.  **Explicit DTO Projections**: Always project collections using `.Select(...)` directly in the database server, fetching only columns needed by the contract. Do not return full entity trees.
3.  **Strict Avoidance of N+1 Queries**: Never lazy-load child collections inside loops. If nested resources are needed (such as applicants inside a visa booking details query), fetch them using explicit `Include` statements or perform combined projection.
4.  **Pagination Limits**: Force boundaries on `pageSize` (max: 100) to protect database resources.

---

## 2. Service Call Optimizations (Dashboard)

*   **Fanning Out Async Calls**: The Operational dashboard requires statistics from three Sales endpoints. The backend uses `Task.WhenAll` to execute these API calls concurrently.
*   **Cancellation Propagation**: Pass `CancellationToken` to all EF Core database calls and typed HttpClient requests, allowing immediate cleanup if client connections disconnect.
