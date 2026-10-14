# Portfolio Management Core

The Portfolio module provides tenant-isolated SQL Server CRUD for portfolio governance. The NEXUS portfolio screen reads exclusively from authenticated APIs through TanStack Query and includes loading, error, empty, search, and filter states.

## Data and validation

Portfolio records contain name, unique tenant-scoped code, description, status, owner, sponsor, dates, budget, currency, priority, health/RAG, confidence, strategic objective, and notes. Base entity fields supply tenant, creation/update audit values, soft-delete support, and row-version concurrency. Name/code, date order, non-negative budget, three-letter currency, active tenant-user references, and unique codes are validated by the service and database index.

## Operations

- `GET /api/portfolios` supports paging plus `search`, `status`, `priority`, and `health` filters.
- `GET /api/portfolios/{id}` returns resolved owner/sponsor names and program count.
- `POST /api/portfolios` creates a portfolio.
- `PUT /api/portfolios/{id}` edits a portfolio.
- `POST /api/portfolios/{id}/archive` archives a portfolio.
- `POST /api/portfolios/{id}/reactivate` reactivates an archived portfolio.

All endpoints require authentication. Global EF query filters enforce tenant and soft-delete isolation. The UI uses the shared `FormSheet` for create/edit and the real user lookup for searchable Owner and Sponsor selection.

An active user assigned the tenant system `admin` role may read unassigned portfolios within the current tenant when `Portfolio.View` is granted. Updating an unassigned portfolio is permitted only for the exact `PUT /api/portfolios/{id}` route, in native mode, and only when the caller also has `Portfolio.Manage`; the service applies an explicit current-tenant predicate. This does not expand archive/reactivate or cross-tenant access. Users without this administrator scope remain restricted to their resolved management assignments.

## Project integration

Projects retain their existing optional `PortfolioId` relationship and selector. Portfolio detail loads linked projects from `GET /api/projects?portfolioId={id}`; no project-manager or resource behavior is changed.

## Schema

Migration `PortfolioManagementCore` adds sponsor, currency, confidence, strategic objective, and notes columns, plus tenant/status and filtered tenant/code indexes.
