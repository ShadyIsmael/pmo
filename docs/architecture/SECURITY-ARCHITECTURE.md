# Security Architecture

---

## Authentication

### JWT Bearer Tokens

| Aspect | Detail |
|--------|--------|
| **Scheme** | JWT Bearer (Bearer) |
| **Secret** | `Jwt:Secret` setting; source includes a local/demo fallback that must be overridden by deployment secrets |
| **Issuer** | `NEXUS` (`appsettings.json`) |
| **Audience** | `NEXUS-API` (`appsettings.json`) |
| **Access-token expiry** | 8 hours (`Jwt:ExpiryHours`) |
| **Refresh-token lifetime** | 7 days (`Jwt:RefreshTokenLifetimeDays`) |
| **Configuration** | `Program.cs` - `AddAuthentication().AddJwtBearer()` with symmetric key signing |

**Evidence:** `Program.cs` configures `AddAuthentication().AddJwtBearer()`.

### JWT Claims

Access tokens embed the following claims:

| Claim | Value |
|-------|-------|
| `sub` (NameIdentifier) | User GUID |
| `email` (ClaimTypes.Email) | User email |
| `unique_name` (ClaimTypes.Name) | Display name |
| `tenantId` | Current tenant GUID |
| `sid` | Persisted `UserSession.Id` for this access-token session |
| `role` | One claim per role name |
| `permission` | One claim per `Resource.Action` permission |

### Authentication Service

| Aspect | Detail |
|--------|--------|
| **Service** | `DemoAuthService` (`Infrastructure/DemoAuthService.cs`) |
| **Interface** | `IAuthenticationService` (`Application/IAuthenticationService.cs`) |
| **Login endpoint** | `POST /api/auth/login` -- validates email/password against DB, returns JWT + refresh token |
| **Refresh endpoint** | `POST /api/auth/refresh` -- rotates the refresh token, returns a new token pair |
| **Logout endpoint** | `POST /api/auth/logout` -- revokes the session and its refresh token |
| **Current-user endpoint** | `GET /api/auth/me` -- returns user + roles + permissions |
| **Password hashing** | ASP.NET Core `IPasswordHasher<User>` (`PasswordHasher<User>`, Identity V3 format) |
| **Refresh-token storage** | SHA-256 hash stored in `UserSession.RefreshToken`; plaintext token never persisted |
| **Session rotation** | `RefreshAsync` revokes the old `UserSession` and creates a new one on every refresh |

**Evidence:** `IdentityModuleRegistration.cs` registers `IPasswordHasher<User>`; `DemoAuthService.cs` implements hashing + rotation.

### Session and authorization isolation

- `UserPermissionClaimsMiddleware` runs after JWT authentication and before authorization. Each authenticated request must carry a valid `sid` whose `UserSession` row matches the token's user and tenant, is not revoked or expired, and belongs to an active, non-deleted user in that tenant. Missing or invalid session binding returns `401`.
- The middleware removes JWT role and permission snapshots and reloads the user's effective role names and permissions from the same tenant before authorization policies execute. Role or direct-permission revocation therefore applies on the next request.
- Refresh revokes the old persisted session and issues the new access token with the replacement session's id. The existing `UserSession.Id` column is reused; no migration is needed.
- In React, `AuthProvider` revalidates `/api/auth/me` before releasing protected UI, scopes React Query to one authenticated session, and clears the previous cache on login/logout/session change. The API client aborts and rejects work started under an older session or management context; those fenced failures are not retried. Cross-tab changes are synchronized using a non-secret localStorage session marker.
- Logout removes the local authenticated view/cache immediately and starts server-session revocation without waiting for best-effort push-device cleanup. An already-authorized server operation may finish, but its stale response is not exposed to the next client session.

Regression coverage is in `apps/api/tests/NEXUS.Api.Tests/Integration/UserPermissionAdministrationTests.cs` and `apps/web-react/src/test/auth-session-isolation.test.tsx`. Compilation, automated test execution, and live browser UAT must be reported separately; compilation alone is not UAT evidence.

### Demo Accounts

All seeded accounts use password `Demo123!` (Development only; seeding is skipped in Production):

| User | Email | Role |
|------|-------|------|
| Alex Admin | `admin@nexus.com` | System Administrator |
| Sarah Executive | `executive@nexus.com` | Executive |
| James Portfolio | `portfolio.manager@nexus.com` | Portfolio Manager |
| Emily Project | `project.manager@nexus.com` | Project Manager |
| Michael Member | `team.member@nexus.com` | Team Member |
| Rachel PMO | `pmo.admin@nexus.com` | PMO Administrator |
| David Finance | `finance.controller@nexus.com` | Finance Controller |
| Lisa Resource | `resource.manager@nexus.com` | Resource Manager |

**Evidence:** `SeedDataProvider.cs` (idempotent identity seed, deterministic GUIDs). Legacy BCrypt hashes (`$2a$`/`$2b$`/`$2y$`) are detected and re-hashed with `PasswordHasher` on reseed.

### Role Switching

| Endpoint | Purpose |
|----------|---------|
| `POST /api/auth/switch-role` | Switch active role context (demo feature) |

---

## Authorization

### Assignment-scoped approval review

An actionable approval assignment may expose only its bounded review projection when the authenticated user is the current assignee, the request is still reviewable, the effective approval permission and tenant match, and the owning object is valid. This is not a general Management Scope exemption: ordinary object reads and mutations remain scope checked. The Change Request exception is limited to its exact pending assignment, review, and canonical decision path, with authorization revalidated by the domain service. Handover decisions continue through the existing accept/reject services; role activation occurs only after acceptance. Knowing an approval identifier is never authorization.

