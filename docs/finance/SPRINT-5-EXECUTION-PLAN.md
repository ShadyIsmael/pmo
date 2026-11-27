# FOPX PM — Sprint 5 Execution Plan

**Sprint:** Financial Cash Flow Management & Forecasting  
**Repository:** `C:\Projects\pmo`  
**Execution date:** 2026-11-23  
**Main Agent:** Orchestration, integration, focused verification, and closure

## Sprint Task List

| Task | Outcome | Owner | Status |
|---|---|---|---|
| 00 — Discovery and execution blueprint | UPI-routed inventory, verified contracts, file ownership, dependencies, risks | Main Agent + all specialists | DONE — VERIFIED |
| 01 - Cash Flow foundation | Authoritative Project-level Planned / Committed / Actual cash-flow API and calculations | Agent A; Agent D supports tests/security | IMPLEMENTED - UNVERIFIED |
| 02 - Cash Flow Calendar | Localized daily/monthly Project Financial Workspace calendar | Agent B; Agents A/D support | IMPLEMENTED - UNVERIFIED |
| 03 - Forecast and variance | Deterministic monthly forecast and timing/amount variance analysis | Agent A backend; Agent B UX; Agent D verification | IMPLEMENTED - UNVERIFIED |
| 04 - Program and Portfolio | Currency-safe consolidation, drill-downs, Excel/PDF exports | Agent C; Agents B/D support | IMPLEMENTED - UNVERIFIED |
| 05 - Integration and closure | Focused regression, build/typecheck, single deferred database procedure, consolidated report | Main Agent; all specialists | BLOCKED |

No native `update_plan` / Tasks List tool is exposed in this environment. This table is the maintained sprint-level task list and checkpoint.

## Scope and Operating Rules

- Preserve the approved lifecycle: Planned Payment → Installment → Payment Request → Approval → Settlement → Actual Paid.
- Reuse Sprint 4's Financial Reporting Foundation, project finance workspace, Program/Portfolio rollups, and Excel/PDF patterns where their semantics fit.
- A request approval is not actual cash. Actuals use confirmed settlements on settlement dates; executed refunds/corrections adjust net actuals while gross actuals remain visible.
- Planned, committed, and actual amounts are separate reporting perspectives. Never add overlapping lifecycle states into one exposure total.
- Do not guess relationships, dates, currencies, or conversions. Propagate Partial/Unavailable quality when evidence is insufficient.
- Reuse existing scoped view permissions; register/enforce any genuinely necessary new permission in the catalog, backend, and frontend before implementation is considered complete.
- No SQL Server access, migrations, seeding, DB-backed persistence tests, or Agent UAT. Database evidence is consolidated for Product Owner PowerShell execution in Task 05.
- Preserve all existing Git changes. The worktree is heavily modified from prior financial and other sprint work; existing files require targeted diff review before edits. No cleanup/reset operations.

## Discovery Evidence

