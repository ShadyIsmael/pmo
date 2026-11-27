# Portfolio Management Core

The Portfolio module provides tenant-isolated SQL Server CRUD for portfolio governance. The NEXUS portfolio screen reads exclusively from authenticated APIs through TanStack Query and includes loading, error, empty, search, and filter states.

## Data and validation

Portfolio records contain name, unique tenant-scoped code, description, status, owner, sponsor, dates, budget, currency, priority, health/RAG, confidence, strategic objective, and notes. Base entity fields supply tenant, creation/update audit values, soft-delete support, and row-version concurrency. Name/code, date order, non-negative budget, three-letter currency, active tenant-user references, and unique codes are validated by the service and database index.

The Portfolio create/edit sheet resets its draft when closed. Currency is chosen from configured system currency records only; free-form currency codes are unavailable, and creating a Portfolio is blocked while the currency list is loading, unavailable, or empty.

## Operations

- `GET /api/portfolios` supports paging plus `search`, `status`, `priority`, and `health` filters.
- `GET /api/portfolios/{id}` returns resolved owner/sponsor names and program count.
- `POST /api/portfolios` creates a portfolio.
- `PUT /api/portfolios/{id}` edits a portfolio.
- `POST /api/portfolios/{id}/archive` archives a portfolio.
- `POST /api/portfolios/{id}/reactivate` reactivates an archived portfolio.

All endpoints require authentication. Global EF query filters enforce tenant and soft-delete isolation. The UI uses the shared `FormSheet` for create/edit and the real user lookup for searchable Owner and Sponsor selection.

### Initial Manager Selection

Portfolio and Program manager candidates must be active same-tenant System Users with an active assigned `Management`-category role. They must also hold effective `Handover.Approve` and the entity-specific `Portfolio.View` or `Program.View` permission. The actor's `Handover.AdminInitiate` authorization is checked separately from candidate eligibility. Dedicated lookups and the create/handover services revalidate the selected User, tenant, Management category, and effective permissions; no manager role-name filter is used. A Resource record, Project Team membership, or prior management assignment is not required. When a manager is selected, the entity's manager field and current manager-assignment history row are persisted together in the creation transaction. Leaving the selector blank leaves the entity unassigned without creating a current manager-assignment row. After creation, manager changes use the governed handover flow; an incoming manager is rechecked when accepting.

### Manager handover approval review

`GET /api/manager-handovers/pending-for-me` returns pending Program and Portfolio handovers assigned to the authenticated incoming manager. The review routes are `GET /api/manager-handovers/programs/requests/{requestId}/review` and `GET /api/manager-handovers/portfolios/requests/{requestId}/review`. `requestId` is the handover request's internal `Id`, not the owning Program/Portfolio ID or its PublicId. The service rechecks the effective `Handover.Approve` permission, authenticated tenant, incoming-manager assignment, pending status, and owning entity before returning the bounded review projection. It intentionally returns 404 when the request is unavailable to the caller. The approval page chooses the endpoint from the explicit `Program` or `Portfolio` discriminator and validates that the response matches the requested review contract.

An active user assigned the tenant system `admin` role may read unassigned portfolios within the current tenant when `Portfolio.View` is granted. Updating an unassigned portfolio is permitted only for the exact `PUT /api/portfolios/{id}` route, in native mode, and only when the caller also has `Portfolio.Manage`; the service applies an explicit current-tenant predicate. This does not expand archive/reactivate or cross-tenant access. Users without this administrator scope remain restricted to their resolved management assignments.

## Project integration

Programs require one same-tenant Portfolio. Projects support both approved paths: a Program within its Portfolio, or a direct Portfolio assignment with no Program. When a Project has a Program, the service resolves `PortfolioId` from that Program and rejects a conflicting supplied Portfolio. A Project Manager receives assigned projects; a Program Manager receives projects in assigned Programs; a Portfolio Manager receives projects in assigned Programs and direct Projects in assigned Portfolios. Portfolio detail loads both paths from its management read model and `GET /api/projects?portfolioId={id}` remains an explicit Portfolio filter.

The Program create selector calls `GET /api/programs/eligible-portfolios` and requires both `Program.Manage` and `Portfolio.View`. A Portfolio Manager sees active, non-deleted Portfolios from their native assignments, independent of a narrower selected Program or Project perspective. An active user with the authenticated tenant's persisted system `admin` role also sees active, non-deleted Portfolios in that same tenant when both permissions are granted; this is the approved tenant-administrator Program-creation scope and does not cross tenants. `POST /api/programs` requires the same permissions and rechecks the same administrator-or-native-assignment scope plus active status and tenant ownership. Program creation does not create or change Portfolio Manager assignments. Read-only View-As cannot load choices or create Programs. Creating, archiving, or reactivating a Portfolio invalidates this lookup. Program edits retain the existing parent option even when it is no longer eligible for reassignment; this creation exception does not widen Program-update scope.

## Schema

Migration `PortfolioManagementCore` adds sponsor, currency, confidence, strategic objective, and notes columns, plus tenant/status and filtered tenant/code indexes.

## Financial Overview - Sprint 6

Portfolio Financial Overview is read-only and computed from unique authorized Projects. It includes Projects assigned directly to the Portfolio (with no Program) and Projects whose current Program belongs to the Portfolio. Project IDs are deduplicated before aggregation; Program subtotals and direct-Project subtotals are views of the same population and are not added on top of grand totals. Programs with no visible Projects and the direct-Project group are shown with zero contributions. Amounts remain separated by currency; there is no FX conversion or Portfolio-owned balance. Reporting uses the selected Tenant Fiscal Year and current hierarchy, and reports partial coverage when the caller cannot read all descendant Projects. The API requires both `Portfolio.View` and canonical `ProjectFinance.View`, and validates tenant, entity, and Project scope. Financial writes remain Project-only. Compile and runtime verification are deferred; see the [Sprint 6 implementation report](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md).

## Manager assignment root-cause report

See [Manager Appointment Independence](MANAGER-ASSIGNMENT-SYSTEM-USERS.md) for the cross-entity candidate rules and findings.

## Organization ownership integrity

Every new Portfolio must be owned by an active Organization in the authenticated creator's eligible scope. `GET /api/portfolios/eligible-organizations` and `POST /api/portfolios` both require `Portfolio.Create`; the lookup returns only eligible Organization IDs and names. The API selects the sole eligible Organization when one exists, requires an explicit selection when multiple exist, and rejects missing, inactive, cross-tenant, or out-of-scope Organizations. No Portfolio ownership change is exposed through ordinary Portfolio edit.

Programs inherit Organization ownership from their required same-tenant Portfolio. Program creation and reassignment reject Portfolios with no active owning Organization. Projects resolve Organization from either their Program's Portfolio or their direct Portfolio; Program/Portfolio disagreement and unresolved ownership are rejected. The contextual Project Portfolio and Program lookups remain authorized by `Project.Create` alone and return only minimal choices from eligible active Organizations; they do not grant Portfolio or Program module browsing.

Project creation provisions and assigns the active Organization-specific default Pre-Initiation phase. Existing ownerless Portfolio records remain excluded from contextual Project choices until an authorized, reviewed remediation is applied. Read-only data audit and the default-preview, approval-gated repair are prepared at `scripts/database/audit-portfolio-organization-ownership.sql` and `scripts/database/remediate-digital-transformation-portfolio-organization.sql`. The SQL audit has not been executed in this agent environment.