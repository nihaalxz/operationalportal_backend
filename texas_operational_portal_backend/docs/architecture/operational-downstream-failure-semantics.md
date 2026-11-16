# Downstream Failure Semantics

This document defines how the Operational Portal backend handles and reports failures originating from its primary dependency: the Sales Portal API.

---

## 1. Downstream Error Mapping Rules

To avoid leaking internal implementation details or downstream service endpoints, and to provide consistent HTTP status codes to clients, the following mappings are enforced:

| Sales Portal API State / Status | Cause / Scenario | Operational API Mapping | Title | Detail |
| :--- | :--- | :--- | :--- | :--- |
| **Connection Refused / Failed** | Sales Portal is offline / unreachable | **503 Service Unavailable** | Dependency Unavailable | The required Sales booking service is currently unreachable. Please try again. |
| **Timeout (HTTP 504 / TaskCanceled)** | Downstream call exceeded request timeout | **504 Gateway Timeout** | Dependency Timeout | The request to the Sales booking service timed out. |
| **HTTP 401 Unauthorized** | Invalid service API key configured | **502 Bad Gateway** | Bad Gateway | An error occurred communicating with the downstream booking service. |
| **HTTP 403 Forbidden** | Invalid permissions for key | **502 Bad Gateway** | Bad Gateway | An error occurred communicating with the downstream booking service. |
| **HTTP 404 Not Found** (Details) | Specific booking ID does not exist | **404 Not Found** | Booking Not Found | The requested booking ID could not be found. |
| **HTTP 404 Not Found** (Queue) | The list resource URL itself returned 404 | **502 Bad Gateway** | Bad Gateway | The downstream service returned an invalid response. |
| **HTTP 429 Too Many Requests** | Downstream rate-limiting triggered | **503 Service Unavailable** | Dependency Busy | The booking service is under heavy load. Please retry in a moment. |
| **HTTP 500 Internal Error** | Sales backend crashed | **502 Bad Gateway** | Bad Gateway | Downstream service returned an internal error. |
| **Invalid JSON / Malformed** | Downstream returned HTML error page | **502 Bad Gateway** | Bad Gateway | Received a malformed response from the booking service. |

---

## 2. Centralized Exception Handling

A global ASP.NET Core exception filter, `DownstreamExceptionFilter`, is used to capture any `DownstreamException` and translate it to an RFC 7807-compliant `ProblemDetails` response:

- Internal downstream URLs and raw exception stack traces are **never** returned in the response body.
- All failed downstream requests are logged internally at the `Error` severity level, containing the exact downstream URI, returned body, and original exception context.
