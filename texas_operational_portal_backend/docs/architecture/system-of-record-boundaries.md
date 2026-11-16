# System of Record Boundaries

This document defines the strict boundaries of data ownership between the Sales Portal and the Operational Portal.

---

## 1. Data Category Matrix

| Data Category | Authoritative System | Primary Readers | Primary Writers | Sync Direction | Consistency Model | Failure Mode |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Operational User Account** | Operational DB | Operational Portal | Operational Portal (Sync API) | Local DB | Strongly Consistent | Access blocked if DB is down. |
| **Staff Roles & Claims** | Operational DB | Operational Portal | Operational Portal | Local DB | Strongly Consistent | Actions fail if DB is offline. |
| **Sales Visa Booking** | Sales Portal | Operational Portal | Sales Portal | Sales $\rightarrow$ Operational (API) | Read-Through (On-Demand) | Read operations return error state. |
| **VFS Booking** | Sales Portal | Operational Portal | Sales Portal | Sales $\rightarrow$ Operational (API) | Read-Through (On-Demand) | Read operations return error state. |
| **Holiday Package Booking** | Sales Portal | Operational Portal | Sales Portal | Sales $\rightarrow$ Operational (API) | Read-Through (On-Demand) | Read operations return error state. |

---

## 2. Strict Architectural Constraints

1.  **No Direct Database Access**: Under no circumstances should the Operational Portal connect directly to the Sales Portal SQL Server instance. All queries must flow through the authenticated internal APIs.
2.  **No Local Copies of Booking Tables**: Do not create entities or tables in the Operational database for `VisaApplication`, `VfsAppointment`, or `HolidayPackage`. Bookings must be requested on-demand.
3.  **Data Isolation**: Only the operational metrics dashboard uses aggregated read calls. No salesman details, financial ledger entries, or customer wallets are ever exposed to the Operational frontend.
