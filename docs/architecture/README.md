# Architecture Overview

> For task-scoped source maps and the generated dependency index, start at [UPI Architecture Index](INDEX.md). See [UPI usage and limits](UPI-README.md) for refresh behavior and routing commands.

## Architectural Principles

### Modular Monolith
NEXUS PMO is built as a **Modular Monolith** -- a single deployment unit with logically separated modules. This approach balances development velocity with long-term scalability:

- **Single deployment** simplifies DevOps, testing, and operations
- **Strict module boundaries** via namespaces and dependency rules prevent coupling
- **Future extraction** path: any module can be split into its own microservice without rewriting business logic
- **Database schema** is unified (shared SQL Server) but module entities are owned by their respective modules

### Clean Architecture
Each module follows Clean Architecture with four layers:

```
┌─────────────────────────────────────┐
│            API Layer                │  Controllers, DTOs, routing
├─────────────────────────────────────┤
│         Application Layer           │  Services, commands, queries, interfaces
├─────────────────────────────────────┤
│           Domain Layer              │  Entities, value objects, domain events
├─────────────────────────────────────┤
│       Infrastructure Layer          │  Repositories, external services, EF config
└─────────────────────────────────────┘
```

- **Domain Layer** is the innermost circle -- no external dependencies
- **Application Layer** depends only on Domain
- **API Layer** depends on Application
- **Infrastructure Layer** implements interfaces defined in Application/Domain

### Domain-Driven Design (DDD)
- **Aggregates** ensure transactional consistency (e.g., `Project` aggregates `Milestone`, `Deliverable`, `Baseline`)
- **Value Objects** for concepts like `Money`, `DateRange`, `Period`
- **Domain Events** (`BaseDomainEvent` via MediatR) for cross-module communication
- **Rich domain models** with behavior, not anemic data bags

### Event-Driven Communication
Cross-module communication uses:
1. **Domain Events** -- raised by aggregates, handled within or across modules via MediatR notifications
2. **Service Interfaces** -- direct dependency injection for synchronous cross-module calls (e.g., Identity module provides `IAuthorizationService`)
3. **Contracts** -- shared interfaces in `NEXUS.SharedKernel` that define cross-cutting concerns

---

## Layer Descriptions

### Presentation Layer (Frontend)
- **Pages:** React Router route tree in apps/web-react/src/routes/AppRoutes.tsx
- **Components:** React feature screens and shared UI components under apps/web-react/src/
- **Layout Shell:** React application shell and route layouts under apps/web-react/src/
- **Data Fetching:** TanStack React Query through the shared React API client

### API Layer
- **Controllers:** ASP.NET Core `[ApiController]` with `[Route("api/{module}")]`
- **Authorization:** `[Authorize]` attribute with policy-based authorization
- **Input Validation:** Model binding + FluentValidation (via `[ApiController]` behavior)
- **Response Format:** Standard envelope with pagination metadata
- **Error Handling:** Problem Details (RFC 7807)

### Application Layer
- **Services:** Orchestrate business operations, coordinate domain objects
- **CQRS Pattern:** Commands for writes, queries for reads (via MediatR)
- **DTOs:** Data transfer objects for API responses
- **Interfaces:** Contracts for infrastructure dependencies (repositories, etc.)

### Domain Layer
- **Entities:** `BaseEntity` provides `Id`, `TenantId`, audit fields, soft delete, row version
- **Value Objects:** Immutable, equality-by-value (`Money`, `DateRange`, `Period`)
- **Domain Events:** `BaseDomainEvent` for notifying other parts of the system
- **Aggregate Roots:** `IAggregateRoot` interface marks consistency boundaries

### Infrastructure Layer
- **Persistence:** Entity Framework Core with SQL Server
- **Repositories:** Implement interfaces from Application/Domain
- **External Services:** Email, file storage, AI provider integrations
- **Module Registration:** Each module has a static `Add{Module}Module()` extension method

---

## Cross-Module Communication

Modules communicate through three mechanisms:

| Mechanism | Use Case | Example |
|-----------|----------|---------|
| **Domain Events** | Asynchronous notifications | `ProjectCreated` event triggers notification, audit log |
| **Service Interfaces** | Synchronous calls | `IAuthorizationService` from Identity used by all modules |
| **Shared Kernel** | Common contracts | `BaseEntity`, `ITenantEntity`, `ISoftDeletable`, value objects |

### Module Registration Pattern
Each module exposes a static extension method on `IServiceCollection`:

```csharp
// Program.cs
builder.Services.AddProjectManagementModule();
builder.Services.AddResourceManagementModule();
// ...

// Each module's registration
public static class ProjectManagementModuleRegistration
{
    public static IServiceCollection AddProjectManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IMilestoneService, MilestoneService>();
        return services;
    }
}
```

---

## Multi-Tenancy Strategy

- **Database:** Single shared database with tenant-scoped data
- **TenantId Field:** Every entity inherits `TenantId` from `BaseEntity`
- **Global Query Filters:** EF Core `HasQueryFilter` automatically filters by `TenantId`
- **Isolation:** Cross-tenant data access is structurally prevented at the database query level
- **Current State:** Single-tenant ready with full multi-tenant architecture in place

---

## Security Model

- **Authentication:** JWT Bearer tokens (symmetric key)
- **Authorization:** Policy-based with `[Authorize]` attribute
- **RBAC:** Role-based access control with `Role` and `Permission` entities
- **Context-Based Roles:** Users hold different roles in different projects/portfolios
- **Data Scoping:** Authorization evaluates User + Role + Permission + TenantId

---

## Plugin Architecture

- **Extension Points:** Navigation items, dashboard widgets, domain events, API endpoints, background jobs
- **Plugin Lifecycle:** Register, Install, Activate, Deactivate, Uninstall
- **Discovery:** Plugins scanned at startup and registered in `PluginRegistry`
- **Tenant-Specific:** Plugins can be activated per tenant
- **Dependencies:** Plugins declare version compatibility and dependencies

---

## Deployment Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        Docker Host                                │
│                                                                   │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────────────┐  │
│  │   NEXUS Web   │   │  NEXUS API   │   │  Background Worker   │  │
│  │  (React/Vite SPA)    │──▶│ (.NET 10)    │   │  (.NET 10)           │  │
│  │  :3000        │   │  :5000       │   │  (internal)          │  │
│  └──────────────┘   └──────┬───────┘   └──────────────────────┘  │
│                             │                                     │
│  ┌──────────────┐   ┌──────┴───────┐                             │
│  │    Redis 7   │   │  SQL Server   │                             │\n│  │   :6379      │   │  2022        │                             │
│  └──────────────┘   └──────────────┘                             │
└──────────────────────────────────────────────────────────────────┘
```

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Modular Monolith over Microservices | Faster development, simpler operations, easier refactoring; extraction path available |
| SQL Server over PostgreSQL | Current implementation targets SQL Server 2022 (EF Core migrations, docker-compose) |
| MediatR for domain events | Decouples modules, enables cross-cutting concerns (logging, audit) without direct dependencies |
| JWT over session auth | Stateless, scalable, suitable for API-first design |
| shadcn/ui over MUI | Lower bundle size, full styling control, Tailwind-native, accessible by default |
| Clean Architecture | Testability, separation of concerns, framework independence for domain logic |
| Single DB with tenant filter | Simpler than database-per-tenant for current scale, extractable later |
| Plugin system via interfaces | Type-safe, discoverable, hot-pluggable without reflection at runtime |
