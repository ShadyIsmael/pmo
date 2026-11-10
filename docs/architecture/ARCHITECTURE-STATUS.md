# Architecture Status

## Current Frontend Authority

React/Vite is the only supported frontend. apps/web-react uses React Router, builds with Vite, runs locally on port 4100, and is served by nginx on Docker host port 4000.

---

## Overall Maturity: Foundation Phase

NEXUS PMO is in the **Foundation Phase** -- a functional shell with working dashboards, navigation, extensive mock data, 19 backend modules with domain entities and controllers, and demo authentication. Most backend endpoints return incomplete or in-memory data.

---

## Frontend

| Metric | Value | Evidence |
|--------|-------|----------|
| Framework | React 18, Vite 6, React Router 6 | apps/web-react/package.json |
| Routes | React Router route tree | apps/web-react/src/routes/AppRoutes.tsx |
| UI | Shared React components and feature screens | apps/web-react/src/ |
| API | Shared typed API client and endpoint map | apps/web-react/src/api/ |
| State and data | TanStack React Query | apps/web-react/package.json |
| Localization | English and Arabic message catalogs | apps/web-react/src/i18n/messages/ |
| Local development | Vite on port 4100 | apps/web-react/vite.config.ts |
| Docker | Static Vite build served by nginx on host port 4000 | apps/web-react/Dockerfile |

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
| **Partial** | 5 | Workflow Engine (entities exist, execution engine not verified), Meetings (schema delivery, SMTP configuration, external-recipient authorization, and owner verification pending), Notifications (in-app, Push, and SMTP Email delivery require runtime configuration and verification), Integrations (entities exist, no real sync adapters), AI Planning (entities exist, MockAIProvider) |
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
| **Seed data** | Deterministic Development seed; a fresh identity bootstrap creates the protected tenant Admin account and role templates | `SeedDataProvider.cs` |
| **Concurrency** | RowVersion (byte array) on BaseEntity | `BaseEntity.cs` |
| **Soft delete** | Global query filter for ISoftDeletable | `AppDbContext.cs:162-164` |
| **Tenant filter** | Request-scoped authenticated tenant claim; fails closed on invalid/missing request context | `AppDbContext.cs` |

---

## Authentication

| Aspect | Status | Evidence |
|--------|--------|----------|
| **JWT Bearer** | Implemented | `Program.cs:59-73` |
| **Development bootstrap** | A fresh seed creates the tenant Admin account only; role templates are not shared demo login accounts | `DemoAuthService.cs`, `SeedDataProvider.cs` |
| **Password** | ASP.NET Core `PasswordHasher<User>` (seeded development credential defined in `SeedDataProvider`) | `SeedDataProvider.cs` |
| **Role switching** | Compatibility endpoint returns the current user and does not switch roles | `AuthController.cs`, `DemoAuthService.cs` |
| **Permission claims in JWT** | Login-time snapshot refreshed from persisted effective grants and direct overrides on every authenticated request | `DemoAuthService.cs`, `UserPermissionClaimsMiddleware.cs`, `UserEffectivePermissionService.cs` |
| **Authorization policies** | Seven canonical Resource policies configured | `Program.cs` — `Resource.View`, `Resource.Create`, `Resource.Edit`, `Resource.Delete`, `Resource.Assign`, `Resource.ViewCostRate`, `Resource.EditCostRate` |
| **Resource Management controllers** | Permission-protected | 6 controllers updated with policy-based `[Authorize]` attributes |
| **Production auth** | Not implemented | Demo accounts only; JWT key must be supplied per environment (`Jwt__Secret`) |

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
| **Email** | Shared SMTP sender and durable NotificationDelivery worker implemented; requires deployment SMTP settings and real-provider verification | `Notifications/Infrastructure/SmtpEmailNotificationChannel.cs`, `NotificationDeliveryProcessor.cs`, `NotificationDeliveryWorker.cs` |
| **Push** | Durable asynchronous delivery, retries, device attempts, and FCM adapter; requires Firebase server configuration and browser consent | `NotificationDeliveryWorker.cs`, `NotificationDeliveryProcessor.cs`, `FirebaseNotificationChannel.cs` |
| **AI Provider** | Mock (`MockAIProvider`) | `AIPlanning/Infrastructure/MockAIProvider.cs` |
| **File Storage** | Placeholder (`LocalStorageProvider`) | `Documents/Infrastructure/LocalStorageProvider.cs` |
| **Enterprise Identity** | Placeholder (v0.1.0 manifest) | `plugins/enterprise-identity-placeholder/plugin.json` |
| **ERP** | Placeholder (v0.1.0 manifest) | `plugins/erp-integration-placeholder/plugin.json` |

---

## Testing

| Aspect | Status | Evidence |
|--------|--------|----------|
| **Unit tests (Resource Management)** | Test project exists; execution pending consolidated final pass | `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests` |
| **API integration tests** | Test project exists; execution pending consolidated final pass | `apps/api/tests/NEXUS.Api.Tests` |
| **E2E tests** | None found | No Playwright/Cypress config discovered |
| **Frontend tests** | Vitest configured; execution pending consolidated final pass | `apps/web-react/package.json`, `apps/web-react/src/test` |

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
| **Compose services** | 5 (sqlserver, redis, api, React/Vite web, worker) | Implemented |
| **API Dockerfile** | Multi-stage, .NET 10 images + net10.0 TFM | Resolved (ADR-005) |
| **Web Dockerfile** | Multi-stage, Vite static build served by nginx | Implemented |
| **Worker Dockerfile** | Same as API, `--worker-mode` entrypoint | Implemented (no worker logic) |
| **Health checks** | SQL Server + Redis | Implemented |

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
| Tenant isolation | Implemented in source; live database/runtime coverage pending | Authenticated tenant claims are validated per request; query filters fail closed on missing or malformed context |
| Mock data embedded in UI | High | Frontend shows fake data; no API integration verified |
| External identity integration | Medium | Local password and session authentication are implemented; enterprise identity provider integration remains deferred |
| Live migration state | High until verified | EF migrations are present and applied by Development initialization; the target database's current history and physical schema must be verified before a change. Development/UAT backups are optional, but every operation still requires exact target, migration, and schema checks. A destructive Development `Shady/NexusPmo` reset still requires explicit Product Owner authorization, all EF migrations, and the full seed. |
| Plugin framework is manifest-only | High | No runtime plugin loading |
| Final test execution | High until verified | Source and test projects exist; the requested consolidated verification pass is pending |
| Docker/.NET version mismatch | Resolved | SDK 10.0 images building net10.0 code (ADR-005) |
| All integrations are mocked | Medium | No real external system connectivity |
