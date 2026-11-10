# Finance Legacy Cleanup

**Status:** Source cleanup complete; build and regression verification recorded below. Owner runtime UAT remains pending.
**Review date:** 2026-10-31
**Scope:** Project Finance UI, parent financial read models, frontend clients, the obsolete tenant-wide summary endpoint, and preservation boundaries for legacy financial records.

## Removed from the supported Finance experience

The Finance entry point is **Project Details > Finance**. The legacy application-wide Finance navigation and the following React routes/screens were removed:

| Retired surface | Removed implementation | Reason |
|---|---|---|
| Global financial overview | `/costs` and `CostsOverviewPage` | Repeated old project-ceiling, allocation, actual-cost, commitment, and forecast calculations; was not the approved Project Annual Budget / Payment Request read model. |
| Global allocation and actual-cost screens | `/costs/budgets`, `/costs/actuals`, `CostsPage` | Exposed legacy `Budget` and `CostEntry` CRUD outside a Project Details context. The legacy forms did not supply all backend-required fields. |
| Global forecast screen | `/costs/forecasts`, `CostsForecastsPage`, and `forecast-calculations.ts` | Summarized only the currently loaded allocation page and represented line variance as forecast output; it was not a full-project or approved annual-budget forecast. |
| Project Finance legacy panels | Project-wide legacy summary/forecast and the `Project.Budget` ceiling / planned-payment paid and outstanding summary cards; `Budget` line CRUD and revisions, `CostEntry` CRUD, and `PurchaseCommitment` CRUD in the Finance workspace | These panels duplicated and mixed old calculations and records into the six approved workspaces. The Finance tab now presents its authoritative annual-budget/closing metrics in Overview, retains Finance approval-mode configuration with Payment Requests, and connects the approved annual-budget, request, settlement/reversal, closing, and audit components while retaining the supported Planned Payments view. |
| Program/Portfolio legacy summary panels | Empty Finance cards and Finance status columns on parent overview/list screens; project Budget columns in Program views | The approved Financials tabs already use scoped `financial-rollup` endpoints. These old overview projections were empty or exposed legacy Project amounts outside the financial permission. The duplicate surfaces and DTO fields are removed. |
| Portfolio Optimization legacy value | Portfolio forecast total derived from legacy overview groups, plus the portfolio selector used only by that calculation | The disabled optimizer had no supported value formula or optimization service. Its forecast total could sum currencies and is removed; scenario and constraint states remain explicitly unavailable. |
| Operations Portfolio Report legacy totals | Budget and actual totals embedded in a Portfolio report card | The report had only `Portfolio.View`, while the old values came from a Finance summary. The report retains non-financial project and progress indicators and no longer projects financial amounts. |
| Legacy React client surface | Old `Budget`, `CostEntry`, `PurchaseCommitment`, global-summary, and `BudgetRevision` DTOs/hooks plus their endpoint constants | No longer consumed by the supported React Finance workspace after the screens were removed. The payment-obligation client remains. |
| Tenant-wide summary API | `GET /api/costs/summary`, `GlobalFinancialSummaryService`, its interface/DTOs, and DI registration | No remaining in-repository consumer after retiring the global page. It also duplicated Project/parent summaries and normalized missing currency to GBP without the approved per-currency/source-gap contract. |

The legacy standalone `/costs/payments` route remains as a compatibility deep link and is still reachable inside **Settlements & Reversals** for a selected Project. It is no longer exposed as an application-wide Finance navigation section.

## Approved architecture retained

| Project Finance section | Connected behavior retained |
|---|---|
| Overview | Backend-sourced annual budget, availability, pending request/approval metrics, finalized closing actuals, and financial term explanations. Missing/unfinalized values remain unavailable. |
| Annual Budgets | Project/Fiscal Year annual budget records, approvals, balances, and the existing closing/carry-forward context. |
| Payment Requests | Request create/edit/submit, configured approval lifecycle, decisions, and history. |
| Settlements & Reversals | Actual Payment Request settlements, reversals, and historical Planned Payment obligations/settlements. |
| Fiscal Year Closing | Readiness, blockers, approval, carry-forward, and closing history. |
| Financial Audit Trail | Existing project-scoped financial activity API, filters, and paging. |

Program and Portfolio reporting continues to use the dedicated, calculated `financial-rollup` read models. Their finance views remain read-only, currency-separated, and without independent budget or transaction write paths.

## Backend dependencies intentionally preserved

