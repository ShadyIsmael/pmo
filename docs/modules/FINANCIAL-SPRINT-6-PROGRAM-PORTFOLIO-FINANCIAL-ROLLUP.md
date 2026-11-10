# Financial Sprint 6 — Program & Portfolio Financial Rollup

**Date:** 2026-10-30
**Status:** SOURCE IMPLEMENTED / STATIC REVIEW COMPLETE / COMPILE NOT VERIFIED / EF RECONCILIATION DEFERRED / DATABASE UNCHANGED / RUNTIME UAT DEFERRED

## Scope delivered

Program and Portfolio Financial Overview are read-only computed views over Project financial records. Projects remain the only owners of Annual Budgets, Payment Requests, approval decisions, settlements, reversals, carry-forward funding, and closing statements. No parent budget, allocation, financial write endpoint, summary table, duplicate ledger, or Sprint 6 EF migration was added.

The dedicated Financials tabs use the `financial-rollup` endpoints and Project Annual Budget model. Legacy financial values on Program/Portfolio overview read models are not queried or combined into these rollups; the existing overview summary projections leave those legacy financial values unavailable/empty.

The Program API aggregates Projects currently assigned to that Program. The Portfolio API aggregates direct Portfolio Projects (where `ProgramId` is null) and Projects assigned to its current Programs, deduplicating Project IDs. Empty Program groups and the direct-Project group are returned for Portfolio visibility. Deleted Projects are excluded; inactive/archived non-deleted Projects remain included. Historical as-of hierarchy is not available; the response identifies its basis as `CurrentHierarchy`.

## Financial calculations

- Approved Annual Budget and its New Funding, Free Carry Forward, Reserved Carry Forward, and Adjustments are reported only for Approved Project budgets. Draft/unapproved budgets do not enter totals.
- `Effective Active Allocations = Direct Approved Allocations + Incoming Approved Transfers - Outgoing Approved Transfers - Approved Reversal Releases` for the selected funding-year budget. A request is counted once in its currently active funding assignment.
- `Available Annual Budget = Approved Annual Budget - Effective Active Allocations`. Settlements do not reduce this value again.
- `Total Unspent Budget = Approved Annual Budget - actual settlements in the selected Fiscal Year`; closed periods use the immutable closing snapshot. This differs from Available Annual Budget because outstanding commitments remain allocated.
- Approved Reversal Releases are unpaid-commitment releases; they are not cash refunds.
- Outstanding payable amounts follow the current funding transfer destination. Actual settlements are grouped by actual payment date within the selected Tenant Fiscal Year.
- Each Payment Request settlement's funding-transfer reference is checked against the transfer whose destination Fiscal Year contains its payment date. Missing or mismatched attribution marks the Project contribution unavailable and the parent report Provisional.
- Payment Request status/count and original approved-amount metrics follow the request's original Fiscal Year association. Current active allocation/outstanding metrics follow current funding Fiscal Year. Cash-paid metrics follow actual payment Fiscal Year. These are separate dimensions and must not be added together.
- Legacy Payment Obligation settlements are excluded from the Payment Request cash metric, preventing the shared settlement table from counting an unrelated source twice.
- All financial totals are grouped by exact currency code; mixed currencies are never summed and no FX conversion is attempted. Currency-group ProjectCount covers validated Project contributions; overall included counts and gap indicators identify Projects whose monetary values are unavailable.
- Closed-year values come from the immutable selected Project/Fiscal Year closing snapshot. Closing coverage is evaluated for that same Fiscal Year only.

Missing/unapproved budgets, unverified legacy request associations, invalid currency, inconsistent relationships/transfers/settlements, or incomplete authorization coverage are exposed as missing/provisional coverage. Invalid Project contribution values are unavailable instead of being guessed. Parent reporting is Reconciled only when every included Project's selected year is Closed, scope is complete, and no known financial gaps or unassociated legacy approved requests remain.

## Permissions and API

