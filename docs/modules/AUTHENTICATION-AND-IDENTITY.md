# Authentication and Identity

## Overview
The Identity module provides end-to-end authentication for the NEXUS PMO platform: login, session refresh with token rotation, logout, and current-user retrieval. It issues JWT access tokens containing identity, role, permission, and tenant claims, and stores sessions (with hashed refresh tokens) in the `UserSession` entity.

`User` represents a login identity and access record: name, email, tenant, authentication/session state, roles, and effective permissions. Professional fields such as job title, mobile phone, avatar, resource type, rate, and availability belong to `Resource`. One active same-tenant Resource may be linked to a User. Lookup projections that need a job title derive it from that linked Resource. Historical `Users.JobTitle`, `Phone`, and `AvatarUrl` columns remain mapped as shadow fields to preserve existing values; they are no longer editable or emitted by user administration/current-user responses.

Invitation-only external Resources have no User record, password, session, or JWT. Their limited-lifetime project invitation token is stored as a hash and can record accept/decline and separate report/notification consent only. New invitation links place the token in the URL fragment, and the React client sends it in a request body so the API token is not part of an HTTP URL. After acceptance, the same project-scoped bearer link can turn off either optional email preference; this action returns consent state only and grants no authentication, project data, or general project API access. Older query-style links are still accepted by the frontend during transition.

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
| POST | `/api/auth/login` | Public (`AllowAnonymous`) | Validate email/password without an existing session, create session, return token pair + user |
| POST | `/api/auth/refresh` | Public (`AllowAnonymous`, refresh token) | Rotate refresh token; return new access + refresh tokens |
| POST | `/api/auth/logout` | Cookie mode: `AllowAnonymous` with CSRF + refresh cookie; bearer mode: authenticated | Revoke the selected session |
| GET | `/api/auth/me` | Authenticated | Return current user with roles and permissions |
| POST | `/api/auth/switch-role` | Authenticated | Compatibility endpoint; returns the current user and does not switch roles |

### User and role administration

User administration and role-management endpoints require the authenticated tenant context. The application services rebind supplied actor/tenant identifiers to the authenticated principal and resolve current persisted effective permissions before returning user/role data or applying mutations. User create/edit forms use Side Sheets; role options come from the roles the actor can safely assign. Profile-only edits preserve existing assignments even when an assigned role exceeds the actor's own permission set; actual role changes still require every resulting role to fit within the actor's effective permissions. Canonical access uses separate `Users.View`, `Users.ManageAccounts`, `Users.AssignRoles`, `Users.SetStatus`, `Users.ResetPassword`, `UserPermissions.Manage`, and role-management permissions. There are no active `users.read` or `users.write` authorization aliases.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/users` | `Users.View` | List tenant users with their assigned roles |
| POST | `/api/users` | `Users.ManageAccounts`; optional role assignment or inactive status also requires `Users.AssignRoles` or `Users.SetStatus` | Create a tenant user and assign only safely manageable tenant roles |
| PUT | `/api/users/{id}` | Matching `Users.ManageAccounts`, `Users.AssignRoles`, or `Users.SetStatus` capability | Update only authorized profile, role-assignment, and status fields; the service checks field groups independently |
| GET/PUT | `/api/users/{id}/permissions` | `UserPermissions.Manage` | Read or save direct `Allow`/`Deny` overrides; Deny wins and omitted entries inherit from roles |
| GET | `/api/roles` | `Roles.View` | List same-Tenant roles the actor may inspect |
| GET `/api/roles/manage`, GET `/api/roles/{id}` | `Roles.View` | List roles the actor can safely manage or read |
| GET `/api/roles/permissions` | `Roles.ConfigurePermissions` | List only active permissions within the actor's delegation ceiling |
| POST | `/api/roles` | `Roles.Create` and `Roles.ConfigurePermissions` when assigning permissions | Create a tenant role with an allowed permission set |
| PUT | `/api/roles/{id}` | `Roles.EditDetails` for details; `Roles.ConfigurePermissions` for grants | Update only authorized role fields; permission replacement is bounded by the actor's effective grants |
| DELETE | `/api/roles/{id}` | `Roles.Delete` | Soft-delete an unassigned, non-system role |

Role and user-role-assignment changes are audited. An actor cannot alter their own role assignments. System roles cannot be renamed or deleted; a role cannot be deleted while assigned to a user; and a role update cannot remove the acting user's final effective role-management capability. Tenant settings use `Settings.ManageTenantConfiguration`; tenant reference-data writes require the granular `ReferenceData.Create`, `ReferenceData.Edit`, or `ReferenceData.Delete` permission for the operation. Platform settings and tenant provisioning use separate Platform policies and are not granted by tenant Administration permissions.

### Request / Response shapes

`POST /api/auth/login`
```json
{ "email": "admin@nexus.com", "password": "<development seed password>" }
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
    "permissions": [ "Resource.View", "Resource.Create", "Project.View" ]
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

