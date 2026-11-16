# Sales-Operational Read Contracts

This document defines the exact contract specifications for the internal endpoints in the Sales Portal.

---

## 1. Endpoint Definitions

All internal endpoints require authentication via the `X-OPS-KEY` header and return JSON payloads.

### GET `/api/internal/operations/visas`
Exposes the paginated and filtered list of all Sales Visa Applications.
*   **Query Parameters**:
    *   `page` (int, default: 1)
    *   `pageSize` (int, default: 10)
    *   `status` (string, optional: pending, under_review, success, rejected)
    *   `search` (string, optional)
*   **Response Payload**:
    ```json
    {
      "items": [
        {
          "id": 1,
          "headName": "John Doe",
          "visaPackName": "Dubai Tourist Visa",
          "clientType": "Inhouse",
          "processingStatus": "Pending",
          "paymentStatus": "Paid",
          "payableAmount": 550.00,
          "applicantCount": 1,
          "createdAt": "2026-01-14T06:50:32Z"
        }
      ],
      "totalCount": 1,
      "pageNumber": 1,
      "pageSize": 10,
      "totalPages": 1
    }
    ```

### GET `/api/internal/operations/visas/{id}`
Returns details for a visa booking. Includes applicant listings and document details.
*   **Response Payload**:
    ```json
    {
      "id": 1,
      "visaPackName": "Dubai Tourist Visa",
      "clientType": "Inhouse",
      "headName": "John Doe",
      "processingStatus": "Pending",
      "paymentStatus": "Paid",
      "payableAmount": 550.00,
      "createdAt": "2026-01-14T06:50:32Z",
      "applicants": [
        {
          "id": 10,
          "fName": "Jane",
          "lName": "Doe",
          "passportNo": "AB1234567",
          "nationality": "Indian"
        }
      ]
    }
    ```

### GET `/api/internal/operations/vfs-appointments`
*   **Query Parameters**: Similar to visas.
*   **Response Payload**: Paginated list containing `id`, `headName`, `name`, `country`, `visaType`, `appointmentDate`, and `processingStatus`.

### GET `/api/internal/operations/holiday-packages`
*   **Query Parameters**: Similar to visas.
*   **Response Payload**: Paginated list containing `id`, `headName`, `passengerName`, `country`, `days`, and `processingStatus`.
