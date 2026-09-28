# NEXUS PMO

### Plan. Govern. Execute. Optimize.

**Enterprise Project Portfolio Management Platform**

NEXUS PMO is a comprehensive project portfolio management (PPM) platform designed for enterprise-scale organizations. It provides end-to-end capabilities for managing portfolios, programs, projects, resources, costs, risks, and governance through a unified, modular architecture.

---

## Architecture Overview

NEXUS PMO follows a **Modular Monolith** architecture with **Clean Architecture** and **Domain-Driven Design (DDD)** principles. The system is composed of 19 backend modules, each with its own domain, application, and API layers, all compiled into a single deployment unit with strict module boundaries.

- **Frontend:** Next.js 14 (App Router), TypeScript, Tailwind CSS, shadcn/ui
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
cd apps/web && npm install && cd ../..

# 3. Restore .NET packages
cd apps/api && dotnet restore && cd ../..

# 4. Start infrastructure
docker compose up -d postgres redis

# 5. Run database migrations (auto-applied on first run)
#    The API auto-creates the database schema and seeds demo data
#    when running in Development mode.

# 6. Start the API
npm run dev:api

# 7. Start the frontend (in a separate terminal)
npm run dev:web

# 8. Open browser
#    Frontend: http://localhost:3000
#    API Swagger: http://localhost:5000/swagger
```

### Demo Accounts

After seeding, the following demo accounts are available:

| Role | Email | Password |
|------|-------|----------|
| Executive | executive@nexuspmo.local | Demo123! |
| Portfolio Manager | portfolio.manager@nexuspmo.local | Demo123! |
| Project Manager | project.manager@nexuspmo.local | Demo123! |
| Team Member | team.member@nexuspmo.local | Demo123! |
| PMO Administrator | pmo.admin@nexuspmo.local | Demo123! |
| Finance Controller | finance.controller@nexuspmo.local | Demo123! |
| Resource Manager | resource.manager@nexuspmo.local | Demo123! |
| System Administrator | admin@nexuspmo.local | Demo123! |

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
| Microsoft Project | Placeholder | MPP import/export |
| Internal Ticketing | Placeholder | Internal helpdesk ticketing system |
| Test Management | Placeholder | Test case management and execution |
| Mobile Workspace | Placeholder | Native mobile application |
| Enterprise Identity | Placeholder | SSO, AD/LDAP integration |
| ERP Integration | Placeholder | SAP, Oracle ERP integration |

---

## Technology Stack

### Frontend
- **Framework:** Next.js 14 (App Router)
- **Language:** TypeScript 5
- **UI Library:** React 18
- **Component System:** shadcn/ui (Radix UI primitives)
- **Styling:** Tailwind CSS 3
- **State Management:** TanStack React Query 5
- **Forms:** React Hook Form + Zod
- **Tables:** TanStack React Table
- **Charts:** Recharts
- **Internationalization:** next-intl (English + Arabic)
- **Theme:** next-themes (dark/light mode)
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
│   ├── web/                          # Next.js 14 frontend
│   │   └── src/
│   │       ├── app/                  # App Router pages
│   │       ├── components/           # Shared components
│   │       │   ├── layout/           # Shell (Sidebar, Header, etc.)
│   │       │   └── ui/              # shadcn/ui primitives
│   │       ├── lib/                  # Utilities, navigation, permissions
│   │       └── messages/             # i18n translations
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
- **NEXUS Web** on port 3000
- **Background Worker** (internal)

---

## Environment Configuration

### Frontend (`apps/web/.env.local`)
```env
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

### API (`apps/api/src/NEXUS.Api/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=nexus_pmo;Username=nexus;Password=nexus_dev_2024",
    "SqlServer": "Server=.;Database=NEXUS_PMO;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Secret": "NEXUS-Demo-Secret-Key-Minimum-16-Chars!",
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
