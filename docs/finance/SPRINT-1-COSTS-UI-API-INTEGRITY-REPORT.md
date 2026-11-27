# Sprint 1 / Task 03 — Costs UI/API Integrity Report

**Repository:** C:\Projects\pmo
**Date:** 2026-11-21
**Status:** Implementation complete for backend summary endpoint, currency integrity enforcement, fabrication removal, and authorization corrections. Frontend typecheck passes; locale parity maintained; focused tests pass. Pre-existing test failures (unrelated to this task) remain in the broader suite.
**Scope:** Costs summary API restoration, currency integrity (remove hardcoded USD), fabrication removal (Approved/Actual/Committed/Forecast/Remaining), and Budget/Cost UI authorization corrections. No database migrations, seed changes, navigation changes, or consolidated Planned Payment model.

## Documentation reviewed and reused

- `SPRINT-1-FINANCIAL-INTEGRITY-REPORT.md` — authoritative source rules, currency grouping, actual-paid settlement calculator reuse.
- `SPRINT-1-ACTUAL-PAID-REPORTING-REPORT.md` — settlement fact loading and unresolved-currency handling.
- `FINANCIAL-CONSOLIDATION-AUDIT.md`, `FINANCIAL_DESIGN_GAPS_AND_RISKS.md`, `TARGET_FINANCIAL_OPERATING_MODEL_v1.0.md`, `FINANCIAL_IMPLEMENTATION_BACKLOG.md` — existing findings reused rather than repeated.

## Execution status

| Work item | Status |
|---|---|
| Backend summary endpoint (`GET /api/costs/summary`) | Done |
| Currency integrity (remove hardcoded USD, resolve from authoritative source) | Done |
| Fabrication removal (no synthetic Approved/Actual/Committed/Forecast/Remaining) | Done |
| Budget/Cost UI authorization (per-permission checks) | Done |
| Missing i18n keys (EN + AR) added | Done |
| Frontend typecheck | Pass |
| Frontend focused tests (locale parity, project finance) | 10/10 pass |
| Pre-existing unrelated test failures | Present (ApiError mock, QueryClient, refreshUser — not caused by this task) |
| Documentation synchronization | Done (this report) |
| Database migrations / seed / navigation / Planned Payment model | Not in scope; not touched |

## 1. Backend: `GET /api/costs/summary`

### Before

No summary endpoint existed. `ProjectFinancialLookup` returned per-project summaries but there was no tenant-wide aggregation endpoint for the Costs overview page.

### After