## Temporary passwords and first login

Temporary passwords are governed by `Security:LocalPasswords:TemporaryPasswordExpiryHours` (default 24 hours; supported range 1–168 hours). Login rejects a temporary credential when `MustChangePassword` is true and its expiry is missing or has elapsed. The forced-password middleware blocks application APIs until the user changes it. Temporary login responses carry no application roles or permissions.

`POST /api/auth/change-initial-password` verifies the current temporary password and applies the configured local password length policy. On success it hashes the permanent password with `IPasswordHasher<User>`, clears `MustChangePassword` and `TemporaryPasswordExpiresAt`, updates the password version timestamp, revokes active sessions, and audits the state change without credential material. The client must sign in again with the new password.

## Session Management

- Each login creates a `UserSession` row (session id, user id, issued/expiry, token hash, status).
- Login and refresh carry explicit `AllowAnonymous` endpoint metadata. The session/permission and initial-password middleware skip these endpoints even if a bearer token is present; login therefore does not depend on an earlier access-token session.
- After login credentials are verified, session creation and last-login metadata use the tenant ID read from the matched account inside a bounded `PreAuthenticationTenantExecutionContext`. Refresh uses the persisted session tenant only after the refresh-token hash, session state, and owning account have been validated. Cookie logout looks up by the refresh-token hash and revokes within that session's tenant. These scopes apply only to `AllowAnonymous` endpoints; authenticated request claims remain authoritative and ordinary anonymous writes still fail closed.
- Invalid login credentials return `401` with the generic credential message. Database exceptions remain `500` and are not rewritten as session failures.
- Login request cancellation is propagated through EF Core to SQL Server. The API treats an `OperationCanceledException` or the narrowly identified SqlClient command-cancellation error as an expected abort only when `HttpContext.RequestAborted` is canceled. Other SQL failures remain error-logged; if the client has disconnected, the handler logs the failure without trying to write a response.
- The refresh token is stored only as a SHA-256 hex hash in `UserSession.RefreshToken`; the plaintext is returned once at issuance.
- `POST /api/auth/refresh`:
  1. Looks up the session by the hashed refresh token.
  2. Rejects expired/revoked/inactive sessions.
  3. Revokes the old session and creates a new one (rotation).
  4. Returns a fresh token pair.
- `POST /api/auth/logout` revokes the session referenced by the supplied refresh token (and the current session).
- Every protected authenticated API request is bound to its persisted `UserSession.Id` through `sid`. The API rejects the request with `401` if that exact session is absent, revoked, expired, associated with a different user/tenant, or belongs to an inactive/deleted user. `AllowAnonymous` endpoints bypass this session check.
- After validating the session, `UserPermissionClaimsMiddleware` replaces token role and permission claims with the user's current effective roles and permissions from the same tenant. Role or direct-permission revocations therefore take effect on the next API request, without waiting for access-token expiry.
- Refresh rotates the persisted session and issues a JWT with the new session id. No schema change is needed because `UserSession.Id` is already persisted.

## My Account self-service

`NEXUS.Api.Account.AccountController` exposes `GET /api/account`, `PUT /api/account/profile`, `POST /api/account/change-password`, `GET /api/account/sessions`, `DELETE /api/account/sessions/{sessionId}`, and `POST /api/account/sessions/revoke-others`. All operations require the normal authenticated middleware and derive user, tenant, and current session from `NameIdentifier`, `tenantId`, and `sid`; no request accepts a target `UserId` or `TenantId`.

The account read model explicitly projects identity and safe Resource fields. Resource cost/payment rates, password hashes, JWTs, and refresh-token material are excluded. Users without a linked Resource receive an empty professional profile and no Resource-derived team/project memberships. Organization access is represented only by the user's explicit `OrganizationSettingsGrant`; organizational-unit membership comes from effective `ResourceOrganizationalUnit` rows. Project relationships are current direct Resource assignments or assignments inherited from a Team in which that Resource currently has effective membership; the source is identified in the response.

Self-service profile updates are limited to FirstName and LastName (100 characters each); email and administrative/professional fields remain read-only. Local password change verifies the current Identity V3 hash, enforces the configured minimum/maximum length, updates through `IPasswordHasher<User>`, revokes every active session, and audits only the fact of the change. Federated identities are directed to their identity provider. Session list/revoke operations are constrained by user and tenant; revoke-others preserves the trusted current `sid`. Because every request validates `sid` against persisted `UserSession`, revoked tokens are rejected on the next request. No database schema change is required.

