# Architecture Status

---

## Overall Maturity: Foundation Phase

NEXUS PMO is in the **Foundation Phase** -- a functional shell with working dashboards, navigation, extensive mock data, 19 backend modules with domain entities and controllers, and demo authentication. Most backend endpoints return incomplete or in-memory data.

---

## Frontend

| Metric | Value | Evidence |
|--------|-------|----------|
| **Framework** | Next.js 14 (App Router) | `apps/web/package.json:14` |
| **Pages** | 57 page.tsx files | Glob scan of `apps/web/src/app/**/page.tsx` |
| **UI Components** | 26 shadcn/ui components | `apps/web/src/components/ui/` directory listing |
| **Data Source** | Mock data (`mock-data.ts`, 633 lines) | `apps/web/src/lib/mock-data.ts:1` -- `// @ts-nocheck - Mock data file; will be replaced by API calls` |
| **Layout Components** | 4 (Sidebar, Header, Breadcrumbs, PageHeader) | `apps/web/src/components/layout/` |
| **State Management** | TanStack React Query 5 (configured, no API fetching verified) | `package.json:42` |
| **Charts** | Recharts | `package.json:44` |
| **Forms** | React Hook Form + Zod | `package.json:39-41` |
| **i18n** | next-intl (EN + AR) | `apps/web/src/messages/en.json`, `ar.json` |
| **Theming** | next-themes (dark/light) | `package.json:46` |

---

## Backend

| Metric | Value | Evidence |
|--------|-------|----------|
| **Runtime** | .NET 10 LTS | All 22 `.csproj` files use `<TargetFramework>net10.0</TargetFramework>` |
| **Projects** | 22 | `NEXUS.slnx` references 22 projects |
| **Modules** | 19 | `Program.cs:99-117` registers 19 module extensions |
| **Entities** | 60+ DbSet properties | `AppDbContext.cs:29-139` |
| **Controllers** | 25 verified | Glob scan of `**/Api/*Controller.cs` |
| **Application Services** | 56 service files | Glob scan of `**/Application/*.cs` |
| **API Documentation** | Swagger/OpenAPI | `Program.cs:36-57` |

### Module Maturity Summary

| Status | Count | Modules |
|--------|-------|---------|
| **Implemented** | 13 | Identity, Organization, Portfolio, ProjectManagement, WorkItems, ResourceManagement, CostManagement, RiskManagement, IssueManagement, Meetings, Approvals, Reporting, Audit |
| **Partial** | 4 | Workflow Engine (entities exist, execution engine not verified), Notifications (in-app plus durable asynchronous Push foundation; Email deferred), Integrations (entities exist, no real sync adapters), AI Planning (entities exist, MockAIProvider) |
| **Mocked** | 0 | (All modules have at least structural code) |
| **Placeholder** | 0 | (No module is completely empty) |
| **Deferred** | 0 | |

---

## Database

| Aspect | Status | Evidence |
|--------|--------|----------|
| **Schema** | Exists in EF Core entity configurations | `AppDbContext.cs:141-159` applies configurations |
| **Migrations** | Development startup applies EF Core migrations, seed data, and workflow provisioning; `--skip-development-database-initialization` skips all three for read-only runtime checks | `Program.cs`, `Migrations/20261012110228_AddNotificationDeliveryFoundation.cs` |
| **Provider** | SQL Server | `Program.cs:205` configures `UseSqlServer` |
| **Seed data** | 591 lines, 8 users, 24 projects, 320 work items, etc. | `SeedDataProvider.cs` |
| **Concurrency** | RowVersion (byte array) on BaseEntity | `BaseEntity.cs` |
| **Soft delete** | Global query filter for ISoftDeletable | `AppDbContext.cs:162-164` |
| **Tenant filter** | Global query filter for ITenantEntity (returns Guid.Empty) | `AppDbContext.cs:167-169` |

---

## Authentication

| Aspect | Status | Evidence |
|--------|--------|----------|
| **JWT Bearer** | Implemented | `Program.cs:59-73` |
| **Demo auth** | Implemented with 8 accounts | `DemoAuthService.cs`, `SeedDataProvider.cs` |
| **Password** | BCrypt hashed (`Demo123!`) | `SeedDataProvider.cs:39` |
| **Role switching** | Endpoint exists | `POST /api/auth/switch-role` |
| **Permission claims in JWT** | Implemented | `DemoAuthService.cs` adds `"permission"` claims for each role permission |
| **Authorization policies** | 5 Resource policies configured | `Program.cs:75-84` — `Resource.View`, `Resource.Assign`, `Resource.Manage`, `Resource.ViewCostRate`, `Resource.EditCostRate` |
| **Resource Management controllers** | Permission-protected | 6 controllers updated with policy-based `[Authorize]` attributes |
| **Production auth** | Not implemented | Hardcoded JWT secret, no enterprise identity |

---

## Plugins

