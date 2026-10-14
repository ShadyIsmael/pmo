# Portfolio Manager Perspective

The Portfolio Manager surface is a management read model over the existing hierarchy:

```text
Portfolio
├── Programs
│   └── Projects
└── Direct Projects
```

`GET /api/portfolio-management/portfolios` and `GET /api/portfolio-management/portfolios/{id}/overview` use the resolved management context and `Portfolio.View` permission. An active user assigned the persisted tenant system role `admin` can read all portfolios in that same tenant, even without a Portfolio Manager assignment; the tenant filter and `Portfolio.View` policy remain mandatory. The same tenant-read scope covers only the Portfolio manager-handover `GET` endpoints for requests, assignment history, and snapshots, and still requires `Handover.View`; manager discovery and mutations are not included. For `PUT /api/portfolios/{id}` only, the same active tenant administrator can use tenant-wide object scope when the independent `Portfolio.Manage` policy is satisfied. This write-scope exception applies only in native mode and does not grant access to another tenant, archive/reactivate operations, or View-As mode. Other users remain limited to their resolved management scope. The overview resolves Programs by `Program.PortfolioId`, resolves Direct Projects only when `Project.ProgramId` is null, and uses the resulting Project ID set exactly once for all downstream summaries.

Financials aggregate the canonical Project-level budget, cost, commitment, and forecast sources. Groups remain separated by currency and expose `isBaseCurrency`; no foreign exchange conversion or mixed-currency grand total is produced. Portfolio or Program financial records are not added to the Project contribution totals.

Risks and Issues currently have Project-level authoritative records. The read model labels that source scope and retains Project, Program, or Direct Project context. Risk overdue counts remain zero because the authoritative Risk entity has no due-date field. Change Requests are surfaced as pending attention only; approval remains in the existing workflow.

Resource summaries use active canonical ResourceAllocation rows. Pressure is reported at 80% or higher, over-allocation above 100%, and cross-Program contention is derived from the Projects attached to each Resource. No resource identity or leveling model is duplicated.

Published Project Status Reports reuse the shared 30-day freshness rule in `ManagementReportingRules`. Program reporting gaps are derived from the reporting state of their Projects because no separate authoritative Program report entity is available.

The React Portfolio overview renders seven compact, API-backed cards with grouped Portfolio/Program/Project health, delivery/milestones, financials, risks/issues, resources, reporting, and activity. The dashboard shows its read-model source once rather than repeating it in every card. Currency groups remain distinct; aggregate amounts use the financial-total text token and individual amounts use sign-based semantic text. Program and Project drill-down controls reuse the existing workspaces and the Perspective Foundation. The native Portfolio Manager context remains permission-aware; View-As context is read-only through the existing middleware.

Portfolio manager handover is available in the React Portfolio workspace when `Handover.View` or `Handover.Manage` is granted. The current manager can submit or cancel a request; eligible same-tenant active managers must have effective `Handover.Approve` access. The recipient alone can accept or reject through My Work → Approvals. The owner changes only on acceptance, when the service writes the manager assignment, audit record, notification, and immutable snapshot in the same tenant. The workspace exposes request status, assignment history, and snapshots; cache refresh updates the Portfolio read model after a decision.

Portfolio edits cannot replace an existing owner; the initial owner is chosen at creation, while an unassigned legacy Portfolio may receive its first manager. Subsequent manager changes must use the handover workflow.
