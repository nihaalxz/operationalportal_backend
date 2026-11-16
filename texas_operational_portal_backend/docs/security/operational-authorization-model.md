# Operational Authorization Model

This document maps the user types and claims to specific operational endpoints and UI visibility behaviors.

---

## 1. Roles and Permission Assignments

The Operational Portal maps users to one of three roles (`Admin`, `Operations`, `Accounts`), which are stored as claims. User types contain individual module permission arrays (stored in the database and serialized to JWT tokens):

| Permission Claim | Admin | Operations | Accounts | Scope of Access |
| :--- | :---: | :---: | :---: | :--- |
| `visas` | Yes | Yes (If configured) | No | View lists & details for Sales Visa Applications. |
| `vfs` | Yes | Yes (If configured) | No | View lists & details for VFS Appointments. |
| `holidays` | Yes | Yes (If configured) | No | View lists & details for Holiday Packages. |
| `admin` | Yes | No | No | Access to user lists, permission updates, audit logs. |

---

## 2. Policy Definitions (.NET Web API)

The backend implements policy-based authorization using ASP.NET Core:
1.  **`OperationalPortalAccess`**: Requires authentication and checking that the user is not deactivated (`IsActive` claim check).
2.  **`CanViewVisas`**: User must have role `Admin` OR have role `Operations` and possess the `visas` permission claim.
3.  **`CanViewVfs`**: User must have role `Admin` OR have role `Operations` and possess the `vfs` permission claim.
4.  **`CanViewHolidays`**: User must have role `Admin` OR have role `Operations` and possess the `holidays` permission claim.
5.  **`CanManageOperationalUsers`**: User must have role `Admin`.

---

## 3. Frontend Route Enforcement (SvelteKit)

*   Route guards check Svelte store state containing claims on initial page loads and transitions.
*   Action buttons (e.g. details navigation) are conditionally rendered depending on claims.
*   *Security Principle*: The frontend views are designed for user experience; authorization must always be verified on the server-side endpoints.
