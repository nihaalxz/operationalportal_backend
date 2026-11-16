# Legacy Operational Portal Analysis

This document details the audit of the legacy Laravel Operational Portal, mapping its endpoints, backend structures, data access policies, and key areas of architectural weakness to be resolved in the .NET 9 migration.

---

## 1. Routing and Controller Mapping

### Public / Authentication Routes
*   `GET /login` $\rightarrow$ `LoginController@showLoginForm`
*   `POST /login` $\rightarrow$ `LoginController@login`
*   `POST /logout` $\rightarrow$ `LoginController@logout`
*   `GET/POST /password/reset` $\rightarrow$ Laravel built-in auth reset controllers

### Dashboard Route
*   `GET /admin/dashboard` $\rightarrow$ `OperationsDashboardController@index`
    *   **Authorization**: `auth` and `role` middleware.
    *   **Behavior**: Aggregate counts and lists from the Sales Portal API.

### Sales Visa Applications
*   `GET /admin/sales-visa-applications` $\rightarrow$ `SalesVisaController@index`
    *   **Reads**: Fetch all visa applications from the Sales Portal API. Filters locally.
*   `GET /admin/sales-visa-applications/{id}` $\rightarrow$ `SalesVisaController@show`
    *   **Reads**: Fetch specific application details from the Sales Portal API.
*   `POST /admin/sales-visa-applications/{id}/process` $\rightarrow$ `SalesVisaController@process`
    *   **Writes**: Marks application as "Under Review" on Sales Portal. (Phase 2)
*   `POST /admin/sales-visa-applications/{id}/approve` $\rightarrow$ `SalesVisaController@approve`
    *   **Writes**: Approves and attaches visa documents to Sales Portal. (Phase 2)
*   `POST /admin/sales-visa-applications/{id}/reject` $\rightarrow$ `SalesVisaController@reject`
    *   **Writes**: Rejects and attaches rejection reason to Sales Portal. (Phase 2)

### VFS Appointments
*   `GET /admin/vfs-appointments` $\rightarrow$ `VfsAppointmentController@index`
    *   **Reads**: Lists VFS appointments from Sales Portal.
*   `GET /admin/vfs-appointments/{id}` $\rightarrow$ `VfsAppointmentController@show`
    *   **Reads**: Detail page for appointment.
*   `POST /admin/vfs-appointments/{id}/status` $\rightarrow$ `VfsAppointmentController@updateStatus`
    *   **Writes**: Updates status to completed/cancelled on Sales Portal. (Phase 2)

### Holiday Packages
*   `GET /admin/holiday-packages` $\rightarrow$ `HolidayPackageController@index`
    *   **Reads**: Lists Holiday packages from Sales Portal.
*   `GET /admin/holiday-packages/{id}` $\rightarrow$ `HolidayPackageController@show`
    *   **Reads**: Detail page for package.
*   `POST /admin/holiday-packages/{id}/status` $\rightarrow$ `HolidayPackageController@updateStatus`
    *   **Writes**: Completes/cancels package on Sales Portal. (Phase 2)

---

## 2. Weaknesses of Legacy Implementation & .NET Improvements

1.  **Direct Sales Portal Coupling**:
    *   *Weakness*: The legacy backend did not check or validate downstream response contracts, leading to potential PHP exceptions if fields were missing.
    *   *Improvement*: Map Sales response contracts using strict type-safe DTOs in C# with robust null checks.
2.  **Shared Global Key Authentication**:
    *   *Weakness*: The use of a static header `X-OPS-KEY` represents a high-risk security vector.
    *   *Improvement*: Implement short-lived signature hashing or client JWT authentication.
3.  **Local Credentials Caching**:
    *   *Weakness*: Remote credentials check creates a copy of the password hash locally, which may get stale.
    *   *Improvement*: Maintain clear account synchronization patterns with token lifetimes, and block deactivated accounts immediately.
