# Operational Security Review

This document audits the security controls, validation mechanisms, and lifecycle protection implemented across the Operational Portal backend.

---

## 1. Security Control Audit

### Data Isolation
- **No Direct Database Access**: Operational Portal contains no connection strings or entity configurations pointing to the Sales Portal database.
- **No Booking Duplication**: No database tables are defined for bookings (`Visa`, `Vfs`, `Holiday`). Data is queried on-demand via the transitional service API.

### Credential & Authentication Hardening
- **Decoupled User Identity**: Removed all remote login check fallbacks, preventing credential forwarding or automatic sync of unauthorized users.
- **Local Credentials**: Operational users authenticate locally using BCrypt-hashed password credentials.
- **Cryptographically Hashed Refresh Tokens**: Refresh tokens are stored in the database strictly as SHA-256 hashes of the base64-encoded random tokens returned to clients. This prevents session hijack if the database is leaked.
- **Token Rotation & Reuse Detection**: Refresh tokens are rotated on every use. If a revoked refresh token is presented, the system flags it as potential theft, immediately invalidating all active refresh tokens for that user.
- **Instant Revocation Middleware**: A global custom middleware (`UserActiveCheckMiddleware`) verifies that the authenticated user is `IsActive` in the database on every request, neutralizing active JWT tokens immediately if a user is deactivated or deleted.

### Configuration & Key Handling
- **No Secrets in Source Control**: Default transitional keys and JWT signing secrets are moved to external environment variables / AppSettings.
- **Fail-Fast Configuration Validation**: Strongly typed settings validate minimum signature lengths, valid URIs, positive timeouts, and fail startup if bootstrap parameters are insecure.
- **Constant-Time Verification**: Custom schemes use `CryptographicOperations.FixedTimeEquals` to verify service keys, eliminating timing attacks.

### API Hardening & Rates
- **ProblemDetails Error Handling**: Standard ProblemDetails prevent raw database, system paths, downstream URLs, or system exceptions from leaking in response bodies.
- **Authentication Rate Limiting**: The login endpoint is protected by a fixed window rate-limiting policy (clamped to 5 login attempts per minute per IP), preventing brute-force attacks.

---

## 2. Security Audit Logs

All sensitive actions (Sync requests, login attempts, refresh failures, reuse detections, and logouts) create structured database audit records:

- **Logs do not contain raw passwords or JWT tokens**.
- IPs and timestamps are preserved for administrative review.
