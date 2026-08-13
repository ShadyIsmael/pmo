# Frontend API Architecture

The web application uses a centralized API boundary under `apps/web/src/lib/api/`.

- `client.ts` owns HTTP transport, base URL resolution, token storage, refresh/retry, 401 session expiry, and response parsing.
- `endpoints.ts` is the typed endpoint map. Path parameters are encoded by endpoint builders.
- `types.ts` contains shared transport, pagination, and normalized error contracts.
- Domain modules (`projects`, `portfolios`, `resources`, `users`, `handover`, and `notifications`) expose API functions and query-key factories.
- `config.ts` is the environment-backed configuration boundary for API URL, locale, pagination, query freshness, and feature flags.

React Query remains configured in `src/app/providers.tsx`; domain query keys are stable, namespaced, and intended to be used for invalidation from mutations.

The compatibility facade at `src/lib/api-client.ts` re-exports the centralized boundary for legacy imports during migration. New code must import from `@/lib/api` or a domain module. Test fixtures and mock-data files are outside this refactor's scope.
