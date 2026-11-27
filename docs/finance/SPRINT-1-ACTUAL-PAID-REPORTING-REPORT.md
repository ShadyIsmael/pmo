# Sprint 1 / Task 02 — Actual Paid Reporting Integrity

**Repository:** C:\Projects\pmo
**Date:** 2026-11-21
**Status:** Implemented and verified with focused tests. No migration, database write, reset, reseed, or UAT was performed.
**Scope:** Unified Actual Paid calculation across both `PaymentSettlement` ownership rails and all affected reporting consumers.

## Documentation reused

- `TARGET_FINANCIAL_OPERATING_MODEL_v1.0.md` and `FINANCIAL_TECHNICAL_BLUEPRINT_v1.0.md` — approved lifecycle: Registered Planned Payment = Reserved, Approved Payment Request = Authorized, Confirmed Payment Settlement = Actual Paid; Outstanding Commitment is not Actual Paid.
- `FINANCIAL-CONSOLIDATION-AUDIT.md` §4.1/§4.3/§8 — confirmed the reporting blind spot (finding 3 and defect 5) and the shared `PaymentSettlements` XOR-owner ledger.
- `FINANCIAL_MIGRATION_CUTOVER_PLAN.md`, `FINANCIAL_IMPLEMENTATION_BACKLOG.md`, `FINANCIAL_DESIGN_GAPS_AND_RISKS.md` — migration safeguards and the pending decimal-precision migration (left untouched).
- `SPRINT-1-FINANCIAL-INTEGRITY-REPORT.md` — Development identity (`Shady / NexusPmo`, DB ID 5), pending migration `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision`, and the known blocked full test-project compilation.

No repository-wide re-audit was performed; only the code paths required for this correction were inspected.

## Root cause confirmed

`PaymentSettlement` is a single append-only settlement ledger with a database check constraint enforcing exactly one owner: `PaymentObligationId` XOR `ProjectPaymentRequestId`.

Reporting consumers resolved settlements **only through `PaymentObligationId`**:

- `ProjectReportingService.GetFinancialSummaryAsync` joined settlements to obligations by `PaymentObligationId`; request-owned settlements were never loaded.
- `ProjectStatusReportService.CaptureFinancialSnapshotAsync` did the same for `ActualPaidSnapshot` and the outstanding-planned-payment snapshots.
- `ProjectFinancialFoundationController` used `ProjectFinancialBalanceCalculator.CalculateActualPaid(obligations)`, which sums only `obligation.Settlements`.
- `ProjectAnnualBudgetClosingWorkflow` loaded all settlements in the fiscal year but required `PaymentSettlement.Currency` to be non-null and equal to the budget currency. Obligation settlements never carry `Currency` (the service does not write it), so they were flagged `settlement_amount_or_historical_currency_unresolved` and excluded from `ActualSettlementsInFiscalYear`.
- `ProgramPortfolioFinancialRollupService` loaded only `ProjectPaymentRequestId.HasValue` settlements, so obligation-rail cash was absent from the live `ActualSettlementsInFiscalYear` rollup even though closing snapshots are meant to cover the fiscal year.

## Correction implemented

### Shared calculation — `ActualPaidSettlementCalculator`

New file: `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs`

- `LoadForProjectAsync` loads confirmed (`!IsDeleted`) settlements for a project through **both** rails using owner-side joins:
  - Obligation rail: `PaymentSettlement` → `PaymentObligation` on `(TenantId, PaymentObligationId)`; project and currency resolve from the obligation.
  - Request rail: `PaymentSettlement` → `ProjectPaymentRequestDetail` on `(TenantId, ProjectPaymentRequestId = ApprovalRequestId)`; project resolves from the detail, currency from the settlement (required by `CK_PaymentSettlements_RequestCurrencyDescription`).
- The owning **Project** is resolved through the owner record, never trusted from `PaymentSettlement.ProjectId` alone; `TenantId` is applied explicitly so the same code works under `IgnoreQueryFilters` (financial-foundation controller) and under global query filters (module services).
- Optional `periodFrom`/`periodTo` date filtering; `SumActualPaid` sums only settlements whose resolved currency matches the normalized target — different currencies are excluded, never converted or merged.
- `HasUnresolvedCurrency` / `HasForeignCurrency` let consumers flag data-quality gaps instead of silently summing.

### Consumers updated

| Consumer | Change |
|---|---|
| `ProjectReportingService.GetFinancialSummaryAsync` | `ActualPaid` now sums both rails within the reporting period. Unresolved-currency settlements null `ActualPaid`; foreign-currency settlements are excluded and flagged via `hasDataQualityGap`. `settledByObligation` still drives the obligation-scoped Outstanding Planned Payments / Due / Remaining-Unpaid metrics. |
| `ProjectStatusReportService.CaptureFinancialSnapshotAsync` | `ActualPaidSnapshot` = obligation-rail settled total (unchanged basis) + request-rail settlements ≤ `PeriodTo` in project currency. Outstanding metrics remain obligation-scoped. New snapshots only; published snapshots are untouched. |
| `ProjectFinancialFoundationController` | Actual Paid = obligation settlements (existing calculator) + request-rail settlements, all-time. Comparability now also requires every request settlement's resolved currency to match the project currency; otherwise Actual Paid is `null` (fail-closed, unchanged posture). |
| `ProjectAnnualBudgetClosingWorkflow` | Settlement currency is resolved as `settlement.Currency ?? owning obligation.Currency` before the unresolved-currency issue check and the `actualPaid` sum, so valid obligation settlements count toward `ActualSettlementsInFiscalYear` in newly generated closing snapshots instead of being flagged as data issues. |
| `ProgramPortfolioFinancialRollupService` | Loads obligation-rail settlements (joined to obligations for project + currency) and adds currency-matched in-period amounts to `ActualSettlementsInFiscalYear`, aligning the live rollup with closing-snapshot semantics. |