The cleanup removes legacy parent summary projections from the Program/Portfolio overview and CRUD response DTOs, but retains the Project legacy write APIs and backend services for `/api/budgets`, `/api/costs`, `/api/commitments`, and `/api/budget-revisions`. Source references show that removing these would exceed the Finance cleanup boundary:

- `ProjectOverviewService` and Project change/reporting paths depend on `IProjectFinancialLookup` and/or `IProjectEvmDirectCostLookup`, which read legacy `Budget`, `BudgetRevision`, `CostEntry`, and `PurchaseCommitment` records.
- Project EVM and published status-report capture retain legacy cost/commitment and snapshot inputs.
- `FinanceWorkflowAdapter` continues to read and transition `BudgetRevision` records through the shared workflow engine.
- The project `financial-foundation` endpoint remains for the approved Payment Request workflow's project currency, Finance approval mode, and row version. Its legacy Budget/obligation summary fields remain in the API response for compatibility, but the Finance Overview no longer displays or fetches those duplicate legacy metrics.
- Existing `Finances.Read` / `Finances.Write` policies and tenant/project resource scoping remain in place for those compatibility APIs. No roles or grants were changed.

The unreferenced in-repository React callers for those Project legacy endpoints have been removed. The response contracts for `/api/portfolio-management/.../overview` and `/api/program-management/.../overview` no longer include the legacy Finance summary groups, placeholder Finance availability indicators, or Project budget/actual fields. `/api/portfolios` and `/api/programs` keep their non-financial record and child-project responses but no longer return parent budget/actual/forecast summaries. The dedicated `financial-rollup` response contracts remain the parent Finance read model. No API endpoint or permission was added, and no separate Project legacy CRUD endpoint was removed. No evidence was available in the repository to prove that separately deployed clients do not call retained compatibility endpoints; those routes remain pending a separately coordinated API deprecation decision.

## Persistence and database cleanup recommendations

No entity, relationship, `AppDbContext`, mapping, migration, SQL, or database record was modified. No Development database was queried. The physical database inventory, migration state, record counts, and actual use of legacy rows are not verified.

Do not remove or rewrite these objects as part of this cleanup:

- `PaymentObligations`, `PaymentSettlements`, and `PaymentNotificationDispatches`: historical planned-payment ownership and notifications remain supported. `PaymentSettlements` is also the shared settlement ledger for Project Payment Requests; each row must retain its current exactly-one-owner relationship.
- `ProjectAnnualBudgets`, `ProjectPaymentRequestDetails`, `ProjectPaymentRequestFundingTransfers`, `ProjectAnnualBudgetClosings`, approver assignments, reversals, `AuditEntries`, and published status-report snapshots.
- Legacy `Budgets`, `BudgetRevisions`, `CostEntries`, `CostCategories`, `CostApprovals`, `PurchaseCommitments`, `Invoices`, `PortfolioBudgets`, and parent/project financial summary fields. These require a later source-consumer review, independently verified Development inventory, record reconciliation, retention decision, and the repository documented database workflow before any retirement proposal. Existing Project EVM, workflow, and historical report consumers currently remain.

Recommended later sequence:

1. Identify deployed consumers of the retained legacy API routes and decide a versioned deprecation/retention window.
2. Inventory exact Development data and relationships read-only, including snapshots, audit references, settlement owners, soft-deleted records, and per-currency totals.
3. Replace or explicitly retire remaining Project Overview/EVM/status-report dependencies before proposing database-object removal.
4. Only after separate authorization, produce a reviewed additive/retirement plan through the EF Core database workflow. No destructive database action is authorized here.

## Localization, permissions, and layout

- Removed English and Arabic strings used only by the retired global Finance navigation/pages and the obsolete parent availability indicators; kept `costs.totalActualCost`, which is still used by the shared Budget Overview widget.
- The six Project Finance section labels and explanations remain localized in both catalogs. The current sidebar uses logical inline-start styling so it moves to the right in Arabic RTL; the mobile selector and keyboard focus states remain.
- Existing effective permission checks and Tenant/Project scopes were not broadened. The Project Finance workspace still requires `Finances.Read` or `Finances.Write`; write actions require their existing specific permissions and writable management context.
- The standalone Planned Payments route retains its `Finances.Read` route guard and its existing service-level checks.

## Source verification