Reused permissions; no new permission key or role grant was introduced.

| Capability | Required permissions |
|---|---|
| Program Financial Overview | `Program.View` and `finances.read` (the existing API policy also recognizes `finances.write`) |
| Portfolio Financial Overview | `Portfolio.View` and `finances.read` (the existing API policy also recognizes `finances.write`) |
| Project contribution | Project must be in the caller's management Project scope; service-level tenant/entity checks apply to every caller |
| Project detail drill-down | Existing Project endpoint permissions are independently enforced after following the link |
| Financial writes | Not provided by Program or Portfolio routes; remain Project-scoped |

The existing keys are registered in the authoritative permission seed/catalog and appear in Roles & Permissions using localized resource/action labels and descriptions. Frontend tabs are hidden unless the user has `finances.read` or `finances.write`; a 403 is shown as a localized restricted-access message. API controllers and the rollup service enforce the same finance and parent-view permissions. Financial rollups require authorized parent and Project scope for all callers; the shared tenant-administrator role-name GET exception is not applied to these routes. No permission is auto-granted.

## Implementation and source verification

Backend endpoints:

- `GET /api/program-management/programs/{programId}/financial-rollup?fiscalYear=&page=&pageSize=`
- `GET /api/portfolio-management/portfolios/{portfolioId}/financial-rollup?fiscalYear=&page=&pageSize=`

Reads are batched in groups of up to 800 Projects; financial contribution totals use the full authorized population before drill-down pagination. Currency groups return aggregates only; authorized Project details appear only in the paginated `projects` collection. The UI provides Fiscal Year selection, currency-specific financial metrics, request/settlement status, closing coverage, Program/direct-Project breakdown, pagination, partial/provisional warnings, localized restricted-access feedback, and Project links. No API write operation is exposed.

Static source review confirmed closing coverage filters to the selected Fiscal Year, the Fiscal Year control has no arbitrary history window beyond the API's supported range, empty Program/direct-Project hierarchy groups are represented, payment settlement attribution is checked against payment Fiscal Year, currency groups contain aggregates only, and financial rollups do not inherit a role-name tenant-admin scope bypass. Project detail pagination is deterministic and guarded against integer overflow. Fiscal Year labels and access/error messages are localized in EN/AR. No build, TypeScript check, unit test, database query, performance test, browser run, permission runtime test, or UAT was executed under the Sprint 6 validation policy. Source compilation is therefore **NOT VERIFIED**.

## Files changed for Sprint 6

- `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioFinancialRollupDtos.cs`
- `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioFinancialRollupService.cs`
- `apps/api/src/NEXUS.Api/ProgramManagement/ProgramManagementController.cs`
- `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementController.cs`
- `apps/api/src/NEXUS.Api/ProgramManagement/ProgramManagementService.cs`
- `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementService.cs`
- `apps/api/src/NEXUS.Api/Management/ManagementContextMiddleware.cs`
- `apps/api/src/NEXUS.Api/Program.cs`
- `apps/web-react/src/api/endpoints.ts`
- `apps/web-react/src/features/delivery/api/delivery.ts`
- `apps/web-react/src/features/delivery/FinancialRollupView.tsx`
- `apps/web-react/src/features/delivery/PortfolioProgramPages.tsx`
- `apps/web-react/src/i18n/messages/en.json`
- `apps/web-react/src/i18n/messages/ar.json`
- `docs/modules/FINANCIAL-PROGRAM-PORTFOLIO-REPORTING.md`
- `docs/modules/FINANCIAL-TARGET-BUSINESS-MODEL.md`
- `docs/modules/FINANCIAL-REDESIGN-ROADMAP.md`
- `docs/modules/PROJECT-ANNUAL-BUDGET-FOUNDATION.md`
- `docs/modules/FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md`
- `docs/modules/FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md`
- `docs/modules/PORTFOLIO-MANAGEMENT-CORE.md`
- `docs/architecture/program-manager-perspective.md`
- `docs/development/financial-consolidated-technical-closure-backlog.md`
- `docs/quality/MASTER-UAT-CHECKLIST.md`
- This Sprint 6 report.

