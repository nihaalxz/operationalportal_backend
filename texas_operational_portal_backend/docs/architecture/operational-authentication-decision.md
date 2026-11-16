# Operational Authentication Decision

This document records the design decision for the Operational Portal authentication model, aligning it with local checks and external synchronization fallbacks.

---

## 1. Authentication Flow & Remote Check Fallback

The login endpoint `POST /api/auth/login` checks the local SQL Server database first.

```mermaid
sequenceDiagram
    participant FE as SvelteKit Frontend
    participant BE as Operational Backend
    participant DB as Operational DB
    participant Core as Texas Core API (Remote)

    FE->>BE: POST /api/auth/login (email, password)
    BE->>DB: Query user by email
    alt User exists & Active
        BE->>BE: Verify local PasswordHash
        alt Success
            BE->>FE: Return Access Token & Cookie Refresh Token
        else Failure
            BE->>FE: 401 Unauthorized
        end
    else User does not exist locally
        BE->>Core: POST /api/remote-login-check (email, password, role="Operations")
        alt Remote authentication success
            Core-->>BE: 200 OK (Name, UserType, Permissions)
            BE->>DB: Insert new OperationalUser with password hash
            BE->>FE: Return Access Token & Cookie Refresh Token
        else Remote failure
            Core-->>BE: 401/500
            BE->>FE: 401 Unauthorized (Generic Credential Error)
        end
    end
```

---

## 2. JWT & Refresh Token Configuration

*   **Access Token (JWT)**:
    *   *Lifetime*: 15 minutes.
    *   *Payload*: Includes `sub` (Id), `email`, `role` (UserType), and `permissions` claims.
    *   *Signing*: HMAC SHA-256 with a secure private key.
*   **Refresh Token**:
    *   *Lifetime*: 7 days.
    *   *Storage*: HttpOnly, Secure, SameSite=Strict cookie named `refresh_token`.
    *   *Rotation*: Re-generated on every usage. Old token is marked as revoked.
*   **Disabled Accounts**: Checked on every request by checking `IsActive` flag. Blocked immediately if set to `false`.
*   **Password Hashing**: Stored locally using `BCrypt.Net-Next`.
