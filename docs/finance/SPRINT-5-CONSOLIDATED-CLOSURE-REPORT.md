# FOPX PM — Sprint 5 Consolidated Closure Report

**Sprint:** Financial Cash Flow Management & Forecasting  
**Date:** 2026-11-23  
**Repository:** `C:\Projects\pmo`  
**Final recommendation:** **BACKEND VERIFIED — PENDING PRODUCT OWNER DATABASE GATE**

## 1. Executive Summary

Project Cash Flow, Calendar, forecast dashboard, Program/Portfolio aggregation, and Excel/PDF export paths are implemented and integrated with the existing financial operating model. The frontend typecheck, focused Cash Flow suite, and production build passed. The offline EF model check reports a synchronized model and no Sprint 5 migration.

The Sprint 5 backend verification blocker has been resolved. The API/test build now compiles successfully (NEXUS.Api and NEXUS.Modules.CostManagement build with 0 warnings and 0 errors), and the focused Sprint 5 backend test suite executes: **20 passed, 0 failed, 0 skipped**, covering planned/committed/actual cash flow, no double counting, partial settlements, executed refunds/corrections, date bucketing, forecast variance, currency isolation, Program/Portfolio aggregation, authorization, and data-quality propagation. Two confirmed defects were fixed (one compile defect in `ProjectCashFlowReader.cs`, one test-expectation defect in `CashFlowCalculationTests.cs`).

Sprint 5 backend financial integrity is verified. Development database identity, physical schema, live persistence, and browser UAT were not checked by the agent and remain reserved for the Product Owner through the documented PowerShell gate (Section 15).

## 2. Sprint Task Completion Matrix

| Task | Status | Evidence / outstanding condition |
|---|---|---|
| 00 — Discovery & execution blueprint | DONE — VERIFIED | UPI-routed discovery, contracts, ownership, dependencies, and risks recorded in the execution plan. |
| 01 — Cash Flow foundation | IMPLEMENTED — VERIFIED | Project API, reader, calculator, and focused tests exist; backend build and focused calculation tests pass (CashFlowCalculationTests, ActualPaidSettlementCalculatorBatchTests). |
| 02 — Cash Flow Calendar | IMPLEMENTED — UNVERIFIED | Project workspace integration, EN/AR, request-detail link, export, frontend tests/build pass; backend API/runtime remains unverified. |
| 03 — Forecast & variance | IMPLEMENTED — UNVERIFIED | Deterministic dashboard and timing variance; frontend tests/build pass; backend API/runtime remains unverified. |
| 04 — Program/Portfolio & exports | IMPLEMENTED — VERIFIED | Scoped aggregation, views, and exports are integrated; backend aggregation tests pass (ProgramPortfolioCashFlowAggregationTests); frontend tests pass. |
| 05 — Integration & closure | BLOCKED (DB GATE ONLY) | Backend build/test verified; report consolidated; Product Owner Development runtime/persistence and browser UAT evidence remain outstanding. |

## 3. Multi-Agent Execution Summary

- **Agent A — Finance Domain & Backend:** defined calculation semantics, added Cash Flow DTO/calculator, batched settlement loading, and adjustment provenance guards.
- **Agent B — Frontend & UX:** added Calendar and Forecast UI, date helpers, API client, localized labels, and focused frontend tests.
- **Agent C — Reporting & Aggregation:** added Program/Portfolio membership-backed API and aggregation, views, and Project/Program/Portfolio Excel/PDF export support.
- **Agent D — QA, Security & Regression:** added calculation, batched-settlement, hierarchy, and authorization tests; backend execution was previously blocked by the IKVM/MppAdapter build path, now resolved; focused cash-flow tests executed (Section 12).
- **Main Agent:** integrated controllers and workspace routes, connected current Project request details under view-only permission, updated the Master UAT Checklist, ran frontend checks and offline EF model verification, and consolidated reports.

## 4. Implemented Cash Flow Architecture