- Read repository `AGENTS.md`, `docs/architecture/INDEX.md`, and `.agents/skills/universal-project-intelligence/SKILL.md`.
- UPI routed Sprint 5 Task 00. Manifest: `docs/architecture/task-scopes/20261123T192356-sprint-5-financial-cash-flow-management-discover-c-251ea.md`.
- Route selected the Project Financial Foundation controller, planned-payment service and DTO contracts, payment workflow policy, financial permission registrations, and the existing Cost Utilization Calendar helper test. Initial selection: 9,539 estimated source-reading tokens; no critical-path gate applied.
- Current architecture index: 1,892 files, 14,862 symbols, and 74,850 relationships. The router reported no changed files during this invocation.
- Existing Project/Program/Portfolio financial reporting and export capabilities are documented in Sprint 4 Tasks 01–04. The Cost Utilization Calendar is a labor/capacity view, not a cash-flow view. Sprint 4's accepted closure conditions and known risks carry forward.
- Task 01 DTO/calculator, batched settlement loader, and Project reader/controller are implemented. Review identified a cross-project reversal-detail provenance check that is now fixed in the reader. Focused backend tests exist, but the test project build failed without diagnostics (`0 warnings, 0 errors`); a bounded diagnostic run produced no output and was canceled. Backend verification remains pending.
- Task 02 Calendar/API/date helpers are implemented. Agent B reports 13 focused Vitest tests passing and `npm run typecheck` passing before workspace wiring. The Main Agent wired the calendar into `ProjectFinanceTab`, aligned its view gate with the existing backend permission policies, and connected fiscal-year options to the authorized annual-budget lookup. Locale labels are being completed; selected-day details are available, but existing source-record navigation is not yet verified.
- Task 03 forecast semantics are confirmed: current `PlannedResidual` and `CommittedUnpaid` remain separate expected-outflow series; `ActualGross` and `ActualNet` remain separate; day variance is settlement date minus expected date (positive late, negative early). No historical monetary forecast accuracy is available without saved as-of snapshots, so the UI must disclose this limitation and must not calculate a fabricated variance.
- Program/Portfolio aggregation remains in progress. New hierarchy DTO/aggregation files and additive unpaged project-membership methods are being implemented under Agent C's exclusive ownership.
- Current worktree has extensive uncommitted changes, including finance APIs, DTOs, rollup services, tests, and UI. Subagents must not edit any existing modified file until ownership and its current diff have been reviewed. New-file ownership is preferred.

## Shared Contract (Confirmed by Finance Discovery)

Use one project-scoped, authorized reporting contract that returns:

- daily or monthly buckets, each grouped by its authoritative currency;
- separate Planned, Committed, Gross Actual, and Net Actual amounts;
- dated detail items with links to the source installment/request/settlement where known;
- explicit unknown-date totals and data-quality flags rather than arbitrary bucket assignment;
- overdue and timing-variance measures with their comparison semantics stated;
- aggregation inputs that preserve project identity and do not double count projects or lifecycle states.

Forecasts and exports must consume the same authoritative DTO/calculation outputs rather than recomputing financial amounts in the UI or a second reporting engine.

### Verified Foundation Findings (Agent A)

- Reuse the current `FinancialReportingCalculator` and `ActualPaidSettlementCalculator` as source/semantics references, but add a separate date-bucketed Cash Flow calculation rather than changing the existing lifetime/annual report contract.
- `ActualPaidSettlementCalculator.LoadForProjectAsync` and installment/request linkage provide confirmed settlements; actual values use settlement dates. Its adjustment query includes only executed adjustments, and refund signs are negative.
- Planned dates come from installment due dates. Approved requests link to an installment; one installment can have multiple partial requests. For Committed, use **Approved only**, and derive remaining unpaid balance from approved request amount less linked valid settlements and approved releases. Pending approvals are excluded from this sprint's Committed definition.
- Do not reuse the existing combined `OutstandingCommitments` summary: it includes legacy `PaymentObligation` balances plus Planned Payment installment outstanding, so it can overlap and is not a cash-flow commitment total.
- No reliable `PaymentObligation` ↔ Planned Payment crosswalk is established. Keep legacy rows as an explicit reconciliation/data-quality limitation; do not guess-map them into buckets.
- Current project-level reporting permissions/scope are reusable (`ProjectFinancialFoundation.View` and `ProjectSettings.View`). No new permission is indicated by discovery; verify the frontend's effective view permission before wiring actions.
- No new database schema dependency is confirmed; source linkage, dates and amounts exist in the current model. This is not a claim of physical Development schema or persistence verification.
- Adjustment state is `Executed` only for actuals; event date is `ExecutionDate ?? EffectiveDate`, while `RecordedAt` is audit chronology. Existing semantics sign Refund negative and Correction positive.
- Request detail kinds are `PaymentRequest` and `ReversalRequest`; active plans are `Registered` or `Locked`. Approved releases come from the existing reversal query with Approved status and matching tenant/project/currency/budget provenance. No persisted release amount exists; map only the validated cumulative reversal amount.
- Payment request roots use workflow type `ProjectPaymentRequest`, entity `Project`, and the target project ID. Since the workflow constants are private, query code must match those exact existing criteria without widening them.
- Request fact input carries its `RequestKind`; include original Payment Request details needed for settlement/date links, but count a request as Committed only when its kind is PaymentRequest and its approval status is Approved. Do not mistake reversal approval records for payment commitments.

