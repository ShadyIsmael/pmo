# Management perspective foundation

Phase 1 establishes management presentation context independently from identity, scope, and permissions.

## Authoritative assignments

- Project Manager: `Project.ProjectManagerId`, with history/current state in `ProjectManagerAssignment`.
- Program Manager: `Program.ProgramManagerId`, with history/current state in `ProgramManagerAssignment`.
- Portfolio Manager: `Portfolio.OwnerId`, with history/current state in `PortfolioManagerAssignment`.

No migration is required; all three relationships already exist in the domain model.

## Resolution

`IManagementScopeResolver` is defined in the shared kernel and implemented in persistence. It derives native perspectives from tenant-scoped assignments and resolves inherited scope as follows:

- Portfolio: assigned portfolios, their programs, their program projects, and direct portfolio projects.
- Program: assigned programs and their projects.
- Project: assigned projects.

Multiple assignments are returned in deterministic name order. The default native perspective is the highest assigned management level.

The API exposes `GET /api/me/perspectives`. A selected context is transported with validated `X-Nexus-*` request headers; it never changes the authenticated identity or token.

## Read-only View-As

`ManagementViewMode.ReadOnlyViewAs` is accepted only for a downward hierarchy transition and requires a selected program/project context. `ManagementContextMiddleware` validates the context after authentication and centrally returns `403` for POST, PUT, PATCH, and DELETE requests while preserving the actual user and tenant in the request context/log entry. Direct portfolio, program, and project URLs are scope-checked server-side.

The frontend keeps context in memory, sends it on API requests, exposes `canMutate = permission && !effectiveReadOnly` as the shared mutation gate, hides the global Create action while viewing read-only, and shows the selected view with an EN/AR read-only indicator. Existing PM routes remain the landing experience for project context; program and portfolio dashboard work is intentionally deferred.
