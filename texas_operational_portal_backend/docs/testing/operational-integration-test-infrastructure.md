# Operational Integration Test Infrastructure

This document outlines the design and safeguards built into the integration testing framework for the Operational Portal backend.

---

## 1. Test Isolation & Lifecycles

Integration tests are executed against a real SQL Server database using `WebApplicationFactory<Program>` to ensure fidelity with SQL Server's transaction logic and constraints.

### Key Lifecycle Rules:
1. **Isolated Database**: Integration tests target a local test database named `TexasOperationalPortal_HTTP_Tests`.
2. **Setup and Cleanup**: The custom testing factory deletes, creates, and seeds the database before each test suite:
   - `EnsureDeletedAsync()` destroys any residual database.
   - `MigrateAsync()` provisions the schema from scratch.
   - `DbInitializer.SeedAsync()` seeds the baseline test data.
3. **Database Race Avoidance**: Tests run sequentially inside test classes via xUnit's default behaviors, avoiding concurrent migration/drop races on the same test database.

---

## 2. Production Database Safeguard

To prevent running integration tests against a live Production environment, the test factory contains connection string check assertions:

- **Check Rule**: The connection string **must** contain `TexasOperationalPortal_HTTP_Tests` or `localhost`, and **must not** contain production server addresses or credentials.
- **Action**: If a connection string is not recognized as a safe local test database, the test suite throws a critical setup exception and aborts immediately.

```csharp
if (!ConnectionString.Contains("TexasOperationalPortal_HTTP_Tests") && !ConnectionString.Contains("localhost"))
{
    throw new InvalidOperationException("CRITICAL: Integration tests are targeted at a non-test database! Aborting execution.");
}
```
This ensures zero risk of data loss or schema contamination on production databases.
