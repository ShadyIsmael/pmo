# ADR-001: Modular Monolith Architecture

**Status:** Accepted  
**Date:** 2025-01-15  
**Author:** Architecture Team  

---

## Context

NEXUS PMO is an enterprise-scale Project Portfolio Management platform expected to support hundreds of concurrent users, multiple tenants, and a growing set of features. The architecture must balance:

- **Development velocity** -- rapid feature delivery in early stages
- **Scalability** -- ability to handle growing load and complexity
- **Maintainability** -- clean separation of concerns as the codebase grows
- **Future-proofing** -- eventual migration to microservices should be possible without rewrites

Options considered:
1. **Full microservices** -- each module as independent service
2. **Traditional monolith** -- single application with no module boundaries
3. **Modular monolith** -- single deployment with strict module isolation

---

## Decision

Adopt a **Modular Monolith** architecture with the following constraints:

- **Single deployment unit** -- all modules compile into one ASP.NET Core application
- **Strict module boundaries** enforced by:
  - Namespace separation (`NEXUS.Modules.{Name}`)
  - Each module owns its domain, application logic, and API controllers
  - Modules communicate via well-defined interfaces and domain events only
- **Shared Kernel** (`NEXUS.SharedKernel`) for common base classes, value objects, and cross-cutting interfaces
- **Module Registration pattern** -- each module exposes `IServiceCollection.Add{Module}Module()` extensions
- **No direct cross-module dependencies** -- communication through contracts in Application layer
- **Future extraction path** -- any module can be moved to a separate microservice without business logic changes

### Module Structure

```
NEXUS.Modules.{Name}/
├── Domain/            # Entities, value objects, domain events
├── Application/       # Services, commands, queries, DTOs, interfaces
├── Api/               # Controllers, route definitions
├── Infrastructure/    # Repository implementations, external service adapters
└── ModuleRegistration/ # DI registration extension methods
```

---

## Consequences

### Positive
- **Simplified DevOps** -- one application to build, test, deploy, and monitor
- **Faster development** -- no inter-service coordination for cross-cutting changes
- **Atomic deployments** -- no version compatibility issues between modules
- **Single database** -- transactions span modules naturally, no distributed transaction headaches
- **Refactoring-friendly** -- module boundaries are logical, can be adjusted easily
- **Performance** -- no network latency for cross-module calls

### Negative
- **Scaling coupling** -- all modules scale together (can be mitigated by extracting high-load modules later)
- **Technology lock-in** -- all modules share the same technology stack
- **Onboarding complexity** -- developers must understand module boundary discipline
- **Build time** -- longer than microservices (solved with incremental builds)

### Mitigation
- Module boundaries are strictly enforced in code reviews
- Performance monitoring identifies extraction candidates early
- CI/CD pipeline runs module-level and integration tests independently
- Domain events provide async processing path for non-critical operations

### Extraction Path
When a module needs to be extracted to a microservice:
1. Module already has its own domain model, database schema ownership, and API
2. Extract API into separate ASP.NET Core project
3. Move shared database tables to new service's database
4. Replace in-process event bus with message queue (RabbitMQ/Kafka)
5. Update cross-module service calls to HTTP/gRPC

**Cost of extraction:** Low to medium -- significantly less than extracting from a traditional monolith.