Status-report **exports** return the persisted report DTO/snapshots, so they inherit the corrected snapshot values for newly generated reports; previously published snapshots are unchanged by design.

## Before / after examples

Project `P`, currency `USD`, reporting period 2026-11-01 → 2026-11-30:

| Settlement | Owner | Amount | Date |
|---|---|---|---|
| S1 | PaymentObligation O1 | 700 | 2026-11-05 |
| S2 | PaymentObligation O1 | 300 | 2026-11-20 |
| S3 | PaymentRequest R1 (approved) | 600 | 2026-11-12 |
| S4 | PaymentRequest R1 (approved) | 400 | 2026-11-25 |

- **Before:** `ActualPaid` = 1,000 (S1+S2 only); S3/S4 invisible to project financial summary, status snapshots, foundation endpoint, and the live rollup.
- **After:** `ActualPaid` = 2,000 (S1–S4, each counted once). `OutstandingPlannedPayments` remains obligation-scoped at `O1.Amount − 1,000`; request commitments stay under the annual-budget allocation model.

Approved request with no settlement → `ActualPaid` contribution 0. Approved reversal of unpaid commitment → no settlement is created or removed; historical `ActualPaid` is unchanged.

## Integrity rules preserved

- `CostEntry` is not treated as cash settlement; Actual Cost/EVM paths untouched.
- Settlements are append-only facts; no settlement, snapshot, approval, or audit record was modified.
- No double counting: the DB XOR-owner constraint plus owner-partitioned summation guarantee each settlement is counted exactly once.
- No currency conversion; mismatched/unresolved currencies are excluded and surfaced as data-quality gaps where the consumer supports it.
- No new Planned Payment aggregate, no approval-workflow change, no migration, no historical rewrite.

## Files modified

- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs` — **new** shared calculator.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectReportingService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportService.cs`
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs`
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs`
- `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioFinancialRollupService.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/ActualPaidReportingTests.cs` — **new** focused regression suite (8 tests).
- `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectFinancialSummaryPersistenceTests.cs` — stale `CommittedCost` assertion replaced; fixture moved from SQLite `EnsureCreated` (broken by SQL Server-specific `nvarchar(max)` schema) to EF InMemory; two stale assertions aligned with current fiscal-boundary and `Expenditure` semantics; added to the `Static tenant resolution` collection.
- `apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj` — new `FocusedActualPaidReporting` flag following the repo's focused-test pattern.
- This report; `FINANCIAL-CONSOLIDATION-AUDIT.md` finding annotation.

## Build and test evidence

| Check | Outcome |
|---|---|
| `dotnet build src/NEXUS.Api/NEXUS.Api.csproj` | **Passed**, 0 warnings, 0 errors |
| Focused suite `dotnet test -p:FocusedActualPaidReporting=true` | **15/15 passed** (8 new Actual Paid tests + 7 financial-summary tests) |
| Full `NEXUS.Api.Tests` build | **Still blocked** by the same pre-existing unrelated compilation errors documented in Sprint 1 Task 01 (`PaymentRequestStatuses`, `ITenantFiscalYearService`, `FiscalYearCalendarService`, `AddCanonicalFinancialActionPolicies`, ambiguous `IAuthorizationService`, `NoopProjectAnnualBudgetApprovalNotificationPublisher`, `ProjectMember`); none are in files touched by this task |
| SQL Server persistence tests / API test host | Not run (same blockers as Task 01; no database writes were authorized or needed) |
| Agent-executed UAT | Not run |

New test coverage maps to the required scenarios: obligation-only, request-only, both rails, multiple partial settlements, project/tenant isolation, period boundaries, no double counting, approved-request-without-settlement = 0, reversal-does-not-reduce-actual-paid, and currency separation. Historical published snapshots remain unchanged because snapshot capture only runs at report creation; existing snapshot persistence tests were left intact.

## Remaining limitations and dependencies

1. **Obligation settlements with an explicit `Currency` differing from the obligation** now resolve to the settlement's own currency (previously the obligation currency always won). The write path never sets it, so this only affects hand-edited/legacy rows; it is the more truthful interpretation and is flagged as a data-quality gap when it diverges.
2. **Status-report snapshots have no data-quality flag** for foreign/unresolved settlement currencies; non-matching settlements are excluded from `ActualPaidSnapshot` (same tolerance as the existing currency-filtered obligations). Consider a snapshot-level quality indicator in a later sprint.
3. **Request settlements whose `Currency` is somehow null** resolve as unresolved → excluded + gap in reporting service; impossible under the SQL Server check constraint.
4. The pending decimal-precision migration remains unapplied and untouched; the Development database was not written to.
5. Portfolio/program rollup now matches closing semantics for the live (non-closed) path; already-`Closed` years continue to use the frozen `ActualSettlementsInFiscalYear` snapshot value, which was computed under the old rules — historical snapshots are intentionally not rewritten.
6. Full-suite test compilation and SQL Server persistence verification remain blocked by the pre-existing unrelated test-project errors tracked since Sprint 1 Task 01.
