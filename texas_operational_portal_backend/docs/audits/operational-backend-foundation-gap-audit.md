# Gap Audit - Operational Backend Foundation

This document provides a gap audit of all Phase 1 backend requirements, classifying their implementation, test coverages, and missing work.

## Gap Matrix

| Requirement | Implementation Evidence | Test Evidence | Execution Evidence | Status | Missing Work |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Legacy Analysis & Context** | [laravel-to-dotnet-map.md](file:///d:/freelanceprojects/TexasPortalsDotNet/texas_sales_portal_backend/texas_sales_portal_backend/docs/migration/laravel-to-dotnet-map.md) | None | Documentation only | **PASS** | None |
| **Feature Inventory & Boundaries** | [system-of-record-boundaries.md](file:///d:/freelanceprojects/TexasPortalsDotNet/texas_operational_portal_backend/texas_operational_portal_backend/docs/architecture/system-of-record-boundaries.md) | None | Documentation only | **PASS** | None |
| **Database Scope & Decoupling** | [operational-database-scope.md](file:///d:/freelanceprojects/TexasPortalsDotNet/texas_operational_portal_backend/texas_operational_portal_backend/docs/architecture/operational-database-scope.md) | Architecture tests | `dotnet test` (ArchitectureTests) | **PASS** | None |
| **Authentication Decision** | [operational-authentication-decision.md](file:///d:/freelanceprojects/TexasPortalsDotNet/texas_operational_portal_backend/texas_operational_portal_backend/docs/architecture/operational-authentication-decision.md) | None | Documentation only | **PASS** | None |
| **Authorization & Permission Model** | JWT Role/Permission claims inside `TokenService.cs` | Claims validation tests in UnitTests | `dotnet test` (UnitTests) | **FAIL** | Policies must be defined in `Program.cs`, checked in database for deactivations, and applied to API endpoints |
| **Sales Service Authentication** | `InternalOperationsAuthAttribute.cs` on Sales API | `InternalOperationsTests.cs` on Sales API | `dotnet test` (Sales integration tests) | **FAIL** | Move transitional `X-OPS-KEY` auth logic to a custom Authentication Handler; externalize secrets from source control |
| **Sales Internal APIs (Visas, VFS, Holiday)** | `InternalOperationsController.cs` in Sales API | `InternalOperationsTests.cs` | `dotnet test` (Sales integration tests) | **FAIL** | Bounded pagination, sorting allowlists, and direct DTO projections without unnecessary includes |
| **Operational Database** | EF Core `OperationalDbContext.cs` & `InitialCreate` migration | Integration tests run against local DB | `dotnet test` (IntegrationTests) | **PASS** | Refine model to include `IsUsed` for refresh token reuse tracking |
| **Authentication Lifecycle (Login)** | `LoginUseCase.cs` & `/api/auth/login` | LocalLogin tests in `LoginAndDashboardTests` | `dotnet test` (IntegrationTests) | **FAIL** | Missing `/api/auth/refresh`, `/api/auth/logout`, `/api/auth/me`, login rate limiting, and audit logging |
| **Refresh Token Rotation & Reuse** | Basic insert of token on Login | None | None | **FAIL** | Implement rotation, hashing, expiration checks, and reuse revocation detection |
| **Logout & Revocation** | None | None | None | **FAIL** | Implement `/api/auth/logout` revoking tokens |
| **Disabled Users Enforcement** | Check `IsActive` in `LoginUseCase.cs` | None | None | **FAIL** | Ensure active status check occurs on every request (action filter or middleware) to block compromised active tokens |
| **Typed Sales Client Hardening** | `SalesOperationsClient.cs` | Mock client tests in `LoginAndDashboardTests` | `dotnet test` (IntegrationTests) | **FAIL** | Missing retry handling on safe GETs, timeout option config, correlation ID propagation, and custom ProblemDetails mapping |
| **Downstream Failure Semantics** | None | None | None | **FAIL** | Explicitly define downstream exception mappings and build exception filter returning ProblemDetails |
| **Dashboard Failure Semantics** | `Task.WhenAll` fanning out requests | None | None | **FAIL** | Usecase uses `.Result` which must be replaced; handle task failures cleanly according to a FAIL ALL policy |
| **Visa/VFS/Holiday Proxy Queues** | Proxy handlers in controllers and use cases | None | None | **FAIL** | Map permissions to proxy actions and add integration tests |
| **Async/Await & CancellationTokens** | Async methods used across layers | None | None | **FAIL** | Usecase calls `.Result`. Propagated cancellation tokens must flow completely down to HTTP and DB requests |
| **Operational Database Isolation** | No cross-database references or models | Integration tests | `dotnet test` (IntegrationTests) | **PASS** | None |
| **Integration Test Infrastructure** | Uses `OperationalApiTestFactory` with local database | xUnit tests | `dotnet test` | **FAIL** | Verify connection string safety (cannot target Production) and prevent parallel database drop/migrate race conditions |
| **Default Production Admin Credentials** | Seeds admin credentials in `DbInitializer.cs` | None | None | **FAIL** | Seeding must be disabled in Production or sourced safely from environment variables; fail-safe startup |
| **Security Review & Secret Handling** | Keys hardcoded as default fallbacks | None | None | **FAIL** | Centralize options validation, validate secrets length and URIs on start, and log auth actions securely |
| **Frontend Readiness** | JWT auth and proxy controllers set up | None | None | **FAIL** | SvelteKit API client and layouts cannot begin until backend authentication lifecycle is fully hardened |

## Recommendations
A systematic execution plan targeting these gaps will secure the application's boundaries, improve operational security, and ensure frontend integration works flawlessly.
