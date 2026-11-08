# Security Architecture

---

## Authentication

### JWT Bearer Tokens

| Aspect | Detail |
|--------|--------|
| **Scheme** | JWT Bearer (Bearer) |
| **Secret** | `Jwt:Secret` (environment: `Jwt__Secret`). No fallback value is compiled in: Development generates a random process-local key, and every other environment fails at startup when the setting is missing or shorter than 32 bytes |
| **Issuer** | `NEXUS` (`appsettings.json`) |
| **Audience** | `NEXUS-API` (`appsettings.json`) |
| **Access-token expiry** | 8 hours (`Jwt:ExpiryHours`) |
| **Refresh-token lifetime** | 7 days (`Jwt:RefreshTokenLifetimeDays`) |
| **Configuration** | `Program.cs` - `AddAuthentication().AddJwtBearer()` with symmetric key signing |

**Evidence:** `Program.cs` configures `AddAuthentication().AddJwtBearer()`.

### JWT Claims

Access tokens carry identity, tenant, session, role, and permission snapshots. The role and permission snapshots are refreshed from the tenant-scoped database before authorization on every authenticated request and are not trusted as the final permission source.

| Claim | Value |
|-------|-------|
| `sub` (NameIdentifier) | User GUID |
| `email` (ClaimTypes.Email) | User email |
| `unique_name` (ClaimTypes.Name) | Display name |
| `tenantId` | Current tenant GUID |
| `sid` | Persisted `UserSession.Id` for this access-token session |
| `role` | One claim per role name |
| `permission` | Login-time snapshot of effective `Resource.Action` permissions; replaced by current effective permissions in `UserPermissionClaimsMiddleware` on each request |

### Authentication Service

| Aspect | Detail |
|--------|--------|
| **Service** | `DemoAuthService` (`Infrastructure/DemoAuthService.cs`) |
| **Interface** | `IAuthenticationService` (`Application/IAuthenticationService.cs`) |
| **Login endpoint** | `POST /api/auth/login` (`AllowAnonymous`) -- validates email/password against DB without requiring a prior session, returns JWT + refresh token |
| **Refresh endpoint** | `POST /api/auth/refresh` (`AllowAnonymous`) -- rotates the refresh token, returns a new token pair |
| **Logout endpoint** | `POST /api/auth/logout` -- revokes the session and its refresh token |
| **Current-user endpoint** | `GET /api/auth/me` -- returns user + roles + permissions |
| **Password hashing** | ASP.NET Core `IPasswordHasher<User>` (`PasswordHasher<User>`, Identity V3 format) |
| **Refresh-token storage** | SHA-256 hash stored in `UserSession.RefreshToken`; plaintext token never persisted |
| **Session rotation** | `RefreshAsync` revokes the old `UserSession` and creates a new one on every refresh |

**Evidence:** `IdentityModuleRegistration.cs` registers `IPasswordHasher<User>`; `DemoAuthService.cs` implements hashing + rotation.

### Session and authorization isolation

- `UserPermissionClaimsMiddleware` runs after JWT authentication and before authorization. Each protected authenticated request must carry a valid `sid` whose `UserSession` row matches the token's user and tenant, is not revoked or expired, and belongs to an active, non-deleted user in that tenant. Missing or invalid session binding returns `401`. It skips endpoints explicitly marked `AllowAnonymous`, including login and refresh.
- The middleware removes JWT role and permission snapshots and reloads the user's effective role names and permissions from the same tenant before authorization policies execute. Role or direct-permission revocation therefore applies on the next request.
- Refresh revokes the old persisted session and issues the new access token with the replacement session's id. The existing `UserSession.Id` column is reused; no migration is needed.
- In React, `AuthProvider` revalidates `/api/auth/me` before releasing protected UI, scopes React Query to one authenticated session, and clears the previous cache on login/logout/session change. The API client aborts and rejects work started under an older session or management context; those fenced failures are not retried. Cross-tab changes are synchronized using a non-secret localStorage session marker.
- Logout removes the local authenticated view/cache immediately and starts server-session revocation without waiting for best-effort push-device cleanup. An already-authorized server operation may finish, but its stale response is not exposed to the next client session.

### My Account self-service boundary

