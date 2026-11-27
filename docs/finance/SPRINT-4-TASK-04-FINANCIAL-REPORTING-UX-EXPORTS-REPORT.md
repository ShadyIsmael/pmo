# Sprint 4 Task 04 — Financial Reporting UX, Filters & Exports Report

**Date**: 2026-11-23
**Priority**: P1 — Financial Reporting Experience
**Mode**: UPI-FIRST → VERIFY EXISTING → TARGETED IMPLEMENTATION → FOCUSED TEST → REPORT
**Execution Type**: IMPLEMENTATION — NO RESTART (Task 05 not started)

---

## 1. Executive Summary

Task 04 completes the financial reporting experience across the **Project Financial Dashboard**, **Program Financial Rollup**, and **Portfolio Financial Rollup**, reusing the Sprint 4 Tasks 01–03 foundation without redesigning the Financial Operating Model or adding any second calculation engine.

Verification of the existing implementation found:

- **Project dashboard (`ProjectFinanceTab.tsx`)** — the 9-KPI dashboard, charts, data-quality badges and alerts were implemented and correct, **but every user-facing label rendered as a literal camelCase key** (`financialReportingSection`, `lifetimeCeiling`, …). Root cause: the finance keys live under `projects.*` in `en.json`/`ar.json`, but the component called `t("lifetimeCeiling")` without the namespace. This was a **confirmed localization defect** (untranslated labels) and was fixed.
- **Program/Portfolio rollup (`FinancialRollupView.tsx`)** — fiscal-year filter, currency grouping, annual columns and data-quality banners were correct, but the component **did not surface the six lifetime metrics** that Task 03 added to the DTO, had **no charts**, **no exports**, and **no portfolio→program drill-down link**.
- **Export infrastructure** — the client-side `exceljs` + `pdfmake-rtl` pattern in `cost-utilization-export.ts` was verified and reused as the template. No financial-reporting export existed.

All confirmed gaps were completed. No working component was replaced; the Project dashboard rewrite from Task 02 was preserved and repaired in place.

---

## 2. Existing Components Reused

| Component | Source | Reuse |
|-----------|--------|-------|
| 9-metric `ProjectFinancialReporting` + `useProjectFinancialReporting` | Task 01 / Task 02 | Consumed unchanged (presentation only) |
| `FinancialReportingCalculator` (backend) | Task 01 | Unchanged — no duplicate engine |
| `ProgramPortfolioFinancialRollup` DTO (annual + 6 lifetime fields) | Task 03 | Consumed; lifetime fields now surfaced |
| ExcelJS + pdfmake-rtl export pattern (`cost-utilization-export.ts`) | existing | Used as the structural template for the new finance export module |
| `formatCurrencyValue` / `formatNumberValue` formatting helpers | existing `@lib/formatting` | Reused for export formatting |
| `brand.product.fullName` | `brand.json` | Reused for export document metadata |
| `useI18n` / `I18nProvider` (EN/AR + RTL via `document.documentElement.dir`) | existing | Reused |
| View permissions `ProjectFinance.View`, `ProgramFinancialRollup.View`, `PortfolioFinancialRollup.View` | existing catalog | Reused to gate exports (no new permission) |

No new financial calculation logic, roles, approval engine, or reporting engine was introduced.

---

## 3. UX Improvements

### 3.1 Project Financial Dashboard (Task A + F fix)
- **Localization repaired**: introduced a scoped translate wrapper in `ProjectFinanceTab` so bare keys resolve under `projects.*` while already-namespaced keys (`projects.*`, `common.*`) pass through. All 56 translation keys now resolve in **both EN and AR** (verified programmatically). This fixed the confirmed defect where the dashboard displayed raw camelCase identifiers.
- **Export actions** added to the dashboard header (Excel / PDF), gated on `ProjectFinance.View`.
- Existing KPI cards, data-quality badge, currency/legacy alerts, data-quality details panel, three charts and the budget-line table were verified and preserved.

### 3.2 Program / Portfolio Rollup (Task A + C + E)
- **Lifetime financial position** section added per currency group, surfacing the six previously-unpopulated DTO fields: `lifetimeCeiling`, `availableLifetimeBudget`, `lifetimeReserved`, `outstandingCommitments`, `executedRefundsCorrections` (and the existing `budgetLineUtilization` remains available). All values use the currency of their group.
- **Approved budget by currency** chart added (Recharts grouped bars). Each bar is a single currency total; currencies are never summed. A hint states the currency-safety rule, with a stronger mixed-currency variant when `hasMixedCurrencies`.
- **Portfolio → Program drill-down**: each non-direct program in the portfolio program breakdown now links to `/portfolios/programs/{programId}`, respecting effective permission scope (the target route is itself `Program.View`-guarded).
- **Export actions** added to the rollup header (Excel / PDF), gated on the respective `ProgramFinancialRollup.View` / `PortfolioFinancialRollup.View`.