| Aspect | Status | Evidence |
|--------|--------|----------|
| **Manifests** | 7 plugin.json files | `plugins/*/plugin.json` |
| **Runtime loading** | Not implemented | No assembly scanning, no `IPlugin` implementations |
| **Plugin Management module** | Structural (entities, controllers, interfaces) | `AppDbContext.cs:134-136`, `PluginController.cs` |
| **Feature flags** | Defined in manifests, not evaluated in code | `plugin.json` files have `featureFlags` arrays |

---

## Integrations

| Category | Status | Evidence |
|----------|--------|----------|
| **MS Project** | Manifest only | `plugins/microsoft-project/plugin.json` |
| **Jira** | Manifest only | `plugins/jira-integration/plugin.json` |
| **Internal Ticketing** | Manifest only | `plugins/internal-ticketing/plugin.json` |
| **Email** | Deferred; preference field and structural delivery channel only, no sender/worker | `Notifications/Domain/NotificationDelivery.cs`, `NotificationService.cs` |
| **Push** | Durable asynchronous delivery, retries, device attempts, and FCM adapter; requires Firebase server configuration and browser consent | `NotificationDeliveryWorker.cs`, `NotificationDeliveryProcessor.cs`, `FirebaseNotificationChannel.cs` |
| **AI Provider** | Mock (`MockAIProvider`) | `AIPlanning/Infrastructure/MockAIProvider.cs` |
| **File Storage** | Placeholder (`LocalStorageProvider`) | `Documents/Infrastructure/LocalStorageProvider.cs` |
| **Enterprise Identity** | Placeholder (v0.1.0 manifest) | `plugins/enterprise-identity-placeholder/plugin.json` |
| **ERP** | Placeholder (v0.1.0 manifest) | `plugins/erp-integration-placeholder/plugin.json` |

---

## Testing

| Aspect | Status | Evidence |
|--------|--------|----------|
| **Unit tests (Resource Management)** | Not configured | No test project exists in solution |
| **Integration tests (Resource Management)** | Not configured | No test project references in `.slnx` |
| **E2E tests** | None found | No Playwright/Cypress config discovered |
| **Frontend tests** | None found | No Jest/Vitest config in `package.json` |

---

## CI/CD

| Aspect | Status | Evidence |
|--------|--------|----------|
| **GitHub Actions** | Not configured | No `.github/workflows/` directory found |
| **README claim** | "GitHub Actions (configured)" | `README.md:173` -- unverified claim |

---

## Docker

| Aspect | Detail | Status |
|--------|--------|--------|
| **Compose services** | 5 (postgres, redis, api, web, worker) | Implemented |
| **API Dockerfile** | Multi-stage, .NET 10 images + net10.0 TFM | Resolved (ADR-005) |
| **Web Dockerfile** | Multi-stage, standalone Next.js output | Implemented |
| **Worker Dockerfile** | Same as API, `--worker-mode` entrypoint | Implemented (no worker logic) |
| **Health checks** | postgres + redis | Implemented |

---

## Documentation

| Document | Status | Location |
|----------|--------|----------|
| Architecture Reference | Updated | `docs/architecture/NEXUS-PMO-ARCHITECTURE.md` |
| Module Catalogue | Updated | `docs/architecture/MODULE-CATALOGUE.md` |
| Resource Management Core | Created | `docs/modules/RESOURCE-MANAGEMENT-CORE.md` |
| Resource Capacity & Allocation | Updated | `docs/modules/RESOURCE-CAPACITY-AND-ALLOCATION.md` |
| Plugin Architecture | Updated | `docs/architecture/PLUGIN-ARCHITECTURE.md` |
| Domain Model | Created | `docs/architecture/DOMAIN-MODEL.md` |
| Deployment Architecture | Created | `docs/architecture/DEPLOYMENT-ARCHITECTURE.md` |
| Security Architecture | Created | `docs/architecture/SECURITY-ARCHITECTURE.md` |
| Integration Architecture | Created | `docs/architecture/INTEGRATION-ARCHITECTURE.md` |
| Architecture Status | Created | `docs/architecture/ARCHITECTURE-STATUS.md` |
| Multi-Tenancy | Existing | `docs/architecture/multi-tenancy.md` |
| Permission Model | Existing | `docs/architecture/permission-model.md` |
| API Conventions | Existing | `docs/api/conventions.md` |
| API Endpoints | Existing | `docs/api/endpoints.md` |
| Domain Entities (detailed) | Existing | `docs/domain/entities.md` |
| Design Decisions | 3 ADRs | `docs/decisions/adr-*.md` |

---

## Key Risks

| Risk | Severity | Implication |
|------|----------|-------------|
| No tenant isolation enforced | Critical | Multi-tenant deployment broken; all queries return Guid.Empty |
| Mock data embedded in UI | High | Frontend shows fake data; no API integration verified |
| No production authentication | High | Hardcoded JWT secret, demo credentials |
| No database migrations | High | `EnsureCreatedAsync` cannot evolve schema safely |
| Plugin framework is manifest-only | High | No runtime plugin loading |
| No unit tests | High | No regression safety net |
| Docker/.NET version mismatch | Resolved | SDK 10.0 images building net10.0 code (ADR-005) |
| All integrations are mocked | Medium | No real external system connectivity |
