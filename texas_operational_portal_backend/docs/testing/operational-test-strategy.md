# Operational Test Strategy

This document outlines the testing architecture for validating the new Operational Portal.

---

## 1. Backend Testing

### Unit Tests (`TexasOperationalPortal.UnitTests`)
*   *Scope*: Validation of domain entities, claims mapper, token generation, and password hashing utility functions.
*   *Framework*: xUnit, FluentAssertions.

### Integration Tests (`TexasOperationalPortal.IntegrationTests`)
*   *Scope*: End-to-end API validations focusing on:
    *   Authentication controllers (Success, failure, and deactivated users).
    *   Authorized endpoints (Ensuring correct role/permission claims check).
    *   Mocked remote login check integrations.
    *   Sales Portal API client HTTP mocks.
*   *Framework*: WebApplicationFactory, SQL Server local/in-memory instance.

### Architecture Tests (`TexasOperationalPortal.ArchitectureTests`)
*   *Scope*: Enforcement of Clean Architecture layer dependencies (Domain has no dependencies, API does not directly touch DbContext).
*   *Framework*: NetArchTest.eShopOnWeb.

---

## 2. Frontend Testing

### Unit Tests
*   *Scope*: Testing store reactions, token refresh states, navigation visibility conditions, and query state debouncing.
*   *Framework*: Vitest, Svelte Testing Library.