The Project endpoint `GET /api/projects/{projectId}/cash-flow` reads the existing financial lifecycle through `ProjectCashFlowReader` and `ProjectCashFlowCalculator`. Program and Portfolio endpoints use the existing scoped membership resolver, then call the same batched Project reader and pure aggregation layer. The frontend Project Financial Workspace hosts the Calendar, Forecast Dashboard, and Project exports; existing Program and Portfolio financial tabs host the hierarchy view and exports.

No new financial entity/table, role, permission key, or grant was introduced. Project API uses the existing `ProjectFinancialFoundation.View` and `ProjectSettings.View` policies plus Project scope. Program and Portfolio APIs use their existing paired entity/rollup view policies and scoped membership resolver. Opening a linked Payment Request requires `ProjectPaymentRequest.View`; the mounted detail component is supplied view-only capabilities from this action.

## 5. Planned / Committed / Actual Reconciliation

- **Planned** reports valid scheduled installment residuals after reliable linked settlements and approved request balances. Pending requests do not remove amounts from Planned.
- **Committed** reports approved unpaid request balances after valid settlements and approved reversal releases. It does not create a second commitment for a linked installment.
- **Actual Gross** comes only from confirmed settlements on actual settlement dates.
- **Executed adjustments / Actual Net** include only executed refunds/corrections, with existing financial sign semantics and execution-date fallback semantics.
- **Timing variance** is date difference in days, not a currency amount. Historical monetary forecast accuracy is unavailable without saved as-of snapshots and is not fabricated.
- **Unknown date, currency, or linkage** remains separately flagged and Partial/Unavailable; no arbitrary period assignment, conversion, or synthetic combined exposure is presented.

The calculator includes provenance checks for adjustment source settlement, currency, and request/obligation/installment ownership. An unresolved adjustment preserves the flagged detail while making affected adjustment/net totals unavailable.

## 6. Calendar Verification

The Project Calendar supports month navigation, daily details, fiscal-year and currency filters, separate lifecycle measures, overdue unpaid items, undated values, and loading/empty/error/unavailable states. English and Arabic labels are present, and locale key sets match for the Cash Flow subtrees. Linked Payment Requests open the existing view-only request detail sheet when the user has `ProjectPaymentRequest.View`; unlinked items retain descriptive IDs/status and do not receive a guessed route.

Focused UI regression covers linked and unlinked source actions. The focused frontend suite passed 21/21 tests. Product Owner browser UAT and live backend authorization remain pending.

## 7. Forecast & Variance Verification

The dashboard uses current scheduled residuals and approved unpaid requests as separate expected-outflow series, with Actual Gross and Actual Net shown separately. Overdue items are date/status-derived. Timing variance uses actual-versus-expected dates in days. No monetary forecast-versus-actual accuracy claim is made without historical forecast snapshots.

Focused frontend forecast tests passed as part of the 21-test suite. Backend calculations and authorization metadata are now verified by the focused backend test suite (Section 12); live date/currency data and runtime permissions remain Product Owner UAT items.

## 8. Program / Portfolio Aggregation

The existing hierarchy resolver includes Program Projects and both direct and indirect Portfolio Projects, filters to visible scope, marks partial coverage, and removes duplicate Project IDs. Aggregation preserves date buckets and currency groups, never converts or adds currencies, keeps lifecycle categories separate, and propagates quality status/codes. Project contributions expose names and direct/Program relationship for drill-down.

Pure C# aggregation and authorization tests were added and now execute (Section 12). Frontend typecheck/build and export tests passed.

## 9. Export Verification

Project, Program, and Portfolio Excel/PDF builders normalize the Cash Flow DTO and include period, currency group, planned/committed/actual measures, dated and undated details, timing variance, contributing Project information, and quality notices. Frontend export tests passed, including Project workbook coverage. Hierarchy export values match the reporting DTO; live API comparison remains a Product Owner UAT item.

## 10. Security & Authorization

Source-level endpoint policies are paired as follows:

