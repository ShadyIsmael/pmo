# ADR-002: Technology Stack

> Historical record: Next.js references below describe the retired frontend and are kept only to preserve the original decision or implementation history. The supported frontend is apps/web-react (React, Vite, and React Router); these references are not build, deployment, agent, or audit targets.

**Status:** Accepted (amended in parts)  \n**Date:** 2025-01-15  \n**Author:** Architecture Team  

---

## Context

Select the technology stack for NEXUS PMO that balances developer productivity, performance, maintainability, and ecosystem maturity. The platform must support:

- Complex domain modeling (DDD)
- Rich, interactive UI with real-time updates
- Enterprise authentication and authorization
- Multi-tenancy
- Plugin architecture
- Extensive reporting and data visualization
- Internationalization (English and Arabic RTL)

---

## Decision

### Frontend

| Technology | Decision | Justification |
|------------|----------|--------------|
| **Framework** | React 18 + Vite + React Router (`apps/web-react`) | Supported frontend per ADR-009; SPA with route-based code splitting |
| **Language** | TypeScript 5 | Type safety across frontend and backend (shared types), reduced runtime errors, excellent tooling |
| **UI Library** | React 18 | Largest ecosystem, concurrent features, server components support |
| **Component System** | shadcn/ui + Radix Primitives | Accessible, unstyled primitives with full Tailwind control; smaller bundle than MUI; consistent with Tailwind ecosystem |
| **Styling** | Tailwind CSS 3 | Utility-first, rapid development, consistent design tokens, built-in dark mode, RTL support |
| **State Management** | TanStack React Query 5 | Server state focus, caching, deduplication, optimistic updates; no need for Redux (most state is server-sourced) |
| **Forms** | React Hook Form + Zod | Performant (uncontrolled), type-safe validation via Zod schemas, minimal re-renders |
| **Tables** | TanStack React Table | Headless, flexible, virtual scrolling support, excellent sorting/filtering |
| **Charts** | Recharts | React-native charting, composable, good customization, supports accessibility |
| **i18n** | next-intl | Next.js-native internationalization, ICU message syntax, RTL support, TypeScript integration |
| **Theme** | next-themes | SSR-safe theme management, system theme detection, no flicker |

### Backend

| Technology | Decision | Justification |
|------------|----------|--------------|
| **Runtime** | .NET 10 LTS | LTS support, excellent performance, mature ecosystem |
| **Framework** | ASP.NET Core 8 | Minimal API, middleware pipeline, dependency injection, excellent OpenAPI support |
| **ORM** | Entity Framework Core 8 | Mature, LINQ provider, migrations, global query filters for multi-tenancy/soft delete |
| **Database** | SQL Server 2022 | Strong EF Core provider, ACID compliance, tenant-scoped shared database |
| **Caching** | Redis 7 | Distributed caching, pub/sub for real-time features, session storage, rate limiting |
| **Authentication** | JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer) | Stateless, scalable, standard, good library support |
| **CQRS/Events** | MediatR | Decouples request/response pipeline, domain event handling pipeline behaviors for cross-cutting concerns (logging, validation) |
| **Mapping** | Manual mapping | Explicit, no magic, perf reasons; automapper avoided for complexity |
| **Validation** | FluentValidation (via MediatR pipeline) | Composable, testable, reusable validation rules |

### Infrastructure

| Technology | Decision | Justification |
|------------|----------|--------------|
| **Containerization** | Docker + Docker Compose | Reproducible environments, CI/CD consistency, developer onboarding ease |
| **CI/CD** | GitHub Actions | Repository integration, marketplace actions, matrix builds, cost-effective |
| **API Documentation** | Swagger / OpenAPI (Swashbuckle) | Industry standard, auto-generated from controllers, testable UI |

---

## Consequences

### Positive
- **TypeScript across stack** -- shared types package (`@nexus-pmo/shared-types`) ensures frontend/backend contract alignment
- **Tailwind + shadcn/ui** -- rapid UI development with consistent design tokens, minimal CSS bloat
- **React + Vite (apps/web-react)** -- supported frontend per ADR-009\n- **EF Core + SQL Server 2022** -- global query filters for multi-tenancy and soft delete at DB level
- **JWT** -- stateless auth suitable for API-first and mobile clients
- **MediatR** -- pipeline behaviors for cross-cutting concerns without polluting business logic

### Negative
- **.NET 8 vs .NET 10** -- not using latest .NET 10, but .NET 8 is LTS
- **No GraphQL** -- REST may require over-fetching in some scenarios (mitigated by rich query parameters)
- **Manual mapping** -- more verbose than AutoMapper but more maintainable and debuggable
- **No SignalR** -- real-time features not yet included (can be added later)

---

## Rejected Alternatives

| Alternative | Reason for Rejection |
|-------------|---------------------|
| React + Vite (SPA) | Superseded: now the supported frontend (apps/web-react) per ADR-009 |
| Angular | Heavier framework, steeper learning curve, less flexible styling |
| Vue.js | Smaller ecosystem for enterprise tooling, fewer UI component libraries |
| MySQL | Weaker EF Core support, fewer advanced features compared to PostgreSQL |
| MongoDB | Document DB not ideal for relational domain model with complex joins |
| GraphQL (Hasura) | Added complexity for API gateway, overkill for current requirements |
| Microservices | Premature; modular monolith provides extraction path without operational overhead |
| RabbitMQ | Unnecessary for current scale; can be introduced when module extraction occurs |
