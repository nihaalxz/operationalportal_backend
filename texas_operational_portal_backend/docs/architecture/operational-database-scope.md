# Operational Database Scope

This document specifies the database scope and schemas owned by the Operational Portal.

---

## 1. Entities & Schema Specification

The database utilizes SQL Server and is defined in EF Core using Code-First Migrations.

### `OperationalUsers` Table
*   `Id` (bigint, PK, Identity)
*   `Name` (nvarchar(100), Not Null)
*   `Email` (nvarchar(256), Not Null, Unique Index)
*   `PasswordHash` (nvarchar(max), Not Null)
*   `UserType` (nvarchar(50), Not Null) - Maps to Enum: `Admin`, `Operations`, `Accounts`
*   `IsActive` (bit, Not Null, Default: 1)
*   `PermissionsJson` (nvarchar(max), Null) - Stores JSON string of permissions (e.g. `["visas","vfs","holidays"]`)
*   `AvatarUrl` (nvarchar(500), Null)
*   `LastLoginAt` (datetime2, Null)
*   `LastLoginIp` (nvarchar(45), Null)
*   `CreatedAt` (datetime2, Not Null)
*   `UpdatedAt` (datetime2, Not Null)

### `RefreshTokens` Table
*   `Id` (uniqueidentifier, PK)
*   `UserId` (bigint, FK $\rightarrow$ `OperationalUsers.Id`, Cascade Delete)
*   `Token` (nvarchar(256), Not Null, Index)
*   `ExpiresAt` (datetime2, Not Null)
*   `IsRevoked` (bit, Not Null, Default: 0)
*   `CreatedAt` (datetime2, Not Null)

### `AuditLogs` Table
*   `Id` (bigint, PK, Identity)
*   `UserId` (bigint, Null)
*   `Action` (nvarchar(100), Not Null)
*   `Timestamp` (datetime2, Not Null)
*   `IpAddress` (nvarchar(45), Null)
*   `Details` (nvarchar(max), Null)

---

## 2. Constraints and Exclusions
*   No booking tables.
*   No customer profiles or salesman portfolios.
*   Cascade delete configured for `RefreshTokens` to clean up session states automatically upon user removal.
