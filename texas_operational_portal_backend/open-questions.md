# Open Questions

This file logs items needing business clarification or architectural decisions during Phase 1.

---

## 1. Local Development Fallbacks
*   **Question**: Does the remote login check API (Texas Connect Core API) currently require an active host for validation during local development, or should we configure a toggle to fall back to simulated mock accounts when the server is unreachable?
*   **Decision**: During local development, the login flow will dynamically fallback to mock/simulated validation if the remote URL check fails or is not configured.

---

## 2. Service Key Rotations
*   **Question**: Should the Transitional API Key mechanism default to a header token config matching the legacy `'X-OPS-KEY'` (`tx_ops_secure_888`), or should we immediately deploy a more modern HMAC signature verification?
*   **Decision**: For Phase 1, we will implement a configurable API key header token matching `X-OPS-KEY: tx_ops_secure_888` for compatibility, but isolate it in an authentication handler so it remains simple to replace with HMAC signed headers in Phase 2.