The repository already contains many concurrent changes; this list identifies the Sprint 6 touchpoints, not every dirty file in the working tree.

## Remaining work and handoff

- Sprint 7A: P0 repeatable full-dependency API build reliability, P0 IKVM/MPP adapter isolation without removing Microsoft Project import, P0 consolidated Sprint 3-6 EF/model/migration/snapshot readiness, P1 genuine financial nullability/compiler fixes, and query-plan/index review. `BuildProjectReferences=false` alone is not build evidence. Sprint 6 itself adds no EF schema requirement.
- Sprint 7B: after the required target, schema, and authorization checks, execute the added aggregation, authorization, mixed-currency, hierarchy, performance, localization, and source-to-Project reconciliation UAT cases. Backups are optional for Development/UAT. Runtime UAT is not part of Sprint 6.
- Separate P2 follow-up: Roles & Permissions cleanup (duplicate entries, categories, EN/AR labels); do not change permission keys or grants as part of Sprint 6.
- Historical hierarchy-as-of reporting and currency conversion remain out of scope. Current hierarchy can change the parent under which a Project appears in current reports; immutable closing snapshots preserve Project financial facts but not a historical parent assignment.

## Acceptance status

| Area | Status | Evidence |
|---|---|---|
| Project-only ownership / read-only parent views | PASS — source present | No parent financial write endpoints or persisted aggregate records added. |
| Program and Portfolio aggregation / deduplication | PASS — source reviewed | Program scope and direct-plus-Program Portfolio scope; distinct Project IDs. Runtime totals not executed. |
| Fiscal Year, carry-forward, reversals, settlements | PASS — source reviewed | Selected Tenant FY; current funding assignment for commitment; actual payment date for cash; no second settlement deduction. |
| Currency and incomplete data handling | PASS — source reviewed | Currency groups and unavailable/provisional states; no FX. |
| Permissions / scope | PASS - source present | Existing keys reused; controller/service checks; authorized parent and Project scope required for every caller. Runtime authorization deferred. |
| EN/AR, RTL, theme | PARTIAL - source present | EN/AR financial-year and access messages are translated; layout uses shared theme/responsive tokens. Visual browser rendering is not verified. |
| API/frontend compile | NOT VERIFIED | Builds were deferred and not run. |
| EF model/migration | UNCHANGED | No Sprint 6 schema changes, migration, or snapshot update. Sprints 3-5 remain in the consolidated backlog. |
| Database / runtime / UAT | DEFERRED | No database connection or mutation and no runtime scenario execution. |

## Post-handoff verification - 2026-10-31

- Parsed both React message catalogs as JSON successfully. The Program and Portfolio `financialRollup` sections each contain 63 matching EN/AR keys; all Arabic values contain Arabic text and no replacement character. All 36 statically referenced component label keys exist in both locales. Finance and parent-view permission descriptions and resource/action labels are present in both locales.
- Confirmed the existing `finances.read`, `finances.write`, `Program.View`, and `Portfolio.View` permissions are seeded. The rollup routes require the parent-view and finance-read policies; the service rechecks effective permissions and tenant/parent/Project scope. The management middleware's tenant-administrator GET allowlist does not include the `financial-rollup` route shape.
- Confirmed the rollup service performs no writes and explicitly filters its query-filter-bypassing reads by Tenant and authorized parent/Project IDs. The Master UAT Checklist contains deferred positive/negative permission, cross-scope, tenant-admin bypass, mixed-currency, hierarchy, and reconciliation scenarios (`RUP-01` through `RUP-39`).
- `git diff --check` passed for the tracked Sprint 6 source and catalog changes. Build, TypeScript check, tests, runtime authorization, browser/RTL rendering, database/model reconciliation, and UAT remain unrun and deferred to the documented follow-up gates.
