# Program and Portfolio Financial Reporting Contract

**Scope:** Financial Sprint 6, 2026-10-30
**Status:** Source implementation documented; focused React regressions pass, API build/test execution remains blocked, and owner runtime UAT is pending.

## Ownership and reporting population

Projects remain the only owners of financial records and write operations. Program and Portfolio reports are computed read models; they do not persist totals, create budgets, allocate funding, approve requests, record settlements, execute reversals, or close fiscal years.

The dedicated `financial-rollup` routes and Financials tabs read Project Annual Budgets, Payment Requests, Reversals, Settlements, Transfers, and Closings. They do not read or combine legacy Program/Portfolio overview financial columns. General Program/Portfolio overview and CRUD response DTOs no longer expose their legacy financial summary groups, availability placeholders, or Project budget/actual fields. Only the dedicated Financials tabs expose parent financial values, using the scoped rollup read models.

A Program population is the set of non-deleted Projects currently assigned to that Program. A Portfolio population is the union of non-deleted Projects assigned directly to that Portfolio with no Program and Projects assigned to its current Programs. Project IDs are deduplicated before aggregation. Inactive and archived but non-deleted Projects remain included. Deleted Projects are excluded. Reports use the current hierarchy; this source does not preserve historical Program/Portfolio assignment snapshots and does not provide historical as-of hierarchy reporting.

The backend limits the population to the authenticated Tenant and the caller's management Project scope. A role name alone, including the system role name `admin`, does not bypass parent or Project scope for financial rollups. Totals, counts, project pages, and hierarchy breakdowns use the same authorized population; no hidden Project amount or count is exposed.

## Fiscal year and currencies

The selected Fiscal Year is resolved with the authenticated Tenant's `financial.fiscalYearStartMonth` and configured timezone. When no year is selected, the Tenant's current fiscal year is used. Fiscal labels use the ending year and date ranges use the established date-only fiscal boundaries.

Every monetary aggregate is grouped by the exact currency code. There is no implicit FX conversion and no mixed-currency grand total. A currency group's ProjectCount is the count of validated Project contributions in that currency; the parent `includedProjectCount` and coverage fields also identify the authorized population and incomplete contributions.

## Financial measures and time dimensions

| Measure | Source and rule |
|---|---|
| Approved Annual Budget | Approved Project Annual Budget only. Draft/unapproved values are excluded. |
| Effective Active Allocation | Direct approved Payment Request allocations + incoming approved funding transfers - outgoing transfers - approved unpaid-commitment reversal releases. Each request is counted in its current funding assignment once. |
| Available Annual Budget | Approved Annual Budget - Effective Active Allocation. Actual Settlements are not subtracted again. |
| Total Unspent Budget | Approved Annual Budget - actual settlements within the selected Fiscal Year; closed periods use the immutable Project closing snapshot. This differs from Available Annual Budget because outstanding commitments remain allocated. |
| Payment Request counts and original approved amount | The request's original Fiscal Year association. These are historical request measures, not the current funding-year allocation. |
| Outstanding payable commitment | Current active funding assignment, after valid settlements and approved unpaid-commitment releases. |
| Actual paid | Approved Payment Request settlements whose actual payment date falls within the selected Tenant Fiscal Year. Legacy Payment Obligation settlements are excluded from this Payment Request metric. |
| Carry Forward | Report free and reserved incoming/outgoing carry separately from New Approved Funding. Never sum annual totals as lifetime funding. |
| Closing coverage | Project closing status for the selected Fiscal Year only. Parent reports are Reconciled only if all included Project periods are closed, authorization coverage is complete, and no financial data gaps/legacy unassociated approvals remain. |

For a closed period, immutable Project closing values take precedence over live recomputation for the historical statement figures. A Project closed in another year is not considered closed for the selected period. A Payment Request Settlement must reference the funding transfer whose destination Fiscal Year contains its payment date; a missing or mismatched link makes that Project contribution unavailable for reconciliation.

## Incomplete and legacy records

An approved legacy request without a verified Annual Budget association is counted as unresolved, and the affected Project contribution is unavailable so its settlement is not attributed to an unrelated approved budget. Missing or unapproved budgets, unknown/invalid currency, mismatched Tenant/Project/Budget links, invalid transfer chains, invalid settlement ownership/currency/Fiscal Year attribution, or allocations exceeding the approved budget are reported as missing/provisional data; unsupported contribution values are unavailable rather than guessed. Parent money totals include only validated Project contributions and remain labeled Provisional when any Project/source gap or partial authorization scope exists.

## Permissions and API

| Report | Required authorization |
|---|---|
| Program | `Program.View` and `finances.read` (`finances.write` also satisfies the existing API Finance.Read policy) |
| Portfolio | `Portfolio.View` and `finances.read` (`finances.write` also satisfies the existing API Finance.Read policy) |
| Project drill-down | Independently authorized by the existing Project Financial Details routes |
| Parent writes | Not supported |

Routes are `GET /api/program-management/programs/{programId}/financial-rollup` and `GET /api/portfolio-management/portfolios/{portfolioId}/financial-rollup`. Results include currency aggregates computed over the full authorized Project population, closing coverage, data-gap indicators, and paged Project details; Portfolios also return Program/direct-Project breakdowns. Currency groups do not embed unpaged Project contributions. Parent APIs re-check permission, Tenant, entity, and Project management scope. A parent report permission does not bypass authorization on a Project detail link.

## Verification boundary

The Finance legacy cleanup changed no EF entity, table, migration, or snapshot. The focused React regression group passed (7 files, 32 tests). The React production build is blocked by an unrelated duplicate JSX `className` in concurrently modified `OrganizationPage.tsx:513`. The API and Portfolio module builds returned failure without compiler diagnostics; the focused API regression-test command also returned exit code 1 without output. These are not successful verification results. SQL query plans, performance targets, runtime endpoint authorization, browser rendering, EN/AR/RTL visual validation, and full source-to-Project financial reconciliation remain pending owner/runtime verification. See the [Finance legacy cleanup report](FINANCIAL-LEGACY-CLEANUP.md), the [Sprint 6 implementation report](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md), and [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#sprint-6-program-and-portfolio-financial-rollup-deferred-to-sprint-7b).
