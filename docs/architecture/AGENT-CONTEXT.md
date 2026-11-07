# FOPX PM - Agent Context

## Frontend Authority

The sole supported frontend is apps/web-react, built with React 18, Vite 6, and React Router 6. The former Next.js application in apps/web was retired on 2026-10-27 under ADR-009. Do not recreate, build, deploy, or audit apps/web. New frontend work and dependency audits target apps/web-react only. Historical documents may retain Next.js details as records, not current instructions.

## Mandatory Code-Quality Reading

Before code changes, read CODE-QUALITY-GUIDELINES.md. Frontend tasks must also read FRONTEND-ENGINEERING-GUIDELINES.md.

## Product

Parent company: FOPX
Product: FOPX PM — FOPX Project Management
Tagline: Plan. Organize. Execute.

## Portfolio Management Core

- Portfolio create, list, detail, edit, archive, and reactivate use the API and SQL Server.
- Portfolio queries are tenant-isolated, authenticated, audited, validated, searchable, and filterable.
- Owner and Sponsor use the active-user lookup; detail resolves their names and linked projects.
- The UI uses TanStack Query and shared form sheets with standard loading, error, and empty states.

## Actual Stack

- Frontend: React 18, Vite 6, React Router 6, TypeScript 5.7, Tailwind 3.4, shadcn/ui, TanStack Query and Table, Recharts, React Hook Form, Zod, Lucide, date-fns.
- Backend: .NET 10, ASP.NET Core, EF Core 10, SQL Server.
- Auth: JWT bearer access and refresh tokens with refresh-token rotation.
- Cache: Redis.
- Docker Compose: SQL Server, Redis, API on host port 5000, React/Vite SPA on host port 4000, worker.
- Local Vite development uses port 4100. The React client reads VITE_API_URL and defaults to http://localhost:5000.
- Localization: English and Arabic message catalogs in the React app.

## Repository Layout

- apps/web-react: only supported browser frontend.
- apps/api: .NET solution and API.
- apps/mobile-placeholder: empty placeholder.
- packages: shared definitions and configuration; inspect before changing a package.
- docs: architecture, API, domain, decisions, and development guidance.
- scripts: database and development helpers.

## Frontend Architecture Rules

- Routes: apps/web-react/src/routes/AppRoutes.tsx.
- Product screens and feature APIs: apps/web-react/src/features/.
- Shared API transport and endpoint map: apps/web-react/src/api/.
- Authentication: apps/web-react/src/auth/.
- English and Arabic messages: apps/web-react/src/i18n/messages/.
- Use React Router for navigation and Vite for development and production builds.
- Keep API access, query keys, permissions, formatting, and locale labels in existing shared React modules.
- Do not add Next.js, server components, app-router conventions, or NEXT_PUBLIC environment variables.

## Backend Module Boundary Rules

- The solution contains modules plus API, Persistence, and SharedKernel projects.
- Each module separates Domain, Application, Infrastructure, API, and registration concerns where applicable.
- Module registrations are service-collection extensions called from Program.cs.
- Avoid direct cross-module DbContext access; use service interfaces.
- AppDbContext owns persistence; tenant and soft-delete filters are global where configured.
- Read relevant database documentation and follow the repository's EF migration and seeding workflow before database or migration work. Verify the exact target and preserve production and data safeguards.

## Important Contracts

- BaseEntity: apps/api/src/NEXUS.SharedKernel/BaseEntity.cs.
- AppDbContext: apps/api/src/NEXUS.Persistence/AppDbContext.cs.
- API and persistence ownership remain in apps/api; use module service interfaces for cross-module communication.
- Frontend navigation and permissions: apps/web-react/src/lib/.
- Frontend API client and endpoint map: apps/web-react/src/api/.
- Shared package contracts live under packages/; confirm actual imports before editing.

## Plugin Rules

- Plugins are manifest-only; no runtime plugin loading is implemented.
- Add a plugin through its manifest and documentation under plugins/.
- Plugin IDs follow the nexus.plugin namespace.

## Authorization and Tenant Scope

- Authentication endpoints include login, refresh, logout, and current-user session validation.
- Access tokens carry role, permission, and tenant claims; refresh tokens rotate.
- Use existing authorization policies and frontend permission helpers. Do not infer authorization from UI visibility alone.
- TenantId is present on persisted tenant-owned entities and tenant query filters are applied where configured.
- Read docs/modules/AUTHENTICATION-AND-IDENTITY.md and the security architecture before changing authentication, authorization, or tenancy.

## Current Implementation References

- Use docs/architecture/ARCHITECTURE-STATUS.md for the current stack and deployment inventory.
- Project planning behavior and its historical prototype notes are in docs/modules/PROJECT-PLAN-POV.md; only the persisted React/Vite feature is active.
- Planned payments are documented in docs/modules/FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md.
- Current project status reporting and API contracts are documented in docs/modules/PROJECT-MANAGEMENT-CORE.md and docs/api/endpoints.md.
- Plugin manifests do not imply a runtime plugin host. Consult module-specific docs before claiming a capability is implemented.
- No repository CI workflow is configured.

## Deferred Capabilities

Verify current module documentation before claiming these capabilities: native mobile app, real MPP import/export, live Jira or ERP synchronization, production AI provider calls, full workflow execution, data-scope authorization, and multi-tenant user registration.

## Important Paths

- API startup: apps/api/src/NEXUS.Api/Program.cs.
- EF context: apps/api/src/NEXUS.Persistence/AppDbContext.cs.
- Frontend API client: apps/web-react/src/api/client.ts.
- Frontend authentication: apps/web-react/src/auth/AuthProvider.tsx.
- Frontend routes: apps/web-react/src/routes/AppRoutes.tsx.
- Frontend navigation and permissions: apps/web-react/src/lib/.

## Required Validation Commands

- npm --prefix apps/web-react run typecheck
- npm --prefix apps/web-react run lint
- npm --prefix apps/web-react run test
- npm --prefix apps/web-react run build
- dotnet build apps/api/NEXUS.slnx

## Mandatory Development Constraints

1. Read this file and the relevant module documentation before changes.
2. Do not rebuild the repository structure or create duplicate modules, pages, or components.
3. Respect module data ownership; do not access another module's DbContext directly.
4. Use existing contracts and extension points.
5. Update relevant architecture documentation and diagrams when architecture changes.
6. Update implementation-status matrices when affected.
7. Add or update ADRs for significant decisions.
8. Keep the application runnable and add focused regression tests for defect fixes.
9. Do not claim implementation or verification without evidence.
10. Do not count generated folders such as node_modules, bin, or obj as source files.
11. Treat mock data as feature-local; prefer the API-backed implementation where one exists.
12. Update relevant docs for changes affecting architecture, modules, plugins, database ownership, integrations, security, deployment, or major domain relationships.

## Frontend API Layer Update

- Shared transport and endpoint construction live under apps/web-react/src/api/.
- The client reads VITE_API_URL and defaults to http://localhost:5000.
- Shared pagination and normalized API error contracts are implemented in the React API boundary.
- Domain API modules and query-key factories are kept with their React feature areas.
- Add frontend API behavior to the supported React app only; there is no parallel Next.js implementation.
