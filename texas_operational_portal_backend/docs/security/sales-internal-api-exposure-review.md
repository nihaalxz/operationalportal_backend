# Sales Internal API Exposure Review

This document reviews the data exposure of all six Sales internal endpoints.

---

## 1. Exposed Fields Audit

We audited the fields serialized by the internal read controllers on the Sales Portal:
- `GET /api/internal/operations/visas`
- `GET /api/internal/operations/visas/{id}`
- `GET /api/internal/operations/vfs-appointments`
- `GET /api/internal/operations/vfs-appointments/{id}`
- `GET /api/internal/operations/holiday-packages`
- `GET /api/internal/operations/holiday-packages/{id}`

### Data Classifications & Findings

| Area | Field Name | Status | Review & Recommendation |
| :--- | :--- | :--- | :--- |
| **Sensitive Financials** | Salesman Salary | **SAFE** | Not exposed in any query or projection. |
| **Sensitive Financials** | Head Wallet / Balance | **SAFE** | Not exposed in any query or projection. |
| **Sensitive Financials** | Head Ledger / Ledger Transactions | **SAFE** | Not exposed in any query or projection. |
| **Downstream URLs** | VfsReceipt / PassportCopy / RejectionDocument / PaymentProof | **WARN** | Currently exposes file path/name strings (e.g. `p.PaymentProof`). *Action*: In the proxy layer (Operational Portal), map these to local download proxy URLs. Never expose internal paths directly to frontend clients. |
| **Booking Profit** | `Profit` (payable - supplier cost) | **WARN** | Expected supplier cost and profit are exposed to the Operational backend. They are mapped in DTOs. Ensure these are NOT exposed to the Operational Frontend. |
| **Customer Metadata** | Name, Contact, PassportNo, IqamaNo, Dob, Nationality | **INFO** | Necessary for operations managers to process applications and VFS appointments. Protected by bearer token policy. |

---

## 2. Conclusion
The internal endpoints only expose data fields relevant to booking fulfillment and processing status. No ledger logs, wallet tables, or payment credentials are leaked.