### Calculation Definitions

- **Planned Residual:** per installment, `max(0, scheduled amount - reliably linked gross settlements - approved unpaid request balances)`. Pending requests remain within Planned until approval. Do not use net actual/refunds to restore planned capacity.
- **Committed Unpaid:** per approved request, `max(0, approved request amount - valid linked settlements - approved unpaid release/reversal)`. Date it from the linked installment due date unless an authoritative request expected date exists. Pending requests are excluded.
- **Actual Gross:** confirmed settlements by settlement date, deduplicated by settlement identity and source owner.
- **Executed Adjustments / Actual Net:** only executed adjustments by execution date, using the existing effective-date fallback only where that source contract permits it. Refunds keep their negative stored sign; corrections retain existing sign/type semantics. Net Actual = Gross Actual + Executed Adjustments.
- **Undated values:** keep separate from dated buckets and surface data-quality reason codes. Never assign them to today or period start.
- **Currency:** group by the currency actually recorded. No conversion and no cross-currency totals. Invalid/unknown currencies remain flagged and unavailable/unallocated; foreign/mismatched known currencies remain separately grouped and flagged.
- **Linkage:** if a payment, settlement, or request cannot be reliably linked to its installment, keep the value Partial/Unavailable and do not invent a crosswalk.

Contract shape: project id, requested range and granularity, `currencyGroups[]`, `buckets[]`, explicit undated/unallocated detail, source-linked item details, and response/group/category quality status with stable reason codes. A bucket carries Planned Residual, Committed Unpaid, Gross Actual, Executed Adjustments, and Net Actual separately. Values remain nullable when incomplete; unknown is not zero.

### Verified QA/Reuse Findings (Agent D)

- Existing tests cover financial decimal precision, payment lifecycle, scoped view/export authorization, utilization-calendar helpers, currency formatting, and Project/Program/Portfolio export infrastructure. They are regression candidates, not yet executed for Sprint 5.
- Calendar utility tests cover 42-day month grids, locale weekday start, date ranges, currency grouping and unavailable values; the existing UI is for labor utilization, not financial cash flow.
- Existing export code supports Project/Program/Portfolio Excel/PDF, currency groups, quality warnings, EN/AR, PDF/Excel RTL. Cash Flow needs an input/DTO-specific export path; current financial summary values cannot stand in for time buckets/categories.
- Add new, dedicated cash-flow test files; do not modify existing finance or permission tests while implementation proceeds in parallel.

### Task 02 Discovery (Agent B)

- UPI's first route was misclassified as `permissions`; the follow-up routes focused on cost management and Project financial workspace. Route manifests: `20261123T192614-cost-utilization-calendar-and-existing-project-fin-72856.md` and `20261123T192715-project-financial-workspace-planned-payments-and-c-cb536.md` under `docs/architecture/task-scopes/`.
- Reuse only date-grid/month navigation and currency grouping from `apps/web-react/src/features/resources/cost-utilization-calendar.ts`; do not reuse its labor/capacity calculations.
- No Cash Flow Calendar or Forecast UI was found. Existing planned-payment section provides authoritative source detail and actions, but is not a calendar.
- Proposed new frontend ownership: `apps/web-react/src/features/projects/CashFlowCalendar.tsx`, `apps/web-react/src/features/projects/api/project-cash-flow.ts`, and a new focused calendar test. Parent-page wiring and both locale catalogs remain Main Agent owned after current-diff review.
- Follow-up UPI route located `apps/web-react/src/features/projects/ProjectFinanceTab.tsx`; it is modified and Main Agent owns its integration. The existing financial tab gates queries/actions with `Permissions.ProjectFinance.View`.
- Existing backend financial reporting requires `ProjectFinancialFoundation.View` plus `ProjectSettings.View`, along with tenant and project-scope checks. Frontend and backend keys differ; preserve existing checks and verify the permission combination in tests/catalog. Do not add grants automatically.
- Calendar cells show each amount independently; the day sheet includes source links, undated values, and quality warnings. Parent workspace wiring and the frontend query gate remain Main Agent owned.

