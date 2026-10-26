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

### User and role administration

User administration and role-management endpoints require the authenticated tenant context. User create/edit forms use Side Sheets; role options come from the roles the actor can safely assign. Profile-only edits preserve existing assignments even when an assigned role exceeds the actor's own permission set; actual role changes still require every resulting role to fit within the actor's effective permissions. Role management uses `users.write` and applies the same server-side permission-subset check so an actor cannot grant access they do not already have.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/users` | `users.read` or `users.write` | List tenant users with their assigned roles |
| POST | `/api/users` | `users.write` | Create a tenant user and assign tenant roles |
| PUT | `/api/users/{id}` | `users.write` | Update a tenant user's profile and role assignments |
| GET | `/api/users/{id}/permissions` | `users.write` | Read role-inherited access and direct user overrides |
| PUT | `/api/users/{id}/permissions` | `users.write` | Save direct `Allow`/`Deny` overrides; omitted entries inherit from roles |
| GET | `/api/roles` | Authenticated | List roles for user role assignment |
| GET | `/api/roles/manage` | `users.write` | List roles the actor can safely manage |
| GET | `/api/roles/permissions` | `users.write` | List only permissions the actor may assign |
| GET | `/api/roles/{id}` | `users.write` | Read a manageable role and its permissions |
| POST | `/api/roles` | `users.write` | Create a tenant role with an allowed permission set |
| PUT | `/api/roles/{id}` | `users.write` | Update a role and replace its permission set |
| DELETE | `/api/roles/{id}` | `users.write` | Soft-delete an unassigned, non-system role |

Role and user-role-assignment changes are audited. An actor cannot alter their own role assignments. System roles cannot be renamed or deleted; a role cannot be deleted while assigned to a user; and role updates cannot remove the acting user's final effective `users.write` permission. No schema migration is required: role, permission, tenant, and role-permission tables already exist.

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
| `sid` | custom | `UserSession.Id`; validated against the active persisted session on every authenticated API request |
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
- Every access token is bound to its persisted `UserSession.Id` through `sid`. The API rejects an authenticated request with `401` if that exact session is absent, revoked, expired, associated with a different user/tenant, or belongs to an inactive/deleted user.
- After validating the session, `UserPermissionClaimsMiddleware` replaces token role and permission claims with the user's current effective roles and permissions from the same tenant. Role or direct-permission revocations therefore take effect on the next API request, without waiting for access-token expiry.
- Refresh rotates the persisted session and issues a JWT with the new session id. No schema change is needed because `UserSession.Id` is already persisted.

## Tenant Resolution

`AppDbContext.GetCurrentTenantId()` resolves the tenant from the `tenantId` JWT claim via the scoped `IHttpContextAccessor`. Outside an HTTP request (e.g. background/seed paths) it falls back to the demo tenant `11111111-1111-1111-1111-111111111111`. The global query filter `e.TenantId == GetCurrentTenantId()` applies to all entities.

## Frontend Integration

- `apps/web-react/src/api/client.ts` — centralized React/Vite client; captures the active client-session and management-context generation for each request, fences late responses, aborts in-flight requests at session/context changes, and keys refresh single-flight work to the owning session and refresh token.
- `apps/web-react/src/auth/AuthProvider.tsx` — verifies the active session with a fresh `/api/auth/me` before exposing authenticated UI. Logout clears local credentials, user state, and private query cache immediately, then best-effort unregisters the current push device and revokes the server session.
- Each authenticated client session receives a fresh TanStack Query client. Account changes clear/cancel the previous cache; protected routes wait for authentication and the management-scope bootstrap to finish.
- The client synchronizes login/logout changes across tabs via the non-secret `nexus_auth_session_id` localStorage key and revalidates `/me` when another tab changes the session. Access/refresh tokens remain in localStorage; this retains the documented XSS exposure and should be revisited for production hardening.
- Session or management-context changes invalidate late API responses; a stale response or `401` cannot populate the next user's cache or trigger refresh/clear against the next user's credentials.
- TanStack Query retries are disabled for authorization failures and other client errors (`shouldRetry` in `apps/web-react/src/api/queryClient.ts`).

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
