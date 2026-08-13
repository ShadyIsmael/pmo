# Authentication and Identity

## Overview
The Identity module provides end-to-end authentication for the NEXUS PMO platform: login, session refresh with token rotation, logout, and current-user retrieval. It issues JWT access tokens containing identity, role, permission, and tenant claims, and stores sessions (with hashed refresh tokens) in the `UserSession` entity.

## Flow

```
Login ──► POST /api/auth/login
              │  validate PasswordHasher hash
              ▼
         JWT (8h) + refresh token (7d)
              │  UserSession row: SHA-256(refreshToken)
              ▼
   Client stores tokens → Authorization: Bearer <jwt>
              │
   Access token expires (401)
              ▼
   POST /api/auth/refresh  ──► revoke old session, create new one, return new pair
              │
   POST /api/auth/logout   ──► revoke session and refresh token
```

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/login` | Public | Validate email/password, create session, return token pair + user |
| POST | `/api/auth/refresh` | Public (token) | Rotate refresh token; return new access + refresh tokens |
| POST | `/api/auth/logout` | Authenticated | Revoke current session; optionally revoke by refresh token |
| GET | `/api/auth/me` | Authenticated | Return current user with roles and permissions |
| POST | `/api/auth/switch-role` | Authenticated (demo) | Switch active role context (dev feature) |

### Request / Response shapes

`POST /api/auth/login`
```json
{ "email": "admin@nexus.com", "password": "Demo123!" }
```
```json
{
  "token": "<jwt>",
  "refreshToken": "<opaque-token>",
  "expiresAt": "2026-08-01T00:00:00Z",
  "user": {
    "id": "guid",
    "email": "admin@nexus.com",
    "fullName": "Alex Admin",
    "jobTitle": "System Administrator",
    "roles": [ { "name": "admin" } ],
    "permissions": [ "Resource.View", "Resource.Manage", "...", "Project.*" ]
  }
}
```

`POST /api/auth/refresh` body: `{ "refreshToken": "<opaque-token>" }`
Response is the same shape as login.

## JWT Claims

| Claim | Type | Source |
|-------|------|--------|
| `sub` | NameIdentifier | User id |
| `email` | ClaimTypes.Email | User email |
| `unique_name` | ClaimTypes.Name | Display name |
| `tenantId` | custom | Tenant id (from user's tenant) |
| `role` | ClaimTypes.Role | One per role |
| `permission` | custom | One per `Resource.Action` permission |

Access token expires after `Jwt:ExpiryHours` (default 8 hours). `Jwt:RefreshTokenLifetimeDays` (default 7) bounds refresh-token validity.

## Password Hashing

- Passwords are hashed with ASP.NET Core `IPasswordHasher<User>` (`PasswordHasher<User>`, Identity V3 format).
- `DemoAuthService` verifies with `VerifyHashedPassword`.
- `SeedDataProvider` writes hashes via the same `IPasswordHasher`. Legacy BCrypt hashes (`$2a$`/`$2b$`/`$2y$`) are detected on reseed and re-hashed to the Identity format.
- BCrypt.Net package has been removed from `NEXUS.Modules.Identity.csproj`.

## Session Management

- Each login creates a `UserSession` row (session id, user id, issued/expiry, token hash, status).
- The refresh token is stored only as a SHA-256 hex hash in `UserSession.RefreshToken`; the plaintext is returned once at issuance.
- `POST /api/auth/refresh`:
  1. Looks up the session by the hashed refresh token.
  2. Rejects expired/revoked/inactive sessions.
  3. Revokes the old session and creates a new one (rotation).
  4. Returns a fresh token pair.
- `POST /api/auth/logout` revokes the session referenced by the supplied refresh token (and the current session).

## Tenant Resolution

`AppDbContext.GetCurrentTenantId()` resolves the tenant from the `tenantId` JWT claim via the scoped `IHttpContextAccessor`. Outside an HTTP request (e.g. background/seed paths) it falls back to the demo tenant `11111111-1111-1111-1111-111111111111`. The global query filter `e.TenantId == GetCurrentTenantId()` applies to all entities.

## Frontend Integration

- `apps/web/src/lib/api-client.ts` — single centralized client. Attaches `Authorization: Bearer <token>`, parses `ApiError`, serializes query params, and exposes `login/logout/refresh/getMe`, `setTokens/clearTokens/hasTokens/subscribeTokens`.
- On a 401 the client refreshes once (single-flight `refreshPromise`), retries the original request once, then clears the session and redirects to `/{locale}/login`.
- Tokens are stored in `localStorage` under `nexus_access_token` and `nexus_refresh_token`.
- `apps/web/src/lib/auth.tsx` — `AuthProvider` + `useAuth()` with `status` (`loading | authenticated | unauthenticated`), `user`, `login`, `logout`, `refreshUser`.
- `apps/web/src/app/[locale]/(dashboard)/layout.tsx` — protected layout; shows a loader until `authenticated`, redirects to login when `unauthenticated`.
- `apps/web/src/app/[locale]/login/page.tsx` — login page wired to `useAuth().login`.
- All resource hooks (`useResources`, `useSkills`, `useVendors`, `useCapacity`, `useAllocations`, `useResourceFinder`) call through the central `api` client.
- TanStack Query retries are disabled for `ApiError` with status 401/403 (`shouldRetry` in `apps/web/src/app/providers.tsx`).

## Demo Accounts

Seeded in Development only (seed skipped in Production). All use password `Demo123!`:

| Email | Role |
|-------|------|
| admin@nexus.com | System Administrator |
| executive@nexus.com | Executive |
| portfolio.manager@nexus.com | Portfolio Manager |
| project.manager@nexus.com | Project Manager |
| team.member@nexus.com | Team Member |
| pmo.admin@nexus.com | PMO Administrator |
| finance.controller@nexus.com | Finance Controller |
| resource.manager@nexus.com | Resource Manager |

## Key Files

- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DemoAuthService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs`
- `apps/api/src/NEXUS.Modules.Identity/ModuleRegistration/IdentityModuleRegistration.cs`
- `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs`
- `apps/api/src/NEXUS.Persistence/AppDbContext.cs` (`ConfigureTenantResolution`, `GetCurrentTenantId`)
- `apps/web/src/lib/api-client.ts`
- `apps/web/src/lib/auth.tsx`
- `apps/web/src/app/[locale]/login/page.tsx`

See also [SECURITY-ARCHITECTURE.md](../architecture/SECURITY-ARCHITECTURE.md).