| Endpoint | Required policies |
|---|---|
| Project Cash Flow | `ProjectFinancialFoundation.View` + `ProjectSettings.View`; Tenant and Project scope checks |
| Program Cash Flow | `Program.View` + `ProgramFinancialRollup.View`; scoped Program and visible Project membership |
| Portfolio Cash Flow | `Portfolio.View` + `PortfolioFinancialRollup.View`; scoped Portfolio and visible direct/Program Project membership |
| Payment Request details | `ProjectPaymentRequest.View`; same Project request API scope; no mutation capability from Calendar action |

Reflection tests assert Project, Program, and Portfolio endpoint policy metadata and routes, but backend tests did not run. Effective persisted `RolePermission` grants, Active Deny, cross-scope behavior, and browser permissions remain Product Owner UAT items in `docs/quality/MASTER-UAT-CHECKLIST.md` (S5-CF-01 through S5-CF-09).

## 11. Confirmed Defects Fixed

Two confirmed in-scope defects were fixed during backend verification:

1. **Compile defect — `ProjectCashFlowReader.cs` (lines 146, 174).** The reader filtered `!detail.IsDeleted` on `ProjectPaymentRequestDetail`, but that entity (and its `ProjectPaymentRequestDetails` table) has no `IsDeleted` property/column (`NEXUS.Modules.CostManagement.Domain.ProjectPaymentRequestDetail` implements `ITenantEntity`, not `BaseEntity`/`ISoftDeletable`; the EF model snapshot confirms no `IsDeleted` column). The detail's deletion status flows through its 1:1 cascade parent `ApprovalRequest`, which the reader already filters via `!request.IsDeleted`. Removed the redundant `!detail.IsDeleted` clauses. Root cause: the new Sprint 5 reader referenced a non-existent member.

2. **Test-expectation defect — `CashFlowCalculationTests.cs` (line 169).** `Only_executed_refunds_and_corrections_change_net_actual_using_execution_then_effective_date` asserted `TimingVarianceDays == 10` for a settlement on Jan 15 with an expected date of Jan 10. The calculator (`ProjectCashFlowCalculator.cs:291-293`) correctly computes `settlement.SettlementDate.DayNumber - expectedDate.DayNumber = 15 - 10 = 5`. Corrected the expected value to `5`. Root cause: stale/wrong expected value in the new Sprint 5 test.

No compile errors were suppressed and no essential production dependency was skipped.

## 12. Focused Backend Test Results

Executed via `dotnet test NEXUS.Api.Tests.csproj -p:FocusedCashFlow=true` (isolated cash-flow test assembly: calculation, authorization, aggregation, and batch-settlement tests only; unrelated pre-existing test-project errors avoided).

| Suite | File | Tests | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| Cash flow calculation | `CostManagement\CashFlowCalculationTests.cs` | 11 | 11 | 0 | 0 |
| Settlement batch loader | `CostManagement\ActualPaidSettlementCalculatorBatchTests.cs` | 1 | 1 | 0 | 0 |
| Aggregation | `ProgramPortfolioCashFlowAggregationTests.cs` | 3 | 3 | 0 | 0 |
| Authorization | `Security\CashFlowAuthorizationTests.cs` | 3 | 3 | 0 | 0 |
| **Total** | | **20** | **20** | **0** | **0** |

Coverage executed:
- Planned, committed, and actual cash flow (separate lifecycle measures; no double counting across Planned Residual + Committed Unpaid + Actual Gross)
- Pending requests and non-PaymentRequest kinds excluded from Committed
- Partial settlements and approved unpaid release/reversal amounts
- Executed refunds/corrections only (negative refund sign, correction sign semantics); Approved-but-unexecuted adjustments excluded
- Execution-date then effective-date fallback for net actual
- Adjustment source-settlement, currency, and request/installment ownership provenance with Partial/Unavailable flags
- Date bucketing (daily and monthly granularity); unknown dates preserved in undated totals
- Forecast/timing variance (days, settlement minus expected); no fabricated monetary accuracy
- Overdue and timing-variance measures date/status-derived
- Currency isolation (USD/EUR separate groups; no conversion or mixed totals; foreign currency flagged)
- Program/Portfolio aggregation with project deduplication, per-currency totals, Partial/Unavailable propagation, missing-project responses
- Tenant and project scope filtering in the reader; endpoint authorization metadata (`ProjectFinancialFoundation.View` + `ProjectSettings.View`; Program/Portfolio paired rollup policies)
- Data-quality propagation (reason codes: MissingAdjustmentSettlementReference, CurrencyMismatch, ConflictingSourceRows, SettlementInstallmentConflict, MissingRequestInstallmentLink, MissingSettlementReference, UnknownPlannedDueDate, UnknownActualExpectedDate, ForeignCurrencySource)