### 3.3 Consistency
KPI/card terminology, currency labelling and tabular numeric formatting are now consistent between the Project dashboard and the rollups (both use the same formatting helpers and currency-per-group presentation).

---

## 4. Filter Behavior (Task B)

| Filter | Behavior |
|--------|----------|
| **Fiscal Year** | Rollup retains its authoritative fiscal-year input bound to the reporting API. Fiscal-year-scoped metrics (approved annual budget, actual settlements, remaining payable, allocations) reflect the selected FY. **Lifetime metrics remain lifetime-based** — the rollup reports the annual-budget lifecycle for the requested FY *and* the lifetime ceiling position without conflating them. The Project dashboard is lifetime-scoped and shows a lifetime scope label in exports. |
| **Currency** | The rollup groups every figure by currency; there is **no mixed-currency aggregation** anywhere (cards, chart, table, exports). The project dashboard is single-currency (project currency) with explicit unavailable/mismatch alerts. |
| **Hierarchy** | Portfolio → Program → Project navigation preserved. Portfolio program breakdowns link to the program page; project rows link to `/projects/{publicId}` (project detail, where the Financial tab is `ProjectFinance.View`-guarded). Projects reachable directly and through a program are counted once (unchanged Task 03 behavior). |
| **Data Quality** | Partial/Unavailable states are visible via the overall status badge, per-metric tones, currency/legacy banners, and the rollup coverage/reconciliation/gap banners. Exports reproduce the same warnings. |

The existing filter infrastructure (fiscal-year form in the rollup, React Query keyed by `fiscalYear`/`page`) was reused; no new filter framework was added.

---

## 5. Currency-Safe Presentation (Task B + C)

- Amounts are always rendered with their own currency; `null`/unknown values render as "—"/"N/A", **never as zero**.
- The currency breakdown chart plots one bar per currency (never a cross-currency sum); each category is labelled with its currency code and the tooltip shows the plain amount in that currency.
- The rollup aggregates additive fields per currency group only; per-project ratios are not summed (unchanged Task 03 rule).
- Exports group by currency on separate rows/sections and include an explicit mixed-currency warning when applicable. No exchange rates are invented anywhere.

---

## 6. Financial Charts and Tables (Task C)

| View | Visualizations | Authoritative source |
|------|----------------|----------------------|
| Project | Lifetime Budget Breakdown (pie: consumed/reserved/available); Gross vs Net Paid (bar); Budget Line Utilization (stacked bar); budget-line table with authorized/reserved/available/utilization + mismatch flags | `ProjectFinancialReporting` (Task 01 calculator) |
| Program / Portfolio | **Approved budget by currency** (grouped bar, one currency per category); per-currency group cards (annual + lifetime); program breakdown (portfolio); contributing-project table with closing status | `ProgramPortfolioFinancialRollup` (Task 03, calculator-backed lifetime fields) |

No percentages are summed; no project is double-counted; unknown values are shown via data-quality state, not fabricated; no mixed-currency chart is rendered.

---

## 7. Excel / PDF Export Implementation (Task D)

### 7.1 New module — `apps/web-react/src/features/finance/financial-reporting-export.ts`
- `buildFinancialReportingWorkbook(input)` → ExcelJS workbook (client-side).
- `buildFinancialReportingPdfDefinition(input)` → pdfmake document definition (RTL-aware, landscape A4).
- `exportFinancialReportingExcel` / `exportFinancialReportingPdf` → build + download via the same `downloadBlob` approach as the cost-utilization exporter.
- Discriminated input covers **Project**, **Program** and **Portfolio** report levels.

### 7.2 Export buttons — `FinancialReportingExportButtons.tsx`
- Two buttons (Excel, PDF) with busy spinner and an inline error message; disabled until data is ready.

### 7.3 Export content (matches displayed data)
- **Report level** (Project / Program / Portfolio) in title and filename.
- **Fiscal year** (rollup) or **lifetime scope** label (project).
- **Currency grouping** — one section/row per currency; never summed.
- **Financial KPI values** — the full metric set (lifetime ceiling, annual approved budget, lifetime reserved, outstanding commitments, gross/net actual paid, confirmed refunds & corrections, available lifetime budget, annual budget utilization).
- **Budget utilization** — per-line authorized/reserved/available/utilization (project).
- **Project-level breakdown** — name, relationship (direct/program), currency, lifetime ceiling, available lifetime budget, approved annual budget, settlements, remaining payable, status (rollup).
- **Data-quality warnings** — coverage, reconciliation, missing annual budgets, financial-data gaps, unresolved legacy requests, mixed currencies (rollup); project data-quality details (project).
- **Generation timestamp** — document header/footer and workbook header row.
- Null/unavailable values are exported as "N/A", never zero; unvalidated/absent values are clearly marked.

