# NEXUS PMO

### Plan. Govern. Execute. Optimize.

**Enterprise Project Portfolio Management Platform**

NEXUS PMO is a comprehensive project portfolio management (PPM) platform designed for enterprise-scale organizations. It provides end-to-end capabilities for managing portfolios, programs, projects, resources, costs, risks, and governance through a unified, modular architecture.

---

## Architecture Overview

NEXUS PMO follows a **Modular Monolith** architecture with **Clean Architecture** and **Domain-Driven Design (DDD)** principles. The system is composed of 19 backend modules, each with its own domain, application, and API layers, all compiled into a single deployment unit with strict module boundaries.

- **Frontend:** React 18, Vite 6, React Router 6, TypeScript, Tailwind CSS, shadcn/ui
- **Backend:** .NET 10 LTS, ASP.NET Core, Entity Framework Core, MediatR
- **Database:** PostgreSQL 16, Redis 7 (caching/queues)
- **Communication:** RESTful API, JWT authentication, domain events via MediatR

---

## Quick Start

### Prerequisites

- Node.js 18+ 
- .NET 10 SDK
- PostgreSQL 16
- Redis 7
- Docker & Docker Compose (optional)

### Local Development Setup

```bash
# 1. Clone repository
git clone <repo-url>
cd pmo

# 2. Install frontend dependencies
npm install
npm --prefix apps/web-react ci

# 3. Restore .NET packages
cd apps/api && dotnet restore && cd ../..

# 4. Start infrastructure
docker compose up -d postgres redis

# 5. Apply database migrations through scripts/database/ef-database-gate.ps1
#    after verifying the Development target and reviewing the generated SQL.
#    API startup does not migrate or seed by default in Development.

# 6. Start the API
npm run dev:api

# 7. Start the frontend (in a separate terminal)
npm run dev:web

# 8. Open browser
#    Frontend: https://localhost:4100
#    API Swagger: https://localhost:5001/swagger
```

### Development Administrator Bootstrap

Explicit Development seeding creates the protected tenant Admin role and the `admin@nexus.com` account. It does not create shared-password demo users. Set `NEXUS_ADMIN_BOOTSTRAP_PASSWORD` through a secure Development secret source before the first seed; the value must contain at least 12 characters. The configured temporary-password expiry defaults to 24 hours, and the seeded account must change its password at first login. Ordinary seed reruns preserve the existing Admin hash and password-change/expiry state; standalone seed operations require the ordinary verified-backup gate. The only no-backup seed is the mandatory immediate seed step of an explicitly Product Owner-authorized reset of Development `Shady/NexusPmo`, after exact environment/server/database verification. For temporary UAT access, `Security:LocalPasswords:DevelopmentAdminTemporaryPasswordBypassEnabled` is false by default and can be enabled only for the seeded Admin in Development; it does not change stored credentials or grants and is ignored in Production. Set it back to false after UAT. An expired bootstrap credential can also be recovered through the local Development-only interactive recovery command, which requires an independently verified backup and uses the application password hasher. No administrator password is stored in source control.

---

## Navigation

The sidebar provides access to all major areas:

- **Home** - Dashboard and KPIs
- **My Work** - Tasks, approvals, timesheets
- **Portfolios** - Portfolio overview, programs, optimization
- **Projects** - All projects, Gantt, Board, Matrix views, templates
- **Resources** - Directory, capacity planning, utilization
- **Costs** - Budgets, forecasts, actuals
- **Risks & Issues** - Risk register, issue log, risk matrix
- **Meetings** - Calendar, action items, minutes
- **Workflows** - Workflow engine
- **Integrations** - External system connections
- **AI Workspace** - AI copilot, plan generator, insights
- **Reports** - Dashboards, portfolio reports, executive reports
- **Documents** - Document management
- **Plugins** - Plugin marketplace
- **Administration** - Users, roles, settings, plugins, license
- **Security & Audit** - Audit log, security policies

---

## Modules