### Task 04 Discovery (Agent C)

- The existing `ProgramPortfolioFinancialRollupService` already scopes Program/Portfolio access, includes direct Portfolio projects plus Program projects, deduplicates projects, and reports partial coverage. Existing rollups are fiscal-year/lifetime summaries rather than dated cash flow.
- Aggregate the same project cash-flow categories by currency and period after deduplicating Project IDs. Keep undated details separate, union reason codes, propagate Partial/Unavailable coverage, and include each contributing Project and direct/Program relationship.
- Existing exports support Excel/PDF and locale/RTL behavior, but their DTO is budget-report-specific. Cash Flow needs its own report DTO/builder so time buckets and lifecycle categories match API values.
- Agent C proposed isolated new API files (`ProgramPortfolioCashFlowDtos.cs`, service, controller), new delivery API/view files, and dedicated export modules. Existing rollup service/DTOs, delivery, and financial export modules are dirty; Main Agent owns any integration edits after diff review.
- Discovery required a scoped expansion because the Cash Flow route selected only a closing workflow and the refined reporting route omitted the rollup service; this dependency was read for the explicit Program/Portfolio aggregation requirement. No DB/schema dependency was identified.
- Hierarchy response retains each project's full cash-flow DTO and relationship. Currency group currency is nullable; sum only equal periods/categories within one currency, propagate Partial/Unavailable and quality codes, and never convert or combine categories.
- Additive full-membership methods on `IProgramPortfolioFinancialRollupService` are approved for the existing service/interface after the Main Agent reviewed the current Sprint 4 diff. The methods return all visible unique project IDs and direct/Program relationship without the existing 100-row pagination.

### Task 01 / QA Routes

- Root's focused Task 01 route: `docs/architecture/task-scopes/20261123T193354-project-financial-cash-flow-from-planned-payment-i-39575.md` selected settlement-adjustment workflow, settlement domain contract, and Planned Payment DTO; dependency awareness exposed `ActualPaidSettlementCalculator`. Finance discovery expanded only to its needed direct settlement, request, and reversal sources.
- Agent D's focused calculation route: `docs/architecture/task-scopes/20261123T193333-cash-flow-calculation-test-scope-in-cost-managemen-d2482.md`. It selected Project financial controllers, reporting/calculation contracts, and existing focused finance tests. No Cash Flow-specific test existed.
- A broad authorization keyword route selected unrelated generic permission samples; it was not used as a source scope. Existing project financial API and UI policies provide the relevant authority patterns.

## Dependency Graph

```text
Task 00 discovery/contracts
        ↓
Task 01 Project cash-flow foundation
       ↙ ↘
Task 02 Calendar   Task 03 Forecast and variance
       ↘             ↙
       Task 04 Program/Portfolio + exports
                  ↓
       Task 05 integration and closure
```

Specialists may do UPI-routed discovery and non-overlapping scaffolding in parallel. API/DTO contracts must be agreed before dependent implementation. No two agents may edit the same file.

## Agent Assignments and File Ownership

