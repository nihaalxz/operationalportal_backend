# Operational Feature Inventory

This inventory captures every legacy route, operational user role, permission check, external data source, write target, and migration decision for the Texas Operational Portal.

## 1. Feature Classifications

### Auth & Sync
*   **Feature**: Login Form / Authentication Handlers
    *   *Legacy Routes*: `GET /login`, `POST /login`, `POST /logout`
    *   *Users*: All Staff
    *   *Permission*: None (Public)
    *   *Reads*: Local DB + Remote login API fallback
    *   *Writes*: Local DB (`last_login_at`, local credentials creation)
    *   *Decision*: **MIGRATE**
*   **Feature**: Staff Sync Endpoint
    *   *Legacy Route*: `POST /api/sync-staff`
    *   *Users*: External System Client
    *   *Permission*: Key validation (`X-SYNC-KEY`)
    *   *Reads*: Local DB
    *   *Writes*: Local DB (Update Name, User Type, and Permissions)
    *   *Decision*: **MIGRATE**

### Dashboard & Metric Cards
*   **Feature**: Metrics Display (Counts of Pending, Under Review, Approved, Rejected)
    *   *Legacy Route*: `GET /admin/dashboard`
    *   *Users*: Operations, Admin
    *   *Permission*: None (Requires valid staff role login)
    *   *Reads*: Sales Portal API (`/sales-visa/applications`, `/vfs-appointments`, `/holiday-packages`)
    *   *Writes*: None
    *   *Decision*: **MIGRATE**

### Sales Visa Applications
*   **Feature**: Read-Only Sales Visas Queue
    *   *Legacy Route*: `GET /admin/sales-visa-applications`
    *   *Users*: Operations, Admin
    *   *Permission*: `visas` permission
    *   *Reads*: Sales Portal API (`/sales-visa/applications`)
    *   *Writes*: None
    *   *Decision*: **MIGRATE**
*   **Feature**: Sales Visa Details Page
    *   *Legacy Route*: `GET /admin/sales-visa-applications/{id}`
    *   *Users*: Operations, Admin
    *   *Permission*: `visas` permission
    *   *Reads*: Sales Portal API (`/sales-visa/applications/{id}`)
    *   *Writes*: None
    *   *Decision*: **MIGRATE**

### VFS Appointments
*   **Feature**: Read-Only VFS Appointments Queue
    *   *Legacy Route*: `GET /admin/vfs-appointments`
    *   *Users*: Operations, Admin
    *   *Permission*: `vfs` permission
    *   *Reads*: Sales Portal API (`/vfs-appointments`)
    *   *Writes*: None
    *   *Decision*: **MIGRATE**

### Holiday Packages
*   **Feature**: Read-Only Holiday Packages Queue
    *   *Legacy Route*: `GET /admin/holiday-packages`
    *   *Users*: Operations, Admin
    *   *Permission*: `holidays` permission
    *   *Reads*: Sales Portal API (`/holiday-packages`)
    *   *Writes*: None
    *   *Decision*: **MIGRATE**

## 2. Deferred & Removed Features
*   *Sales Visa Mutations (Process, Approve, Reject)*: **DEFER** to Phase 2.
*   *VFS Appointments Status Updates (Process, Update Ref, Complete)*: **DEFER** to Phase 2.
*   *Holiday Packages Status Updates (Process, Update App ID, Documents Upload)*: **DEFER** to Phase 2.
*   *Texas Connect Core Visas (`/admin/visa-applications`)*: **DEFER/REMOVE** (Core portal integration is out of scope).
