# Sprint 5 — Task 02: Cash Flow Calendar

**Status:** IMPLEMENTED — UNVERIFIED  
**Owner:** Frontend & UX; Finance and QA support  
**Date:** 2026-11-23

## Outcome

Added a localized daily/monthly Project Cash Flow Calendar in the existing Project Financial Workspace. It uses the authoritative Project Cash Flow API, shows Planned, Committed, Actual Gross/Net and executed adjustments separately, supports date navigation, fiscal-year and currency filters, overdue indicators, undated details, and Partial/Unavailable quality notices.

Selected-day details show descriptions, status, amount, currency, expected/event dates, and linked installment/request/obligation identifiers. When an item has a linked Payment Request and the user has `ProjectPaymentRequest.View`, its existing request detail side sheet opens in view-only mode. Items without a reliable request link do not get a fabricated destination. No new permissions or grants were added.

Excel/PDF actions export the current Project Cash Flow DTO and its date range, currency groups, details, timing variance, and quality notices through the existing export libraries.

Key files include `CashFlowCalendar.tsx`, `cash-flow-calendar.ts`, `api/project-cash-flow.ts`, `ProjectFinanceTab.tsx`, `CashFlowExportButtons.tsx`, and `cash-flow-export.ts` under `apps/web-react/src/`.

## Verification

- Frontend typecheck: **PASS**.
- Focused Cash Flow frontend tests: **21 passed across 4 files**; includes linked/unlinked source-action behavior.
- Frontend production build: **PASS**; 3,015 modules transformed. Existing Rollup annotation and large-chunk warnings remain.
- English and Arabic locale files parse as JSON. Cash Flow locale subtree key counts match (Project calendar 37, forecast 27, hierarchy 50, export 15).

Backend API compilation and runtime authorization remain unverified; see Task 01 and the consolidated closure report. No Agent UAT or database access was performed.