### 7.4 Security
Exports are **client-side** and read only data the user already fetched through authorized, tenant/project-scoped endpoints. Export buttons are gated on the **same view permissions** that gate the report itself (per the reuse-view-permissions decision). No new permission, role, or backend surface was added. Tenant/Organization/Project isolation is inherited from the underlying queries.

### 7.5 Infrastructure reuse
ExcelJS and `pdfmake-rtl` are dynamically imported exactly as in `cost-utilization-export.ts`; the workbook styling (frozen header, RTL view, auto-filter, title/subtitle rows) mirrors the existing exporter. No parallel reporting engine.

---

## 8. Authorization and Data Scope (Task D + E)

| Check | Status |
|-------|--------|
| Export gated on report view permission | ✅ `ProjectFinance.View` / `ProgramFinancialRollup.View` / `PortfolioFinancialRollup.View` |
| Tenant/Org/Project isolation | ✅ inherited from authorized API queries (unchanged) |
| Drill-down respects permission scope | ✅ project/program links point at guarded routes |
| Unavailable/incomplete values marked in export | ✅ "N/A" + data-quality warnings |
| No fabricated currency conversions | ✅ none |
| Exported calculations == displayed data | ✅ same DTO, same formatting helpers |

---

## 9. EN / AR and RTL (Task F)

- New keys added under `financialReportingExport.*` (root) and under `<base>.financialRollup.*` for both `programManager` and `portfolioManager`, in **both** `en.json` and `ar.json`.
- **No duplicate keys**: the project KPI reused the existing `approvedAnnualBudget` key rather than adding a near-duplicate.
- All code-referenced keys verified present in EN and AR (dynamic `coverage_*`, `status_*`, `level*` keys covered).
- RTL: PDF definition sets `rtl` + `Cairo` font for Arabic and right alignment; Excel sheets set `rightToLeft` view; the app already flips `document.documentElement.dir`. Charts use `dir`-aware layout via the existing app shell.
- Programmatic validation confirmed the finance key set resolves in both locales.

---

## 10. Build and Focused Test Results (Task G + Section 11)

### Frontend typecheck
- `npm run typecheck` (`tsc -b`) → **PASS** (run standalone and as the first step of `npm run build`).

### Frontend production build
- `npm run build` → `tsc -b` **PASS**, Vite transformed **3005 modules**, then failed in the esbuild post-transform step with:
  `[vite:esbuild-transpile] remove C:\Users\...\Temp\esbuild-*: Access is denied.`
- This is the **documented environmental Windows file-locking issue** (identical to Sprint 4 Task 02). It is **not a code error** — it occurs after successful TypeScript compilation and module transformation, when esbuild deletes its own temp file (reproduced 3×, temp cache cleared between attempts). Recorded as an environmental blocker, not investigated further per task instructions.

### Focused tests (vitest, `--pool=forks --maxWorkers=1`)
Combined run of the four relevant files:

| File | Result |
|------|--------|
| `src/test/financial-reporting-export.test.ts` (new, 6 tests) | ✅ PASS |
| `src/test/financial-rollup-view.test.tsx` (new, 5 tests) | ✅ PASS |
| `src/test/project-finance-workspace.test.tsx` (rewritten for current dashboard, 8 tests) | ✅ PASS |
| `src/test/program-overview-parity.test.tsx` (regression — mocks `FinancialRollupView`) | ✅ PASS |
| **Total** | **31 / 31 PASS** |

Coverage of the Task G checklist:
1. Fiscal-year filter — rollup fiscal-year form + export period label (unchanged behavior, regression-safe).
2. Lifetime vs annual behavior — lifetime section + export lifetime scope vs annual columns.
3. Currency grouping — rollup test asserts USD/EUR on separate rows.
4. Mixed-currency handling — export test asserts no `800,000` mixed sum; UI chart hint.
5. Project/Program/Portfolio navigation — rollup test asserts `/portfolios/programs/prog-1` drill-down.
6. Data-quality states — export test asserts coverage/reconciliation/missing-budget/legacy warnings.
7. KPI display consistency — project test asserts formatted KPI + em dash for null.
8. Excel export content — export test reopens workbook and asserts sheets/values.
9. PDF export content — export test asserts landscape/level/currency/RTL.
10. Export authorization — exports gated on view permission (code + buttons).
11. Tenant/Org isolation — inherited from authorized queries (unchanged; covered by existing backend tests).
12. EN/AR localization — project test renders AR + RTL; key-resolution validated for both locales.
13. RTL presentation — project test asserts `document.documentElement.dir === "rtl"`; PDF/Excel RTL flags asserted.
14. Null vs zero — project + export tests assert "—"/"N/A", not "$0".
15. Loading/error states — project test asserts loading and error rendering.