| Owner | Bounded responsibility | Initial ownership boundary |
|---|---|---|
| Agent A - Finance Domain & Backend | Calculation semantics, source linkage, Project cash-flow DTO/calculator, forecast calculations | New `CashFlowDtos.cs` and `ProjectCashFlowCalculator.cs`; exclusive additive edit to `ActualPaidSettlementCalculator.cs` for batched facts with single-project parity; no other existing source/API wiring |
| Agent B — Frontend & UX | Calendar, forecast dashboard, filters, detail views, EN/AR/RTL | New `CashFlowCalendar.tsx`, `project-cash-flow.ts`, forecast components/helpers, and their focused frontend tests; root owns workspace wiring and both locale catalogs |
| Agent C - Reporting & Aggregation | Program/Portfolio hierarchy aggregation, currency grouping, export integration | New cash-flow DTO/aggregator, controller/view/API/export files; exclusive additive membership-method edits in `IProgramPortfolioFinancialRollupService` and `ProgramPortfolioFinancialRollupService.cs` only; preserve existing diff |
| Agent D - QA, Security & Regression | Focused tests, authorization boundaries, financial invariants, test/build findings | New `CostManagement/CashFlowCalculationTests.cs`, `Security/CashFlowAuthorizationTests.cs`, and later aggregation contract tests; no production-code edits |
| Main Agent | Task list, contract integration, shared file edits, defects, tests/build, database procedure, closure | `docs/finance/SPRINT-5-EXECUTION-PLAN.md`, `ProjectCashFlowReader.ReadManyAsync`, Project cash-flow API controller, and workspace/route/locale integration after targeted diff review |

## Acceptance Criteria

1. Every cash-flow amount comes from an authoritative existing financial lifecycle record; relationships that cannot be established remain Partial/Unavailable.
2. Planned, committed, gross actual, and net actual amounts are separated, currency-safe, correctly dated, and never fabricated into a combined exposure.
3. A scoped user can inspect monthly and daily project cash flow, expected/overdue payments, source details, and forecast/actual variance in EN/AR and RTL.
4. Program and Portfolio aggregation counts each project once, includes direct Portfolio projects, and never mixes currencies or sums percentages.
5. Excel/PDF exports reproduce the reporting DTO values, filters, currency grouping, and data-quality notices.
6. Backend and frontend authorization follow canonical scoped permissions; no new grants are silently assigned.
7. Focused automated tests and applicable builds/typecheck pass, or exact external blockers are documented with evidence.
8. Task 05 records the database items as `Not run`/`Pending Product Owner evidence` where appropriate and includes the reviewed PowerShell procedure without executing it.

## Known and Carried-Forward Risks

- Legacy `PaymentObligation` overlap and unresolved historical payment mappings must not be guessed into the new forecast.
- Financial runtime/persistence and persisted `RolePermission` behavior have documented Sprint 4 evidence gaps; assess only their direct impact on new cash-flow scope.
- Windows Vite/esbuild temporary-file access failure and prior backend test infrastructure problems remain environmental candidates; reproduce only through bounded relevant commands.
- Physical database schema and live Development data are unverified for this Sprint until the Product Owner executes the consolidated Task 05 procedure.

## Resume Checkpoint

**Current step:** Tasks 00-04 implementation is integrated. Frontend typecheck, focused UI/export tests, and production build passed. Backend Task 01/04 tests remain unverified: the serial run entered the existing IKVM/MppAdapter dependency build and emitted extensive unsupported/missing-class warnings, then was stopped after 150 seconds before tests began. A bounded API-only compile attempt with `BuildProjectReferences=false` spent 120 seconds in the same IKVM reference task and was stopped before reaching a C# diagnostic. The earlier separate API build exited 1 with 0 warnings/0 errors. Task 05 is documented as BLOCKED pending backend verification and Product Owner runtime/persistence evidence.
**Database operations:** None performed or permitted from this agent environment. Offline EF model check passed: model synchronized, 39 repository migrations, no Sprint 5 migration, and no database connection opened. Target identity, target pending-migration count, and physical schema are not verified for Sprint 5.
**Final evidence:** Latest UPI refresh indexed 1,915 files / 15,089 symbols / 75,784 edges; validation passed with zero errors or warnings. Frontend: typecheck passed; focused Cash Flow suite passed 21/21 tests; production build passed. Master UAT Checklist includes S5-CF-01 through S5-CF-09 for Product Owner execution; no Agent UAT was performed.
**Remaining external gates:** Backend build/test environment must complete the API and focused test assembly; Product Owner authorized Development runtime/persistence and browser UAT remain pending. No Sprint 6 work is started.