- Account API routes are `[Authorize]` and require valid `NameIdentifier`, `tenantId`, and `sid` claims. The client cannot select a different account or tenant. Profile writes update only FirstName/LastName, and security/session queries constrain both user and tenant.
- The account projection excludes password hashes, tokens, refresh-token hashes, Resource cost profiles, and financial rates. Professional data is read from the current linked Resource; tenant and organization settings access is independently projected from the tenant and explicit grant records.
- Password changes use the existing `IPasswordHasher<User>` and configured local password length policy, require the current password, revoke all active user sessions, and write an audit event without credential data. The change endpoint uses the existing authentication rate limit. Federated identities cannot change their provider-managed password here.
- Session revoke operations update persisted `UserSession.RevokedAt`; the existing per-request session validator makes that effective on the next API request. `revoke-others` excludes only the verified current `sid`. A successful self-password change therefore requires local sign-in again.
- Automated source tests/builds are not authenticated runtime evidence. Do not claim runtime PASS without verifying these routes under an authorized Development test identity.

Regression coverage is in `apps/api/tests/NEXUS.Api.Tests/Integration/UserPermissionAdministrationTests.cs` and `apps/web-react/src/test/auth-session-isolation.test.tsx`. Compilation, automated test execution, and live browser UAT must be reported separately; compilation alone is not UAT evidence.

### Development bootstrap identity

A fresh Development seed creates the protected `admin@nexus.com` account and tenant role templates. Role templates are not additional login accounts. A new or legacy-hash Admin account requires `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` from a secure secret source; the seed has no embedded administrator password. The seeded Admin is marked to change the password at first login. Existing bcrypt hashes are upgraded through `PasswordHasher<User>` on reseed.

Bootstrap temporary passwords expire according to `Security:LocalPasswords:TemporaryPasswordExpiryHours` (24 hours by default). Ordinary reseeds preserve current Admin hashes and password-change/expiry state. For temporary Development UAT access, `Security:LocalPasswords:DevelopmentAdminTemporaryPasswordBypassEnabled` is false by default and skips initial-password/expiry enforcement only for the seeded local Admin while the host is Development. The existing hash still must verify; no persisted password state or permission data changes, and disabling the setting restores normal enforcement on subsequent requests. An expired Development bootstrap credential can also be recovered by the local interactive command: it verifies the Development server/database identity and pending migrations, requires an independent SQL Server backup that passes `RESTORE VERIFYONLY`, reads temporary and permanent passwords without console echo, and uses the registered `IPasswordHasher<User>`. No public recovery endpoint or global expiry bypass exists.

### Role Switching

| Endpoint | Purpose |
|----------|---------|
| `POST /api/auth/switch-role` | Compatibility endpoint that returns the current user; it does not switch roles |

---

## Authorization

### Assignment-scoped approval review

An actionable approval assignment may expose only its bounded review projection when the authenticated user is the current assignee, the request is still reviewable, the effective approval permission and tenant match, and the owning object is valid. This is not a general Management Scope exemption: ordinary object reads and mutations remain scope checked. The Change Request exception is limited to its exact pending assignment, review, and canonical decision path, with authorization revalidated by the domain service. Handover decisions continue through the existing accept/reject services; role activation occurs only after acceptance. Knowing an approval identifier is never authorization.

### ASP.NET Core Authorization

| Aspect | Detail |
|--------|--------|
| **Attribute** | `[Authorize]` on controllers |
| **Policy-based** | `Program.cs` and modules register exact canonical permission policies (Resource examples: `Resource.View`, `Resource.Create`, `Resource.Edit`, `Resource.Delete`, `Resource.Assign`, `Resource.ViewCostRate`, `Resource.EditCostRate`) |
| **Pipeline** | `app.UseAuthentication()` + `app.UseAuthorization()` in `Program.cs` |

### Role-Based Permissions

`SeedDataProvider.cs` defines tenant role templates and the deterministic Development bootstrap. A fresh seed creates only the protected `admin@nexus.com` account; it does not create shared-password demo users. The tenant `admin` role receives the protected baseline derived from active catalog metadata. `pmo.admin` and other roles keep their separately defined grants and gain no authority from their names.

Authorization policies require exact active canonical keys. No tenant role name is an API authorization substitute. See [the permission model](permission-model.md) and [the current permission audit](../security/permission-audit/FINAL-PERMISSIONS-CLOSURE-REPORT.md) for source counts and verification limits.

### Frontend Permission Model

The supported React application mirrors the 154 active canonical permission keys in `apps/web-react/src/lib/permissions.ts`. Routes and controls use permission capabilities, not a frontend role-name list. These checks guide the UI; the backend remains the security boundary.

---

## Data Scope Authorization

Tenant identity comes from the authenticated principal and is validated against the active tenant registry. Missing or invalid request tenant context fails closed; the EF tenant query filter returns no tenant rows without a valid context. Platform configuration uses separate platform authorization and cannot be granted through tenant roles. Financial API operations use their specific canonical permission keys, with resource scope and workflow rules checked by the backend service.

### Project public identifiers and object scope