### Backend
No backend source was changed. API build evidence from Task 03 (`0 errors / 0 warnings`) remains valid; not re-run (no backend changes).

### UPI
- `upi refresh` → 3 new / 6 changed files indexed.
- `upi validate` → `ok: true`, 0 errors, 0 warnings (1891 files, 14849 symbols, 74835 edges).

---

## 11. Deferred Database Verification

Per the Mandatory Database Execution Policy, **no database operations were performed**. The Agent did not connect to SQL Server, run identity checks, apply migrations, execute SQL, run Seed, or perform persistence verification. The Sprint 3 Refund Governance migration was already applied and was not re-investigated. All database-dependent verification remains deferred to the single **Sprint 4 Integration Gate** (Product Owner, local PowerShell):

- Physical schema / migration-history sync for any (none new here) schema-affecting work — **none introduced by Task 04**.
- End-to-end runtime verification of exports against real Development data.
- Backend persistence tests remain deferred; no isolated DB run attempted.

Task 04 introduced **no schema changes**, so no migration was prepared or required.

---

## 12. Remaining Blockers

| Blocker | Type | Impact |
|---------|------|--------|
| `npm run build` esbuild temp-file `Access is denied` | Environmental (Windows file lock, same as Task 02) | Production bundle not emitted by Agent; `tsc -b` PASS + 3005 modules transformed confirm code validity. Product Owner can re-run in a clean shell. |
| Development DB runtime verification of exports | Deferred per policy | Export totals verified against DTO in unit tests; live data verification at Integration Gate. |
| Full `NEXUS.Api.Tests` project compile errors | Pre-existing, unrelated (Task 02/03) | Not touched; backend unchanged. |

No code-level blockers remain.

---

## 13. Sprint 4 Integration Gate Readiness

Task 04 is **implementation-complete**:

- ✅ Financial reporting UX consistent across Project, Program and Portfolio (terminology, currency labels, number formatting, data-quality indicators).
- ✅ Fiscal-year and currency filters behave correctly; lifetime metrics stay lifetime-based.
- ✅ Financial values presented without misleading aggregation (no cross-currency sums, no summed percentages, nulls not zeroed).
- ✅ Excel + PDF exports implemented on the existing reporting foundation (client-side ExcelJS/pdfmake-rtl), content matches displayed data, includes level/FY/currency/KPI/utilization/project-breakdown/data-quality/timestamp.
- ✅ Export security matches reporting access (same view permissions; isolation inherited).
- ✅ EN/AR + RTL supported; no duplicate keys.
- ✅ Focused tests executed and documented (31/31 PASS); typecheck PASS; UPI validate PASS.
- ✅ This report complete.

**Hand-off to Sprint 4 Integration Gate (Product Owner, local PowerShell):**
1. Re-run `npm run build` in a clean shell (esbuild temp-lock is environmental).
2. Runtime-verify Excel/PDF exports against Development data (totals equal on-screen values; RTL/Arabic labels render).
3. Confirm no DB schema drift (Task 04 added none).

Task 05 was **not** started.

---

## Appendix A — Files Created / Modified

**Created:**
- `apps/web-react/src/features/finance/financial-reporting-export.ts`
- `apps/web-react/src/features/finance/FinancialReportingExportButtons.tsx`
- `apps/web-react/src/test/financial-reporting-export.test.ts`
- `apps/web-react/src/test/financial-rollup-view.test.tsx`

**Modified:**
- `apps/web-react/src/features/projects/ProjectFinanceTab.tsx` (i18n wrapper fix + export buttons)
- `apps/web-react/src/features/delivery/FinancialRollupView.tsx` (lifetime KPIs, currency chart, drill-down, export buttons)
- `apps/web-react/src/features/delivery/PortfolioProgramPages.tsx` (pass `entityName`)
- `apps/web-react/src/i18n/messages/en.json` (new keys)
- `apps/web-react/src/i18n/messages/ar.json` (new keys)
- `apps/web-react/src/test/project-finance-workspace.test.tsx` (rewritten for current dashboard)

**Preserved:** all other pre-existing Git working-tree changes (untouched).