| Module | Status | Description |
|--------|--------|-------------|
| Identity | Implemented | Authentication, authorization, role management |
| Organization | Implemented | Company structure, business units, departments, teams |
| Portfolio | Implemented | Portfolio management, programs, strategic objectives |
| Project Management | Implemented | Projects, milestones, baselines, deliverables |
| Work Items | Implemented | Tasks, bugs, stories, epics with agile workflows |
| Resource Management | Implemented | Resources, allocations, capacity, skills, rate cards |
| Cost Management | Implemented | Budgets, cost entries, invoices, commitments |
| Risk Management | Implemented | Risk register, mitigation, contingency planning |
| Issue Management | Implemented | Issue tracking, categorization, resolution |
| Meetings | Implemented | Meeting scheduling, agendas, minutes, actions |
| Workflow Engine | Partial | Workflow templates, instances, transitions, SLA |
| Approvals | Implemented | Approval requests, multi-level approval, delegation |
| Notifications | Implemented | In-app notifications, templates, preferences |
| Documents | Implemented | Document storage, versioning, reviews |
| Reporting | Implemented | Report definitions, schedules, exports |
| Audit | Implemented | Audit trail, activity logging |
| Integrations | Partial | External system connections, sync, webhooks |
| Plugin Management | Implemented | Plugin registry, tenant activation |
| AI Planning | Partial | AI-powered plan generation, analysis |

---

## Plugins

| Plugin | Status | Description |
|--------|--------|-------------|
| Jira Integration | Placeholder | Bi-directional sync with Jira |
| Microsoft Project | Partial | MPP import is implemented in the Project Planning adapter; MPP export is not available |
| Internal Ticketing | Placeholder | Internal helpdesk ticketing system |
| Test Management | Placeholder | Test case management and execution |
| Mobile Workspace | Placeholder | Native mobile application |
| Enterprise Identity | Placeholder | SSO, AD/LDAP integration |
| ERP Integration | Placeholder | SAP, Oracle ERP integration |

---

## Technology Stack

### Frontend
- **Framework:** React 18 with Vite 6 and React Router 6
- **Language:** TypeScript 5
- **UI Library:** React 18
- **Component System:** shadcn/ui (Radix UI primitives)
- **Styling:** Tailwind CSS 3
- **State Management:** TanStack React Query 5
- **Forms:** React Hook Form + Zod
- **Tables:** TanStack React Table
- **Charts:** Recharts
- **Internationalization:** English and Arabic React message catalogs
- **Theme:** shared React theme configuration
- **HTTP Client:** Fetch API (wrapped)

### Backend
- **Runtime:** .NET 10 LTS
- **Framework:** ASP.NET Core 10
- **ORM:** Entity Framework Core 10
- **Database:** PostgreSQL 16 (primary), SQL Server (fallback)
- **Caching:** Redis 7
- **Authentication:** JWT Bearer tokens
- **CQRS/Events:** MediatR
- **API Documentation:** Swagger / OpenAPI
- **Logging:** Built-in ILogger

### Infrastructure
- **Containerization:** Docker & Docker Compose
- **CI/CD:** GitHub Actions (configured)
- **Monitoring:** Health checks endpoint

---

## Project Structure