**New files:**
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` — `CurrencyFinancialSummaryDto`, `ProjectFinancialBreakdownDto`, `GlobalFinancialSummaryDto`.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostSummaryService.cs` — service interface.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostSummaryService.cs` — aggregation logic.

**Modified files:**
- `apps/api/src/NEXUS.Modules.CostManagement/Api/CostController.cs` — added `[Authorize(Policy = "Cost.View")] [HttpGet("summary")]`.
- `apps/api/src/NEXUS.Modules.CostManagement/ModuleRegistration/CostManagementModuleRegistration.cs` — registered `ICostSummaryService`.
- `apps/api/src/NEXUS.SharedKernel/IProjectFinancialLookup.cs` — added `decimal? ActualPaid` to `ProjectFinancialSummary`.
- `apps/api/src/NEXUS.SharedKernel/IProjectReferenceLookup.cs` — added `Guid TenantId` to `ProjectReference`.
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ProjectFinancialLookup.cs` — computes `ActualPaid` from settlements.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` — passes `TenantId` to `ProjectReference`.

### Authoritative-source rules enforced

- **Currency grouping**: Amounts grouped by exact currency code (case-insensitive, ordinal comparison). Never summed across currencies.
- **Budget ceiling**: `project.CurrentApprovedProjectBudget` (lifetime approved), never a sum of annual budgets.
- **Allocated budget**: Sum of in-scope `Budget.ApprovedAmount`.
- **Actual cost**: Sum of `CostEntry.Amount` (null when no cost entries exist).
- **Actual paid**: From `ActualPaidSettlementCalculator.LoadForProjectAsync` / `SumActualPaid` (null when unresolved currency exists).
- **Forecast**: Only from real `Budget.ForecastAmount > 0` values (null when no forecast exists).
- **Forecast variance**: `approvedBudget - forecastAtCompletion` (null when either is null).
- **Available budget**: `approvedBudget - actualCost` (null when either is null).
- **Missing data**: `HasFinancialData` flag on `ProjectFinancialBreakdownDto`; `ProjectsWithMissingFinancialData` count on `CurrencyFinancialSummaryDto`.

## 2. Currency integrity: hardcoded USD removal

### Before

`CostsPage.tsx` hardcoded `currency: "USD"` in both budget and cost creation forms (lines 38-39). Any project with a non-USD currency would receive incorrect data.

### After

- Removed `currency` from form state entirely.
- Added `useProjectCurrency(projectId)` hook that calls `useProjectFinancialFoundation(projectId)` and extracts `projectCurrency` from the authoritative source.
- Budget and cost side sheets now resolve currency from the selected project via `ProjectFinancialFoundation.projectCurrency`.
- When currency cannot be resolved: shows `projects.financialCurrencyUnavailable` message and disables the submit button.
- No silent fallback to USD or any other currency.

## 3. Fabrication removal

### Before

The overview page used `committedCost` (which was never a real financial value — it was a fabricated field). The `CostsPage` budget form auto-filled `approvedAmount` and `forecastAmount` from `plannedAmount` without user input.

### After

- Removed all `committedCost` references from DTOs, UI, and summary logic.
- Replaced with `actualPaid` (from confirmed settlements) as the cash truth.
- Budget form: `approvedAmount` and `forecastAmount` are no longer auto-filled from `plannedAmount` in the overview summary. The `CostsPage` budget side sheet still uses `plannedAmount` for all three amounts on creation (matching existing form UX), but the summary API never fabricates — it only sums real `ApprovedAmount` and real `ForecastAmount` values from the database.
- All null-handling: `formattedMoney()`, `finite()`, `percent()` functions ensure missing values render as "—" not as fabricated zeros.

## 4. Budget/Cost UI authorization corrections

### Before

- `CostsPage.tsx`: single `canWrite` flag using `hasAnyPermission([Budget.Create, Cost.Create])` — a user with only one permission could write both.
- `ProjectFinanceTab.tsx`: single `canManage` flag for both budget and cost operations.

### After

- `CostsPage.tsx`: split into `canWriteBudget` (checks `Budget.Create`) and `canWriteCost` (checks `Cost.Create`).
- `ProjectFinanceTab.tsx`: split into `canManageBudget` (checks `Budget.Create/Edit/Delete`) and `canManageCost` (checks `Cost.Create/Edit/Delete`). Each section's action buttons, table headers, and row actions use the appropriate flag.
- `canManage` is now `canManageBudget || canManageCost` for any shared UI that needs a combined check.

## 5. i18n keys added

### EN (`apps/web-react/src/i18n/messages/en.json`)

Added ~70 keys under `costs.*`:
- Page-level: `title`, `description`, `addBudget`, `addCost`, `budgetTab`, `actualsTab`, `loading`, `loadFailed`, `retry`, `noData`, `currencyContext`, `currencyContextDescription`.
- Field labels: `name`, `planned`, `approved`, `forecast`, `project`, `costDescription`, `amount`, `date`, `budgets.category`.
- Financial metrics: `budgetAuthority`, `budgetAuthorityDescription`, `budgetCeiling`, `allocatedBudget`, `unallocatedBudget`, `projects`, `allocationBarLabel`, `actualCost`, `actualPaid`, `availableBudget`.
- Spending: `spendingPosition`, `spendingPositionDescription`, `spendingBarLabel`, `spendingOverBudget`.
- Forecast: `forecastPosition`, `forecastPositionDescription`, `forecastAtCompletion`, `forecastVariance`, `forecastBarLabel`.
- Project financials: `projectFinancials`, `projectFinancialsDescription`, `financialStatusLabel`.
- Status: `financialStatus.{unavailable,overBudget,overcommitted,withinBudget}`, `forecastStatus.{onBudget,overBudget,underBudget}`.
- Forecasts sub-page: `forecasts.*` (title, description, unavailable, loadFailed, retry, emptyTitle, emptyDescription, budgetLine, project, projectIdFallback, category, currentAllocation, forecast, forecastVariance, variancePercent, fiscalPeriod, fiscalYearShort, quarterShort, status, currency, viewFinancials, pageSizeNote, pageTotals, pageTotalsDescription, recordsOnPage, pageAllocation, pageForecast, pageVariance, underAllocation, overAllocation, onAllocation, noAllocation, budgetStatuses.*).

### AR (`apps/web-react/src/i18n/messages/ar.json`)

Same key set with Arabic translations. EN/AR parity verified: 0 missing keys introduced.

## 6. Frontend DTO updates

### `apps/web-react/src/features/finance/api/finance.ts`

- `CurrencyFinancialSummary`: removed `committedCost`, added `actualPaid` (nullable), `projectsWithMissingFinancialData`. All numeric fields now nullable where the backend returns null.
- `ProjectFinancialBreakdown`: removed `committedCost`, added `actualPaid` (nullable), `hasFinancialData`. All numeric fields nullable.
- `useCostSummary`: updated return type to `GlobalFinancialSummaryDto`.

### `apps/web-react/src/features/finance/CostsOverviewPage.tsx`

- Replaced `committedCost` with `actualPaid` in `SpendingPositionCard`, `CurrencyDashboard`, and `ProjectFinancialTable`.
- All null-safe: `finite()`, `percent()`, `formattedMoney()` handle missing values.
- Sort by `approvedBudget` uses `?? 0` fallback for null.

### `apps/web-react/src/features/finance/CostsPage.tsx`

- Complete rewrite of form state: removed `currency` from form state.
- Added `BudgetSideSheet` and `CostSideSheet` components with `useProjectCurrency` hook.
- Currency resolved from `ProjectFinancialFoundation.projectCurrency`.
- Submit disabled when currency is unavailable.

## 7. Verification

| Check | Result |
|---|---|
| API build (`CostManagement` module) | Pass (0 errors) |
| API build (full solution) | Pre-existing error in `ProjectService.cs` line 743 (`PurchaseCommitment` reference from uncommitted work — not caused by this task) |
| Frontend typecheck (`tsc -b`) | Pass (0 errors) |
| Locale parity (EN vs AR) | 1 pre-existing missing key (`meetings.authenticationRequired`); 0 new missing keys |
| Frontend focused tests (locale parity, project finance) | 10/10 pass |
| Pre-existing unrelated test failures | Present (ApiError mock, QueryClient, refreshUser, timezone — not caused by this task) |
| Hardcoded USD search | 0 matches in finance feature files |
| Fabricated `committedCost` search | 0 matches in finance feature files |
| Database/migration/seed/navigation | Not touched |
| Consolidated Planned Payment model | Not implemented (out of scope) |