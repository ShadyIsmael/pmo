# Program Manager Perspective

The Program Manager perspective is a presentation and aggregation layer over the existing Project and Portfolio domains. It does not create a second Project, Risk, Issue, Resource, milestone, or status-report source of truth.

## Scope and context

`GET /api/program-management/programs` returns only Programs in the resolved `ManagementContext`, except that an active user assigned the persisted tenant system role `admin` can read all Programs in that same tenant without a Program Manager assignment. The `Program.View` policy and tenant isolation remain mandatory; the exception is GET-only, does not grant write access, and does not apply in View-As mode. The same tenant-read scope covers only the Program manager-handover `GET` endpoints for requests, assignment history, and snapshots, and still requires `Handover.View`; manager discovery and mutations are not included. Native Program Manager requests use the complete native Program assignment scope for the My Programs landing; a Portfolio Manager viewing as Program Manager remains limited to the selected inherited Program context. `GET /api/program-management/programs/{programId}/overview` validates the selected Program against the same server-side scope before querying any child data, with the same tenant-admin read exception.

## Read model

`ProgramManagementService` performs bounded batch reads for the selected Program: child Projects, Project schedules and milestones, published Project Status Reports, Project Risks, Issue records, pending Project Change Requests, active Resource Allocations, Project financial records, and Project-linked Meetings. Dashboard summaries are returned with bounded top-N items; the Projects table is server-paginated.

Program delivery is derived only from child Projects and their existing milestones/schedules. A Project is considered critical when its authoritative Priority is `Critical` or its Health is `Red`; schedule delay is reported only when a Project Schedule has both baseline and forecast end dates and the forecast is later.

Project Risks are aggregated from the existing Risk entity and Issues from the existing Issue entity. No ProgramRisk or copied Project domain was introduced. Resources are aggregated from canonical ResourceAllocation rows and Resource records; Stakeholder relationships are not treated as capacity allocations.

Financials remain grouped by currency. Program base currency is displayed separately from foreign-currency exposure, and the response deliberately contains no mixed-currency grand total or FX conversion.

Published status reports are preferred. A report is `Current` when its ReportDate is within the centralized 30-day freshness rule, `Stale` when older, and a Project without a published report is `Missing`.

## Workspace and dashboard

The existing `/portfolios/programs` route is the My Programs landing. `/portfolios/programs/{id}` is the Program workspace with Overview, Projects, Delivery, Financials, Risks & Issues, Resources, Status Reports, and Meetings / Activity tabs. Project links navigate to the existing Project experience; no Program-specific Project CRUD was added.

The React dashboard uses seven compact API-backed cards with stable identities (`program.health-overview`, `program.delivery-milestones`, `program.financials`, `program.risk-issues`, `program.resources`, `program.status-reports`, and `program.activity`). Health/distribution, delivery/milestones, and risks/issues are grouped to reduce duplicated space; the read-model source appears once above the card grid. Currency groups remain distinct; aggregate amounts use the financial-total text token and individual project amounts use sign-based semantic text. A persistent layout engine is intentionally deferred.

Native and View-As contexts use the same workspace. Existing permissions remain authoritative; `ReadOnlyViewAs` adds the existing centralized mutation deny layer and does not grant Project, Risk, Financial, Planning, or Resource permissions.

The React Program workspace includes a permission-gated manager handover surface. The current Program Manager can request or cancel a transfer; the selected active same-tenant manager must have effective `Handover.Approve` access and decides from My Work → Approvals. Program ownership and current assignment change only after acceptance. The decision is recorded in the request history and audit log, a snapshot is captured, and both managers are notified. The workspace presents pending/completed requests, assignment history, and snapshots, and the Program read model is refreshed after acceptance.

Editing an assigned Program no longer changes its manager directly. An unassigned Program can receive its initial manager through the edit sheet; every later manager change must use the handover workflow.