- Browser-facing `/api/projects/{id}/...` routes and `projectId` query filters accept only `Project.PublicId`. The API resolves that value within the authenticated tenant; an internal `Project.Id` in either external position is rejected, with no fallback lookup.
- `ManagementContextMiddleware` checks the resolved internal project against the caller's effective management scope. Endpoint authorization policies independently enforce effective permissions; the public ID is not an authorization credential.
- `ProjectResourceScopeMiddleware` applies object-level scope checks to direct project-owned records and validates parent ownership for plan/version/content and risk action/impact routes. Tenant predicates are used when resolving owners.
- React owns a QueryClient per authenticated session and discards it on login, logout, expiry, or observed session change. The API remains the authority; frontend guards/cache isolation are not substitutes for server authorization.
- `Project.Id` may remain in response DTOs and relationship request bodies for internal joins and existing domain contracts. It must not be used in browser project URLs or `projectId` query filters; project DTOs provide `PublicId` for those external references.

### Administrative assignment recovery scope

- `/api/governance/assignment-recovery` is a deliberately tenant-wide administrative discovery surface for Projects, Programs, and Portfolios. It is not an entity-data read bypass: it returns a bounded recovery projection, resolves entities by tenant + `PublicId`, and rechecks the active actor and effective recovery permission in the service.
- `ManagementContextMiddleware` skips normal Management Scope addressability only for this exact route prefix, authenticated Native mode, and a user carrying at least one recovery permission. Controller policies and service-level effective permission checks remain mandatory. ReadOnlyViewAs does not receive this exception, and ordinary entity routes retain their existing Management Scope checks.
- State changes use canonical manager-handover acceptance or the existing Sponsor synchronizer, are tenant-validated and audited, and do not make a proposed incoming manager current before acceptance. Reconciliation is an explicit `Manage` action with history preservation and concurrency handling.
- Implementation and current verification limits: [Entity Assignment Recovery](../modules/ENTITY-ASSIGNMENT-RECOVERY.md).

### Tenant Isolation Infrastructure

| Component | Implementation | Status |
|-----------|---------------|--------|
| `ITenantEntity` interface | `TenantId` property on `BaseEntity` | Implemented |
| Global query filter | `e.TenantId == GetCurrentTenantId()` | Implemented |
| Tenant resolution | `GetCurrentTenantId()` reads the authenticated `tenantId` claim; validated pre-authentication session writes use a bounded server-derived scope only on anonymous auth endpoints | Implemented |
| Seed tenant ID | `11111111-1111-1111-1111-111111111111` | Fixed in seed data |

**Note:** Tenant identity for login session writes is loaded from the matched account only after password verification. Per-tenant token issuance binds the resulting session and JWT to that same persisted tenant.

---

## Production Security Gaps

| Gap | Severity | Detail |
|-----|----------|--------|
| Default seed credentials | Resolved | The Development seed requires `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` from a secure environment source, enforces a minimum length, and requires a first-login password change. No shared-password demo users or default password are seeded. |
| JWT secret fallback | Resolved | No fallback signing key is compiled in. Development generates a random process-local key; every other environment fails fast unless `Jwt:Secret`/`Jwt__Secret` is supplied with at least 32 bytes of UTF-8 data. Previously issued tokens must be invalidated by rotating the deployed secret |
| No HTTPS enforcement | High | `ASPNETCORE_URLS=http://+:5000` in Dockerfile |
| No rate limiting | Resolved | Fixed-window limiter (10 requests/minute per client IP) on `/api/auth/login` and `/api/auth/refresh` |
| No CORS restrictions (methods/headers) | Medium | `AllowAnyHeader().AllowAnyMethod()` in `Program.cs` |
| Hardcoded DB password | Resolved | `docker-compose.yml` requires `NEXUS_DB_PASSWORD` from the environment and binds SQL Server to `127.0.0.1:1433` |
| Externally reachable caches/databases | Resolved | SQL Server and Redis publish on loopback only; Redis supports optional AUTH via `NEXUS_REDIS_PASSWORD` |
| Anonymous sensitive endpoints | Resolved | `/api/ai-planning` requires canonical Planning permissions; `/api/integrations` requires `Settings.View` and returns a projection without credentials |
| No audit auto-logging | Medium | Audit entities exist but no automatic `SaveChanges` interceptor |
| No secrets management | High | No vault/KeyVault integration; secrets must be injected by the deployment platform |
| No password policy | Medium | Local password policy exists for temporary passwords; no account lockout logic |
| No MFA | High | No multi-factor authentication support |
| Refresh tokens in localStorage | Medium | Frontend stores access + refresh tokens in localStorage (XSS risk); cookies preferred for production |
