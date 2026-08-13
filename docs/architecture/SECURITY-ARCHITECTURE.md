# Security Architecture

---

## Authentication

### JWT Bearer Tokens

| Aspect | Detail |
|--------|--------|
| **Scheme** | JWT Bearer (Bearer) |
| **Secret** | `NEXUS-Demo-Secret-Key-Minimum-16-Chars!` (hardcoded in `appsettings.json`) |
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
| Hardcoded JWT secret | Critical | `"NEXUS-Demo-Secret-Key-Minimum-16-Chars!"` in `appsettings.json` |
| No HTTPS enforcement | High | `ASPNETCORE_URLS=http://+:5000` in Dockerfile |
| No rate limiting | High | No rate limiting middleware found |
| No CORS restrictions (methods/headers) | Medium | `AllowAnyHeader().AllowAnyMethod()` in `Program.cs` |
| Hardcoded DB password | High | `nexus_dev_2024` in `docker-compose.yml` |
| No audit auto-logging | Medium | Audit entities exist but no automatic `SaveChanges` interceptor |
| No secrets management | High | No vault/KeyVault integration |
| No password policy | Medium | No password complexity, expiry, or lockout logic |
| No MFA | High | No multi-factor authentication support |
| Refresh tokens in localStorage | Medium | Frontend stores access + refresh tokens in localStorage (XSS risk); cookies preferred for production |
