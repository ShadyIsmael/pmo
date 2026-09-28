# Portfolio Manager Perspective

The Portfolio Manager surface is a management read model over the existing hierarchy:

```text
Portfolio
├── Programs
│   └── Projects
└── Direct Projects
```

`GET /api/portfolio-management/portfolios` and `GET /api/portfolio-management/portfolios/{id}/overview` use the resolved management context and `Portfolio.View` permission. The overview resolves Programs by `Program.PortfolioId`, resolves Direct Projects only when `Project.ProgramId` is null, and uses the resulting Project ID set exactly once for all downstream summaries.

Financials aggregate the canonical Project-level budget, cost, commitment, and forecast sources. Groups remain separated by currency and expose `isBaseCurrency`; no foreign exchange conversion or mixed-currency grand total is produced. Portfolio or Program financial records are not added to the Project contribution totals.

Risks and Issues currently have Project-level authoritative records. The read model labels that source scope and retains Project, Program, or Direct Project context. Risk overdue counts remain zero because the authoritative Risk entity has no due-date field. Change Requests are surfaced as pending attention only; approval remains in the existing workflow.

Resource summaries use active canonical ResourceAllocation rows. Pressure is reported at 80% or higher, over-allocation above 100%, and cross-Program contention is derived from the Projects attached to each Resource. No resource identity or leveling model is duplicated.

Published Project Status Reports reuse the shared 30-day freshness rule in `ManagementReportingRules`. Program reporting gaps are derived from the reporting state of their Projects because no separate authoritative Program report entity is available.

The frontend defines the default widget identities centrally and renders them through reusable `WidgetShell` units. Program and Project drill-down controls reuse the existing workspaces and the Perspective Foundation. The native Portfolio Manager context remains permission-aware; View-As context is read-only through the existing middleware.
