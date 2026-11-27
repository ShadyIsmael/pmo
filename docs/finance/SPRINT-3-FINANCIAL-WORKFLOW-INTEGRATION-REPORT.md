# Sprint 3 / Task 03 — Financial Workflow Integration Report

**Date:** 2026-11-22  
**Repository:** `C:\Projects\pmo`  
**Status:** API build passed; focused backend test project did not compile; database was not accessed.

## 1. Executive Summary

Targeted integration work was completed across settlement source-of-truth handling, replacement previews, refunds/corrections, Actual Paid reporting, and the Project Financial Tab. Existing financial workflows and permission keys were reused. Replacement now derives paid amounts from settlement records, previews budget impact on the server, and keeps Pending reversals as blockers until approval.

The frontend typecheck, production build, focused planned-payment UI test, and API build passed. The focused backend test build failed on compile errors in existing test sources outside this task, so no backend tests executed. No live database inspection or write occurred. The Development baseline stated below is Product Owner-provided, not a live recheck.

## 2. Implemented vs Deferred Matrix

| Area | Status | Evidence / limit |
|---|---|---|
| Planned Payment registration and lifetime reservation | Existing workflow retained; integration reviewed | No reservation redesign. |
| Payment Request, Annual Budget funding, sequential/parallel approval | Existing workflow retained; Financial Tab wiring completed | Runtime approval authorization was not exercised. |
| Settlement, installment reconciliation, Gross Actual Paid | Implemented in source | Settlement records are the source of truth; direct installment link and legacy detail fallback are reconciled. |
| Remaining Commitment Replacement | Implemented in source | Server preview reports paid, outstanding, variable replacement amount, net lifetime delta, budget availability, and request/reversal blockers. |
| Refund and Correction | Implemented in source | Scoped history/actions, validation, transactional audit, and reporting fields are wired. Refunds do not release lifetime reservation. |
| Project / Program / Portfolio Gross and Net Actual Paid | Implemented in source | Existing `ActualPaid` remains gross; net adds effective-date adjustments in matching project currency. |
| Financial Tab navigation | Implemented in source | Planned Payments, Requests, Settlements, adjustments, and replacement remain under Project Details → Financial Tab. |
| Quick One-off Payment Requests | Deferred by scope | Requires shared transaction refactoring; not implemented. |
| Historical Data Migration / backfill / cutover | Deferred by Product Owner | No historical rewrite or migration performed. |
| Live DB checks and browser UAT | Deferred / not run | No Agent UAT; no database connection or mutation. |

## 3. Verified Workflow Integrations

- Settlement recording requires an Approved Payment Request, current Project Manager eligibility, active identity, canonical permission, writable management context, and Tenant/Project scope. Partial settlements reconcile to approved and outstanding balances inside the existing serializable transaction.
- A missing linked installment now fails closed. The persisted installment `ActualPaid` mirror is refreshed from confirmed settlement rows plus the new settlement; reporting and replacement use settlement records as their financial source.
- Actual Paid loads both Payment Obligation and Payment Request settlement ownership paths. Legacy request settlements without a direct installment FK resolve through the original request detail; a direct FK takes precedence so a row is counted once.
- Replacement preview and commit share server calculations. Preview validates installment totals, derives historical paid, calculates available lifetime budget before/after and net reservation change, and returns unresolved requests/reversals.
- Pending reversal requests remain blockers. Only an approved eligible reversal amount reduces active request exposure; creating or submitting a reversal does not release funds.
- Refund/Correction history uses a scoped `ProjectSettlement.View` endpoint. Mutations reuse `PaymentSettlement.Record`, validate actor, scope, current Project Manager, amount precision, effective date, reason, currency, refund cap, and idempotency payload, then save the audit row in the same transaction.
- Project, Program, and Portfolio reporting expose Gross and Net Actual Paid; adjustment effective dates are period-filtered and currency-scoped. Refunds do not release Lifetime Budget.

## 4. Financial Calculation Invariants

- **Gross Actual Paid** = sum of confirmed, in-scope settlement facts in the requested period and project currency.
- **Net Actual Paid** = gross paid − refunds + corrections whose effective dates fall in the reporting period and whose currencies match the project.
- Approval alone does not increase Actual Paid; only a committed settlement does.
- Settlement creates no additional Lifetime or Annual Budget reservation/deduction.
- Installment settled amount derives from confirmed settlement facts. Historical request detail provides the link only when the direct installment FK is absent.
- Replacement net Lifetime Budget change = new outstanding commitment − current unpaid commitment. For 500,000 original, 200,000 paid, and 400,000 replacement outstanding: exposure is 600,000 and net additional lifetime consumption is 100,000.
- Historical settlement rows and paid amounts remain unchanged and are not rebooked during replacement.
- Refunds affect Net Actual Paid only; they do not release lifetime reservation.

## 5. Backend Changes

- Added settlement-to-installment reconciliation in `ActualPaidSettlementCalculator`, including legacy fallback and adjustment effective-period filtering.
- Settlement recording now fails if the linked installment is missing and refreshes the persisted mirror from confirmed settlement facts.
- Added scoped Settlement Adjustment history read, idempotency comparison across the full command, and transactional Refund/Correction audit entries.
- Added a Planned Payment replacement preview endpoint and shared preview/commit calculation. Replacement idempotency checks the command fingerprint and preserves exact replay semantics.
- Added Gross/Net Actual Paid fields to Project financial foundation, Project reporting/cost summaries, and Program/Portfolio rollups.
- No entity/schema change or migration was introduced by Task 03.

