# Operational Database Isolation Verification

This document verifies the database isolation boundaries between the Sales Portal and the Operational Portal.

---

## 1. Boundary Rules & Audited Assets

To maintain database separation, the following isolation constraints are verified:

1. **Operational DbContext Scope**:
   - `OperationalDbContext.cs` only exposes `Users`, `RefreshTokens`, and `AuditLogs` DbSets.
   - It contains no references to `VisaApplication`, `VfsAppointment`, or `HolidayPackage` database tables.
2. **Operational Domain Models**:
   - The domain models include `OperationalUser`, `RefreshToken`, and `AuditLog`.
   - There are no entity classes representing Sales Portal bookings.
3. **No Direct References**:
   - `TexasOperationalPortal.Infrastructure` does not reference `TexasPortal.Infrastructure` or `TexasPortal.Application`.
   - The projects are compiled in completely separate solutions.
4. **Data Access Layer**:
   - The Operational backend reads booking data strictly on-demand by calling the transitional API endpoints on the Sales Portal via `ISalesOperationsClient` (`SalesOperationsClient`).
   - No direct SQL queries, cross-database links, or shared database credentials are used.

---

## 2. Automated Architecture Assertions

In `ArchitectureTests.cs`, we enforce:
- Domain layer must not reference any other layer.
- Application layer must not reference Infrastructure or Api layers.
- Infrastructure layer must not reference Api layer.
- Api controllers must not reference DbContext directly (queries must go through Application features).