- **React regression:** 7 focused files passed (32 tests): Project Finance workspace, planned-payment Project scope, Program/Portfolio overview, Program/Portfolio sheets, localization, and Portfolio Optimization. Coverage includes the six Finance sections, selected-year persistence, request/settlement separation, permission gates, EN/AR labels, RTL direction, Program/Portfolio read-only rollup access, and removal of legacy parent totals.
- **React production build:** `npm run build` is blocked only by the existing TypeScript error in concurrently modified, out-of-scope `src/features/organization/OrganizationPage.tsx:513` (duplicate JSX `className`). The prior Finance and parent-summary TypeScript errors are resolved.
- **Vite bundle-only check:** did not complete; it emitted the same duplicate-attribute warning at `OrganizationPage.tsx:513` and was stopped after producing no further output.
  - **API builds:** `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore` and `dotnet build apps/api/src/NEXUS.Modules.Portfolio/NEXUS.Modules.Portfolio.csproj --no-restore` both returned `Build FAILED` with 0 warnings and 0 diagnostic errors. No successful API/module build evidence is available.
- **API regression tests:** the focused `dotnet test` attempt for `PaymentObligationLifecycleTests`, `PaymentRequestWorkflowPolicyTests`, and `ProjectEvmPersistenceTests` returned exit code 1 with no stdout or diagnostic output; no executed test count is available. Treat API test verification as blocked, not passed.
  - **Source scans:** no references remain to the deleted React pages/components/hooks, old React legacy endpoint constants, or `/api/costs/summary` implementation. No legacy parent financial summary fields are referenced by general Program/Portfolio overview or Operations screens. Both EN/AR JSON catalogs parse and the six Finance navigation labels are present in each; orphaned global `nav.costs` and Project-wide legacy summary labels were removed, while the shared `costs.totalActualCost` label remains for `BudgetOverviewWidget`. Whitespace scans found no trailing spaces in the final Finance, parent-rollup, localization, and documentation files; `git diff --check` emitted no whitespace errors for tracked Finance files.
- **Persistence boundary:** no entity, relationship, mapping, migration, SQL, or database record was changed or queried.

Runtime/browser UAT, service restart, and database operations remain pending Owner UAT.

## P1 Annual Budget recovery cleanup addendum - 2026-11-08

A repository-wide consumer scan found that `GlobalFinancialSummaryService`, `IGlobalFinancialSummaryService`, and `GlobalFinancialSummaryDtos` remained as source files even though the tenant-wide endpoint and DI registration had already been removed and this document recorded them as retired. No API route, DI registration, frontend caller, test, or other active source file referenced the service contract. The one-off `scripts/maintenance/budget-global-edit.cjs` only rewrote those retired files. The orphan service, interface, DTOs, and rewrite script were removed. No public API contract, persisted entity, migration, database record, or history was changed.

The cleanup also confirmed the supported Project Annual Budget and Year-End Closing flows use the canonical project-scoped permissions. `finances.read` and `finances.write` remain unsupported in those flows; old references in the Sprint 5 and Sprint 2 historical notes were corrected. No authorization alias or broad fallback was found in the supported Annual Budget frontend/API path.

The legacy `Budget`, `BudgetRevision`, `CostEntry`, `PurchaseCommitment`, and `PaymentObligation` services and records remain intentionally retained: Project EVM, Project change/reporting paths, shared workflow integration, published snapshots, and Planned Payments still consume them. Their retirement would require a separate consumer and data-retention review. This cleanup made no database connection or data change.

## Owner UAT scenarios

1. Confirm the global Finance nav and old `/costs`, `/costs/budgets`, `/costs/actuals`, and `/costs/forecasts` pages are gone; open Finance from a Project's Details page.
2. Navigate the six Project Finance sections and confirm one focused workflow is presented at a time.
3. Confirm Annual Budgets uses Project Annual Budget records and does not show legacy allocation lines or budget revisions.
4. Create and approve a Payment Request; verify approval history remains distinct from settlement history.
5. Record partial/full actual settlements and a permitted reversal; confirm historical Planned Payment settlement owners and balances remain intact.
6. Review fiscal-year blockers, closing, carry-forward, and audit trail using authorized and unauthorized users.
7. Confirm Program and Portfolio Financials tabs remain read-only, use the dedicated rollup endpoints, and group exact currencies; confirm parent Overview, Program/Portfolio lists, Operations Portfolio Reports, and Portfolio Optimization do not show old Budget/Actual/Forecast totals or availability badges.
8. Test English desktop, Arabic RTL, keyboard navigation, and narrow/mobile section selection.