Theme and locale continue through the existing React providers and browser-local storage. Notification preferences continue through `/api/notifications/preferences`. No duplicate server preference store is introduced; compact layout remains explicitly unavailable because no application-wide compact preference exists yet. The Account API adds no entity or migration. Its project-membership projection reads `ProjectMembers.TeamId`, so databases must include the existing migration `20261031142036_AlignResourceProjectTeamsAndInvitations`; the Development migration state was not rechecked for this Account task.

## Tenant Resolution

`AppDbContext.GetCurrentTenantId()` resolves ordinary request tenants from the authenticated `tenantId` claim via the scoped `IHttpContextAccessor`. A missing or malformed claim resolves to `Guid.Empty`, so tenant query filters fail closed. After credentials or a refresh-token hash have been validated, the Identity service may establish a short-lived `PreAuthenticationTenantExecutionContext` on an `AllowAnonymous` authentication endpoint, using only the tenant ID loaded from the account/session record. Authenticated request claims take precedence; no request-supplied tenant value can establish this scope. Only trusted out-of-request Development initialization paths retain the fixed seed-tenant fallback `11111111-1111-1111-1111-111111111111`.

## Frontend Integration

- `apps/web-react/src/api/client.ts` — centralized React/Vite client; captures the active client-session and management-context generation for each request, fences late responses, aborts in-flight requests at session/context changes, and keys refresh single-flight work to the owning session and refresh token.
- `apps/web-react/src/auth/AuthProvider.tsx` — verifies the active session with a fresh `/api/auth/me` before exposing authenticated UI. Logout clears local credentials, user state, and private query cache immediately, then best-effort unregisters the current push device and revokes the server session.
- Each authenticated client session receives a fresh TanStack Query client. Account changes clear/cancel the previous cache; protected routes wait for authentication and the management-scope bootstrap to finish.
- The client synchronizes login/logout changes across tabs via the non-secret `nexus_auth_session_id` localStorage key and revalidates `/me` when another tab changes the session. Access/refresh tokens remain in localStorage; this retains the documented XSS exposure and should be revisited for production hardening.
- Session or management-context changes invalidate late API responses; a stale response or `401` cannot populate the next user's cache or trigger refresh/clear against the next user's credentials.
- TanStack Query retries are disabled for authorization failures and other client errors (`shouldRetry` in `apps/web-react/src/api/queryClient.ts`).

## Development bootstrap

A fresh Development seed creates the protected `admin@nexus.com` account and tenant role templates; the templates are not additional login accounts. The seed requires `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` from a secure Development secret source for a new or legacy-hash Admin account, hashes it with the registered ASP.NET Core password hasher, sets the configured expiry, and requires the account to change that password at first login. A seed rerun preserves an existing current Admin hash and all password-change/expiry state, including a missing or elapsed temporary-password expiry; login continues to reject missing or elapsed expiry until an explicit recovery or the temporary Development-only bypass below. No shared development password is embedded or seeded.

To replace an expired bootstrap credential, use the explicit local `--recover-development-admin-password` operation in an interactive Development console. It verifies the expected SQL Server/database identity and pending migrations. A backup may be created when explicitly requested, but independent backup storage and `RESTORE VERIFYONLY` are not prerequisites; a missing backup must not block this Development operation. The temporary and permanent passwords are entered twice with console echo disabled and are never accepted as arguments, environment variables, source values, or log fields. Recovery changes only the Admin password lifecycle fields, revokes that user's sessions, and writes an audit entry without credential data. It then exercises temporary login, mandatory password change, and login with the new permanent password through the application authentication service. This is a local Development operator action, not an API endpoint or an application role permission.

For temporary UAT access, `Security:LocalPasswords:DevelopmentAdminTemporaryPasswordBypassEnabled` defaults to `false` in `appsettings.Development.json`. When enabled, the authentication service and password-enforcement middleware honor it only when the host is Development and the account matches the seeded Admin email, seed tenant, and Local authentication source. The existing password hash must still verify. While enabled, login, refresh, `/api/auth/me`, and authenticated API requests do not enforce that Admin's `MustChangePassword` or temporary expiry. The database flags, expiry timestamp, hash, roles, and grants are not changed. Set the option back to `false` (or remove its environment override) after UAT; configuration reload applies the file change, while an environment-variable change requires restarting the API. Production and all other accounts continue using the normal policy.

## Key Files

- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DemoAuthService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs`
- `apps/api/src/NEXUS.Modules.Identity/ModuleRegistration/IdentityModuleRegistration.cs`
- `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs`
- `apps/api/src/NEXUS.Persistence/AppDbContext.cs` (`ConfigureTenantResolution`, `GetCurrentTenantId`)
- `apps/web-react/src/api/client.ts`
- `apps/web-react/src/auth/AuthProvider.tsx`
- `apps/web-react/src/features/home/LoginPage.tsx`

See also [SECURITY-ARCHITECTURE.md](../architecture/SECURITY-ARCHITECTURE.md).