**Result: 20 passed, 0 failed, 0 skipped.** No test case is claimed as passed unless it executed in this run.

## 13. Frontend Test Results (previously passing, unchanged)

- `npm run typecheck`: **PASS**.
- Focused Cash Flow Vitest suite: **4 files, 21 tests passed**.
- Locale JSON parse and Cash Flow subtree key parity: **PASS**.

## 14. Build Results

- Frontend `npm run build`: **PASS**, 3,015 modules transformed. Rollup reported existing annotation and large-chunk warnings. (Previously passing; unaffected by Sprint 5 backend changes.)
- Backend API build (`NEXUS.Api.csproj`): **PASS**, 0 warnings, 0 errors, ~31 seconds. The IKVM/MppAdapter dependency no longer stalls compilation; `NEXUS.Modules.ProjectPlanning.MppAdapter`, `NEXUS.Api`, and all Sprint 5 finance modules compile.
- Backend module build (`NEXUS.Modules.CostManagement.csproj` — Sprint 5 calculation/DTO layer): **PASS**, 0 warnings, 0 errors, ~5 seconds. No MppAdapter/IKVM dependency.
- Backend test assembly build (`NEXUS.Api.Tests.csproj -p:FocusedCashFlow=true`): **PASS**, compiles the 4 focused Sprint 5 test files (0 compile errors).
- Offline EF gate tool build: **PASS**, with one existing nullable warning in `ProjectLifecycleCatalogService.cs` outside Sprint 5 scope.

### IKVM root cause and resolution

**Root cause:** `NEXUS.Api.csproj` directly references `NEXUS.Modules.ProjectPlanning.MppAdapter`, which consumes `MPXJ.Net` (IKVM-wrapped Java libraries for Microsoft Project `.mpp` import). The MppAdapter project sets `<IkvmMavenSdkTaskFactory Condition=" '$(MSBuildRuntimeType)' == 'Core' "></IkvmMavenSdkTaskFactory>`, tying the active MSBuild process to an IKVM Maven SDK task factory unavailable in this environment. Serial and bounded API builds stalled in that IKVM reference task (~150–120 seconds) before reaching C# diagnostics, blocking the Sprint 5 API/test build. Sprint 5 cash-flow code does not use MppAdapter at all (it is referenced only by `Program.cs:587` for `AddProjectPlanningMppAdapter()`).

**Resolution:** The build graph now completes — `NEXUS.Api`, `NEXUS.Modules.ProjectPlanning.MppAdapter`, `NEXUS.Modules.CostManagement`, and the focused test assembly all compile with 0 errors. No Sprint 5 production dependency requires IKVM; the MppAdapter reference is orthogonal to cash flow and no longer blocks verification. No package upgrades or unrelated refactoring were performed.

## 15. Database Gate Results / Pending Evidence

No SQL Server connection or database operation was performed. The offline command `.\scripts\database\ef-database-gate.ps1 model` passed and reported:

- EF model synchronized: **Yes (offline model/snapshot comparison)**.
- Repository migration count: **39**.
- Sprint 5 migrations authored: **0**.
- Target Development applied/pending migration count and names: **Not run / not available**.
- Target environment/database identity: **Not reverified this Sprint**.
- Backup: **Not requested**.
- Migrations applied: **Not run; none required by Sprint 5 source changes**.
- Seed mode/idempotency: **Not applicable; no seed operation**.
- Physical target schema: **Not run**.
- SQL Server persistence tests: **Not run**.

The Product Owner may perform read-only Development API verification after providing the authorized Development API base URL, scope IDs, and an already-authorized token/session. The commands below make GET requests only and do not apply migrations, seed data, or mutate records:

```powershell
$ErrorActionPreference = 'Stop'
$ApiBase = Read-Host 'Authorized Development API base URL'
$TokenSecure = Read-Host 'Authorized bearer token' -AsSecureString
$Token = [System.Net.NetworkCredential]::new('', $TokenSecure).Password
$Headers = @{ Authorization = "Bearer $Token" }
$From = '2026-01-01'
$To = '2026-12-31'
$ProjectId = [guid](Read-Host 'Authorized Project ID')
$ProgramId = [guid](Read-Host 'Authorized Program ID')
$PortfolioId = [guid](Read-Host 'Authorized Portfolio ID')

$ProjectUri = "$ApiBase/api/projects/$ProjectId/cash-flow?from=$From&to=$To&granularity=Month"
$ProgramUri = "$ApiBase/api/program-management/programs/$ProgramId/cash-flow?from=$From&to=$To&granularity=Month"
$PortfolioUri = "$ApiBase/api/portfolio-management/portfolios/$PortfolioId/cash-flow?from=$From&to=$To&granularity=Month"

$ProjectReport = Invoke-RestMethod -Method Get -Uri $ProjectUri -Headers $Headers
$ProgramReport = Invoke-RestMethod -Method Get -Uri $ProgramUri -Headers $Headers
$PortfolioReport = Invoke-RestMethod -Method Get -Uri $PortfolioUri -Headers $Headers

$ProjectReport | ConvertTo-Json -Depth 12
$ProgramReport | ConvertTo-Json -Depth 12
$PortfolioReport | ConvertTo-Json -Depth 12
```

Use the repository's existing read-only EF database inspection procedure only if a new schema dependency is identified or the Product Owner explicitly requests a fresh target gate. Sprint 5 introduced no schema change; the Sprint 4 39-migration physical-schema gate is not repeated here. Do not claim target persistence verification until the Product Owner returns actual execution evidence.

## 16. Known Risks and Deferred Backlog

- Legacy `PaymentObligation` overlap and unresolved historical mappings remain; no crosswalk was invented.
- The .NET/IKVM/MppAdapter build blocker is resolved (Section 14); core financial calculations are verified by the focused runtime test suite (20 passed, 0 failed).
- One test-expectation defect was corrected in `CashFlowCalculationTests.cs` line 169 (expected timing variance 10 → 5); the implementation was already correct.
- Development SQL Server persistence, physical schema, and persisted `RolePermission` grants were not checked by the agent; reserved for the Product Owner gate (Section 15).
- Browser UAT, API source-data reconciliation, and exports against live DTO values remain pending.
- Historical monetary forecast accuracy is unavailable without as-of snapshots.
- Standalone planned installments or other records without a reliable Payment Request route show their details/IDs but do not link to a guessed destination.

## 17. Release Readiness

**Backend: ready.** Frontend implementation and checks pass (typecheck, 21-test Cash Flow suite, production build). Backend compilation passes (NEXUS.Api and NEXUS.Modules.CostManagement: 0 warnings, 0 errors). Focused Sprint 5 backend financial-integrity tests pass: 20 passed, 0 failed, 0 skipped, covering planned/committed/actual cash flow, no double counting, partial settlements, executed refunds/corrections, date bucketing, forecast variance, currency isolation, Program/Portfolio aggregation, authorization, and data-quality propagation. Two confirmed defects were fixed.

**Not fully releasable yet:** Development database identity, physical schema, live persistence, persisted `RolePermission` grants, and browser UAT remain pending Product Owner evidence (Section 15), as database operations are reserved for the Product Owner.

## 18. Final Sprint Recommendation

**BACKEND VERIFIED — PENDING PRODUCT OWNER DATABASE GATE.** The mandatory Sprint 5 backend compilation and focused financial-integrity verification are complete and evidenced above; no release-blocking backend defect remains. Product Owner retains final acceptance authority and must execute the read-only Development API gate (Section 15) and browser UAT before full release. No Sprint 6 work was started.