### ASP.NET Core Authorization

| Aspect | Detail |
|--------|--------|
| **Attribute** | `[Authorize]` on controllers |
| **Policy-based** | `Program.cs` calls `AddAuthorization()` -- custom permission policies registered by each module (e.g. `Resource.View`, `Resource.Assign`, `Resource.Manage`, `Resource.ViewCostRate`, `Resource.EditCostRate`) |
| **Pipeline** | `app.UseAuthentication()` + `app.UseAuthorization()` in `Program.cs` |

### Role-Based Permissions

The backend defines roles with associated permissions in `SeedDataProvider.cs`:

| Role | Permissions |
|------|-------------|
| admin | All 15 permissions |
| executive | All except settings |
| portfolio.manager | portfolios, projects, reports |
| project.manager | projects, resources, reports |
| team.member | projects.read only |
| pmo.admin | All 15 permissions |
| finance.controller | finances, reports, projects |
| resource.manager | resources, projects |

**Evidence:** `SeedDataProvider.cs:129-156`

### Frontend Permission Model

`Permissions.ts` defines 14 categories with ~80 permission constants:

| Category | Permissions |
|----------|-------------|
| Project | View, Create, Edit, Delete, Approve, Close, ViewFinancials, EditFinancials |
| Portfolio | View, Create, Edit, Delete, Manage |
| Program | View, Create, Edit, Delete, Manage |
| Task | View, Create, Edit, Delete, Assign, Complete, Reassign |
| Resource | View, Allocate, Deallocate, ManageRates, ViewCapacity |
| Risk | View, Create, Edit, Delete, Mitigate, Escalate |
| Issue | View, Create, Edit, Delete, Resolve, Escalate |
| Meeting | View, Create, Edit, Delete, ManageActions |
| Workflow | View, Create, Edit, Delete, Execute |
| Report | View, Create, Schedule, Export |
| Document | View, Upload, Edit, Delete, Share |
| Notification | View, Manage |
| Ticket | View, Create, Edit, Resolve |
| TestManagement | View, Create, Edit, Execute |
| Integration | View, Configure, Manage |
| Administration | View, ManageUsers, ManageRoles, ManageSettings, ManagePlugins |
| SecurityAndAudit | View, ViewAuditLog, ManagePolicies |
| AICopilot | Access, GeneratePlan, Analyze |
| Costs | View, Manage, Approve |

**Evidence:** `permissions.ts:1-128` (166 lines)

### Frontend Roles (13)

| Role | Key |
|------|-----|
| Executive | `"Executive"` |
| Portfolio Manager | `"Portfolio Manager"` |
| Program Manager | `"Program Manager"` |
| Project Manager | `"Project Manager"` |
| Team Lead | `"Team Lead"` |
| Team Member | `"Team Member"` |
| PMO Director | `"PMO Director"` |
| PMO Analyst | `"PMO Analyst"` |
| Resource Manager | `"Resource Manager"` |
| Financial Controller | `"Financial Controller"` |
| Risk Manager | `"Risk Manager"` |
| Administrator | `"Administrator"` |
| Auditor | `"Auditor"` |

**Evidence:** `permissions.ts:132-146`

---

## Data Scope Authorization

### Current State: Partially Implemented

| Dimension | Status | Evidence |
|-----------|--------|----------|
| **Point of View** (role switching) | Endpoint exists | `POST /api/auth/switch-role` documented |
| **Data Scope** (tenant isolation) | Enforced per request | `GetCurrentTenantId()` resolves the `tenantId` JWT claim; falls back to the demo tenant outside an HTTP context (`AppDbContext.cs`) |
| **Field-level permissions** | Constants defined, NOT enforced | `Project.ViewFinancials`, `Costs.Approve` in `permissions.ts` |

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
| Tenant resolution | `GetCurrentTenantId()` reads the `tenantId` JWT claim via `IHttpContextAccessor` | Implemented |
| Seed tenant ID | `11111111-1111-1111-1111-111111111111` | Fixed in seed data |

**Note:** All seeded users and data share the single demo tenant. Real per-customer tenancy requires tenant resolution at login and per-tenant token issuance.

---

## Production Security Gaps

| Gap | Severity | Detail |
|-----|----------|--------|
| Demo credentials | Critical | Seeded accounts use hardcoded password `Demo123!` (Dev-only; seeding skipped in Production) |
| JWT secret fallback | Critical | A demo fallback remains in source; production must configure a high-entropy secret through a secret store |
| No HTTPS enforcement | High | `ASPNETCORE_URLS=http://+:5000` in Dockerfile |
| No rate limiting | High | No rate limiting middleware found |
| No CORS restrictions (methods/headers) | Medium | `AllowAnyHeader().AllowAnyMethod()` in `Program.cs` |
| Hardcoded DB password | High | `nexus_dev_2024` in `docker-compose.yml` |
| No audit auto-logging | Medium | Audit entities exist but no automatic `SaveChanges` interceptor |
| No secrets management | High | No vault/KeyVault integration |
| No password policy | Medium | No password complexity, expiry, or lockout logic |
| No MFA | High | No multi-factor authentication support |
| Refresh tokens in localStorage | Medium | Frontend stores access + refresh tokens in localStorage (XSS risk); cookies preferred for production |