```
C:\Projects\pmo
├── apps/
│   ├── api/                          # .NET 10 ASP.NET Core API
│   │   └── src/
│   │       ├── NEXUS.Api/            # API host (Program.cs, middleware)
│   │       ├── NEXUS.SharedKernel/   # Base classes, interfaces, value objects
│   │       ├── NEXUS.Persistence/    # EF Core DbContext, migrations, seed
│   │       ├── NEXUS.Modules.Identity/
│   │       ├── NEXUS.Modules.Organization/
│   │       ├── NEXUS.Modules.Portfolio/
│   │       ├── NEXUS.Modules.ProjectManagement/
│   │       ├── NEXUS.Modules.WorkItems/
│   │       ├── NEXUS.Modules.ResourceManagement/
│   │       ├── NEXUS.Modules.CostManagement/
│   │       ├── NEXUS.Modules.RiskManagement/
│   │       ├── NEXUS.Modules.IssueManagement/
│   │       ├── NEXUS.Modules.Meetings/
│   │       ├── NEXUS.Modules.WorkflowEngine/
│   │       ├── NEXUS.Modules.Approvals/
│   │       ├── NEXUS.Modules.Notifications/
│   │       ├── NEXUS.Modules.Documents/
│   │       ├── NEXUS.Modules.Reporting/
│   │       ├── NEXUS.Modules.Audit/
│   │       ├── NEXUS.Modules.Integrations/
│   │       ├── NEXUS.Modules.PluginManagement/
│   │       └── NEXUS.Modules.AIPlanning/
│   ├── web-react/                          # React + Vite frontend
│   │   └── src/
│   │       ├── routes/                  # React Router configuration
│   │       ├── components/           # Shared components
│   │       │   ├── layout/           # Shell (Sidebar, Header, etc.)
│   │       │   └── ui/              # shadcn/ui primitives
│   │       ├── lib/                  # Utilities, navigation, permissions
│   │       └── i18n/             # English and Arabic message catalogs
│   └── mobile-placeholder/           # Future mobile app
├── modules/                          # Module documentation/interfaces
├── plugins/                          # Plugin source code
├── packages/
│   ├── shared-types/                 # Shared TypeScript types
│   ├── ui/                           # Shared UI components
│   ├── config/                       # Shared configuration
│   ├── localization/                 # i18n resources
│   └── api-client/                   # API client library
├── services/
│   └── background-worker/            # Background job processor
├── scripts/                          # Development/build scripts
├── docker-compose.yml                # Infrastructure services
└── docs/                             # Documentation
```

---

## Useful Commands

| Command | Description |
|---------|-------------|
| `npm run dev` | Start frontend dev server |
| `npm run dev:api` | Start API dev server |
| `npm run dev:web` | Start frontend dev server |
| `npm run build` | Build all |
| `npm run lint` | Lint frontend |
| `npm run typecheck` | TypeScript type checking |
| `npm run test` | Run all tests |
| `npm run seed` | Seed database |
| `npm run docker:up` | Start Docker services |
| `npm run docker:down` | Stop Docker services |

---

## Docker Setup

```bash
# Start all services (PostgreSQL, Redis, API, Web, Worker)
npm run docker:up

# View logs
docker compose logs -f api web

# Stop all services
npm run docker:down
```

Services:
- **PostgreSQL 16** on port 5432
- **Redis 7** on port 6379
- **NEXUS API** on port 5000
- **NEXUS React/Vite SPA** on Docker host port 4000; local Vite uses 4100
- **Background Worker** (internal)

---

## Environment Configuration

### Frontend (`apps/web-react/.env.local`)
```env
# Leave VITE_API_URL unset for the local same-origin HTTPS proxy.
```

### API (`apps/api/src/NEXUS.Api/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=nexus_pmo;Username=nexus;Password=<set via environment>",
    "SqlServer": "Server=.;Database=NEXUS_PMO;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Secret": "<not stored in source; supply through Jwt__Secret>",
    "Issuer": "NEXUS",
    "Audience": "NEXUS-API"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

---

## Documentation

For detailed documentation, see the [docs](./docs) directory:

- [Architecture Overview](../architecture/README.md)
- [Module Catalogue](../architecture/module-catalogue.md)
- [Plugin Architecture](../architecture/plugin-architecture.md)
- [Permission Model](../architecture/permission-model.md)
- [Multi-Tenancy Strategy](../architecture/multi-tenancy.md)
- [API Conventions](../api/conventions.md)
- [API Endpoints](../api/endpoints.md)
- [Domain Entities](../domain/entities.md)
- [Design Decisions](../decisions/)
- [UI Design System](../ui/design-system.md)
- [Local Development Setup](../development/local-setup.md)
- [Coding Conventions](../development/coding-conventions.md)
- [Implementation Roadmap](../development/roadmap.md)