## 6. Frontend Changes

- Payment Requests now exposes Requests and Settlements navigation within the Project Financial Tab, including settlement history and partial payment actions.
- Current Project Manager status gates settlement/refund/correction controls in the UI; backend checks remain authoritative.
- Settlement History shows original settlement references and adjustment history, with Refund/Correction forms for amount, reason, and effective date.
- Gross and Net Actual Paid are displayed from the financial foundation API.
- The replacement Side Sheet uses server preview values and remains disabled while the preview is stale, budget is insufficient, or blockers remain.
- English/Arabic labels were added with the existing RTL behavior. The top-level `/costs/payments` link was removed from the Project Financial Tab.
- Planned-payment UI test mocks were updated for replacement and preview hooks.

## 7. Authorization and Audit Coverage

- Reused canonical permissions: `PaymentSettlement.Record`, `ProjectSettlement.View`, and the existing Planned Payment edit capability. No new Finance Role, permission key, automatic role grant, or approval engine was added.
- Backend workflows enforce active identity, Tenant/Project scope, writable context, current Project Manager eligibility for settlement/refund/correction/replacement, and existing assigned-approver rules.
- Refund/Correction writes append immutable adjustment history and an audit row in one transaction. Replacement preserves original/replacement linkage and plan history. Existing settlement audit remains transactional.
- Source review found existing assigned-approver and self-approval checks. Runtime positive/negative, Active Deny, and cross-scope tests remain outstanding.
- Updated `docs/security/PERMISSIONS-FINAL-TARGET-MATRIX.csv` with current settlement/adjustment UI and backend evidence.
- Updated `docs/quality/MASTER-UAT-CHECKLIST.md` with settlement, replacement, reversal-blocker, refund/correction, reporting, authorization, and EN/AR cases. These are deferred to consolidated UAT.

## 8. Database / Migration Evidence

| Required evidence | Result |
|---|---|
| Model synchronized | No persisted model/schema change was made in Task 03. Live EF model synchronization was not rechecked. |
| Pending migration count and names | Product Owner-provided baseline: zero pending migrations. Not live-verified. |
| Target environment and database identity verified | Not run; no Development connection was opened. |
| Backup status | Not requested. |
| Migrations applied | None in Task 03. |
| Required seed mode and idempotency | Product Owner-provided baseline: Minimal System Seed succeeded and is idempotent; 8 roles, 190 permissions, 0 projects. Not live-reverified; no seed ran. |
| Physical schema verified | Not run. |
| Persistence tests | Not run against SQL Server. |
| Blockers | Backend build/test verification is still pending. |

## 9. Build and Test Results

- Frontend typecheck: **Passed** (`npm run typecheck`).
- Frontend production build: **Passed** (`npm run build`); Vite emitted dependency annotation and large-chunk warnings, then completed successfully.
- Focused planned-payment UI tests: **Passed, 7/7** (`npm run test -- src/test/planned-payment-section.test.tsx`).
- API build: **Passed** using `MSBuildEnableWorkloadResolver=false` and `BuildProjectReferences=false`; 352 IKVM warnings, 0 errors, 7:38 elapsed. The standard workload resolver remains unavailable in this environment.
- Backend test build: **Failed before test execution**. The test project has compile errors in existing files, including missing `PaymentRequestStatuses`, `ITenantFiscalYearService`, and `FiscalYearCalendarService` references in Annual Budget tests; a missing `AddCanonicalFinancialActionPolicies` extension in `CostUtilizationExportAuthorizationTests`; a removed `ResourceManagement.Domain.ProjectMember` reference in `ManagementScopeResolverTests`; a stale `ProjectService` named argument in `ProjectStatusChangeCatalogTests`; and other Annual Budget publisher/signature mismatches. No backend test cases ran. The updated Planned Payment test file produced nullable warnings only in the captured output.
- Planned Payment replacement test calls were updated to the current DTO contract, and a variable-amount preview test was added; execution is blocked by the test-project compilation errors above.
- Database/runtime authorization/reporting tests: **Not run**. No Agent UAT was performed.

## 10. Remaining Defects and Blockers

1. Focused backend tests remain blocked because the API test project fails compilation in unrelated existing tests. Repair those test-project integration errors in an authorized follow-up, then run the Actual Paid, Planned Payment/replacement, and approval-policy tests.
2. Development database identity, migration history, Minimal Seed, and physical schema were not live-verified. Use the stated Product Owner baseline; do not reset the database.
3. SQL Server tests remain outstanding for settlement/replacement concurrency, adjustment atomicity, Active Deny, and Tenant/Project isolation.
4. Browser validation for EN/AR and RTL Financial Tab workflows remains deferred to consolidated UAT.
5. Quick One-off Payment Requests remain separately deferred.

## 11. Recommended Sprint 3 / Task 04

After API build and focused backend tests pass, run an isolated non-Agent UAT cycle for settlement, replacement, refund/correction, and reporting cases in the Master UAT Checklist. Use a verified Development target for read-only identity/schema checks and isolated data for financial mutations. Capture evidence for sequential/parallel approvals, self-approval rejection, Active Deny, scope boundaries, concurrency, and Project/Program/Portfolio gross/net consistency. Do not start Quick One-off Payment Requests until separately scheduled.
