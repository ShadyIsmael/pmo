# NEXUS PMO — Agent Context File

## Mandatory Code-Quality Reading

Before any code change, read and follow [CODE-QUALITY-GUIDELINES.md](CODE-QUALITY-GUIDELINES.md). Treat it and this file as mandatory acceptance criteria. Do not start implementation before reviewing both documents.

Frontend tasks must also read [FRONTEND-ENGINEERING-GUIDELINES.md](FRONTEND-ENGINEERING-GUIDELINES.md). Backend-only tasks must not read it unless directly relevant.

## Product
NEXUS PMO: Enterprise Project Portfolio Management Platform
Tagline: Plan. Govern. Execute. Optimize.

## Portfolio Management Core
- Real SQL Server portfolio create, list, detail, edit, archive, and reactivate flows are implemented.
- Portfolio API queries are tenant-isolated, authenticated, audited through base entity fields, validated, searchable, and filterable; portfolio code is unique per tenant.
- Owner and Sponsor use the real active-user lookup. Portfolio detail resolves their names and displays linked projects through the existing project portfolio filter.
- The UI uses TanStack Query and the shared FormSheet with NEXUS loading, error, and empty states. Project-manager/resource selectors and seed data remain independent.

## Actual Stack
- Frontend: Next.js 14.2.35, React 18.3, TypeScript 5.5, Tailwind 3.4, shadcn/ui (Radix), TanStack Query 5.50, TanStack Table 8.19, Recharts 2.12, react-hook-form 7.52, zod 3.23, next-intl 3.26, Lucide 0.400, date-fns 3.6
- Backend: .NET 10 LTS (net10.0 target), ASP.NET Core, EF Core 10, SQL Server (primary)
- Auth: JWT Bearer (access + refresh) with ASP.NET Core PasswordHasher, refresh-token rotation
- Cache: Redis (configured)
- Deploy: Docker Compose (sqlserver:2022, redis:7, api:5000, web:4000)
- i18n: next-intl, English + Arabic

## Repository Layout
apps/
  web/          - Next.js frontend (port 3000)
  api/          - .NET solution (22 projects, port 5000)
  mobile-placeholder/ - empty
packages/
  shared-types/ - TypeScript type definitions
  config/       - App config, navigation, theme, permissions
  localization/ - English and Arabic locale files
  api-client/   - TypeScript API client
docs/           - Architecture, API, domain, decisions, development docs
plugins/        - 7 plugin manifests (manifest-only, no runtime loading)
scripts/        - database/, development/

## Backend Module Boundary Rules
- 22 projects in NEXUS.slnx: 19 modules + Api + Persistence + SharedKernel
- Each module: Domain/Application/Infrastructure/Api/ModuleRegistration layers
- Module registrations are IServiceCollection extensions called in Program.cs
- NO direct cross-module DbContext access - use service interfaces
- All entities inherit BaseEntity (Id, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, RowVersion)
- AppDbContext in NEXUS.Persistence owns ALL entities (single database)
- Soft delete and tenant query filters are global

## Frontend Architecture Rules
- Pages in: src/app/[locale]/(dashboard)/
- Layout hierarchy: RootLayout > Providers > LocaleLayout > DashboardLayout > pages
- "use client" for all interactive pages
- UI components in src/components/ui/ (shadcn/ui style)
- Layout components in src/components/layout/
- Mock data in src/lib/mock-data.ts (used by all pages)
- Navigation config in src/lib/navigation.ts
- Permission constants in src/lib/permissions.ts
- Utils (formatCurrency, formatDate, cn, etc.) in src/lib/utils.ts
- i18n messages in src/messages/en.json and src/messages/ar.json

## Important Contracts
- BaseEntity: /apps/api/src/NEXUS.SharedKernel/BaseEntity.cs
- AppDbContext: /apps/api/src/NEXUS.Persistence/AppDbContext.cs (60+ DbSets)
- Navigation config: /apps/web/src/lib/navigation.ts
- Permission constants: /apps/web/src/lib/permissions.ts
- Mock data: /apps/web/src/lib/mock-data.ts
- API client: /packages/api-client/src/client.ts

## Plugin Rules
- 7 plugins in /plugins/: microsoft-project, jira-integration, internal-ticketing, test-management, mobile-workspace, enterprise-identity-placeholder, erp-integration-placeholder
- Plugins are manifest-only (plugin.json) - no runtime plugin loading
- To add a plugin: create plugin.json + README.md in new directory under /plugins/
- Plugin IDs follow: nexus.plugin.{name}

## Authorization Rules
- Demo accounts seeded in Development (8 role accounts + 92 additional users, password "Demo123!")
- POST /api/auth/login with email/password, POST /api/auth/refresh (refresh-token rotation), POST /api/auth/logout
- JWT Bearer access token (8h) + opaque refresh token (7d, SHA-256 hashed in UserSession.RefreshToken)
- Passwords hashed with ASP.NET Core PasswordHasher<User> (Identity V3); legacy BCrypt hashes migrated on reseed
- Dev-only role switching: POST /api/auth/switch-role
- Permissions defined in frontend permissions.ts (~80 constants, 15 categories)
- Backend [Authorize(Policy = "...")] attribute with 5 Resource policies on 6 controllers
- Permission claims embedded in JWT tokens (`"permission"` claim type)
- Policies: Resource.View, Resource.Assign, Resource.Manage, Resource.ViewCostRate, Resource.EditCostRate
- No data-scope authorization implemented
- Tenant isolation enforced via JWT `tenantId` claim (falls back to demo tenant outside HTTP context)

## Multi-Tenancy Rules
- TenantId on ALL entities (via BaseEntity)
- Global query filter: e => e.TenantId == GetCurrentTenantId() -- resolves `tenantId` JWT claim; outside HTTP context returns demo tenant (11111111-1111-1111-1111-111111111111)
- To enforce real tenancy: issue per-tenant tokens and add tenant resolution at login
- Seed data uses single tenant: 11111111-1111-1111-1111-111111111111

## Current Implementation Status
- Frontend: 57 pages, 25 UI components, 4 layout components - Project Registry and Project Detail now use real APIs (others use embedded mock data)
- Backend: 19 modules structured with entities/services/controllers - Project Management now has full CRUD + team management with real data
- Backend: Project Plan Working Draft persistence foundation added (ProjectPlanDraft entity/table, one active draft per tenant+project, RowVersion concurrency); plan editor, CR submission/approval, and baseline promotion are deferred
- Backend: Initial (first) approved baseline workflow added (ProjectBaselineVersion v1 approved/current + InitialBaseline project snapshot, tenant/audit/concurrency, duplicate rejection); no CR, Working Draft, frontend, or notifications
- Planning: Project Planning domain designed and finalized as a first-class module (`NEXUS.Modules.ProjectPlanning`): versioned relational model (Plan/PlanVersion with Working+Baseline types, activities incl. zero-duration milestones, WBS adjacency tree, deliverables, dependencies, assignments, calendars w/ tenant templates + project overrides). Every plan row belongs to one PlanVersionId; no unversioned live rows; no JSON payloads. Baseline v1 is snapshotted from the pre-baseline Working version and is immutable; post-baseline edits flow only into a Working version (the working draft). A project may contain multiple plans, each with a first-class PlanType (Execution/Recovery/Tender/Construction/Shutdown/Scenario/Custom) with governing rules (Execution-only Start Project; Recovery requires an existing approved baseline; Scenario isolated until promoted; Custom configurable). Change Requests belong to the Project, not to a Plan; plans are only impacted by a CR. Future capabilities (Monte Carlo, What-if, importers, AI scheduling, resource optimization, critical path) extend Plans/PlanVersions only. See docs/architecture/PROJECT-PLANNING-MODEL.md and ADR-007 (Accepted, governing) and ADR-006 (Accepted, basis). Implementation (module skeleton, migration, services, endpoints, tests) is deferred.
- Authorization: 5 permission policies configured on Resource Management controllers; Project policies added; JWT tokens include permission claims
- Auth: Real end-to-end flow (login/refresh/logout/me, PasswordHasher, refresh rotation, JWT claims for roles/permissions/tenantId, protected dashboard routes)
- Plugins: Manifest-only
- Integrations: All mocked (MockAIProvider, LocalStorageProvider)
- Database: SQL Server migrations (Database.MigrateAsync)
- 45 unit tests (Resource Management), InMemory provider, SQL Server integration tests planned
- No CI/CD
- Dockerfiles: .NET 10 images, net10.0 projects (consistent)

## Deferred Capabilities (DO NOT CLAIM AS IMPLEMENTED)
- Real MPP parsing
- Real Jira sync
- Real AI provider calls
- Native mobile app
- Active Directory / Entra ID
- ERP integration
- Production email/push
- Full workflow execution engine
- Data scope authorization
- Multi-tenant login/registration (tenancy is filtered per request but all users share the demo tenant)

## Required Validation Commands
- npm run build (in apps/web)
- dotnet build (in apps/api)
- npm run lint (in apps/web)
- npm run dev (in apps/web) -- starts on port 4000
- dotnet run (in apps/api/src/NEXUS.Api) -- starts on port 5000

## Important File Paths
- Architecture doc: docs/architecture/NEXUS-PMO-ARCHITECTURE.md
- Module catalogue: docs/architecture/MODULE-CATALOGUE.md
- Agent context: docs/architecture/AGENT-CONTEXT.md (this file)
- Auth doc: docs/modules/AUTHENTICATION-AND-IDENTITY.md
- Project Management doc: docs/modules/PROJECT-MANAGEMENT-CORE.md
- Resource Management doc: docs/modules/RESOURCE-MANAGEMENT-CORE.md
- Seed data: apps/api/src/NEXUS.Persistence/SeedDataProvider.cs
- Frontend API client: apps/web/src/lib/api-client.ts
- Frontend auth state: apps/web/src/lib/auth.tsx
- Frontend mock data: apps/web/src/lib/mock-data.ts

## Mandatory Development Constraints
1. Read this file and NEXUS-PMO-ARCHITECTURE.md before making changes.
2. Do NOT rebuild the repository structure.
3. Do NOT create duplicate modules, pages, or components.
4. Respect module data ownership - no direct cross-module database access.
5. Use existing contracts and extension points for new features.
6. Update this file and NEXUS-PMO-ARCHITECTURE.md after architectural changes.
7. Update affected Mermaid diagrams.
8. Update implementation-status matrices.
9. Add or update ADR files for significant decisions.
10. Keep the application runnable after every change.
11. Add regression tests for defect fixes.
12. Do NOT claim implementation without verifying it.
13. Do NOT count generated folders (node_modules, .next, bin, obj) as source files.
14. ALL mock data is in src/lib/mock-data.ts or SeedDataProvider.cs.
15. Any change affecting architecture, modules, plugins, database ownership, integrations, security, deployment, or major domain relationships MUST update the relevant document in docs/architecture.

## Frontend API Layer Update (1 August 2026)
- Centralized frontend transport and endpoint construction under `apps/web/src/lib/api/`.
- Added environment-backed `apps/web/src/lib/config.ts` for API URL, locale, pagination, query freshness, and feature flags.
- Added shared pagination and normalized API error contracts, including validation fields and status categories.
- Added domain API modules and query-key factories for projects, portfolios, resources, users, handover, and notifications.
- Existing `api-client.ts` is now a compatibility re-export; new frontend code should use `@/lib/api`.
