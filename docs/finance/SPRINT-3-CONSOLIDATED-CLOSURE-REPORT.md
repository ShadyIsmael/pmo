# FOPX PM — Sprint 3 Consolidated Closure Report

**As of:** 2026-11-23  
**Repository:** `C:\Projects\pmo`  
**Execution:** Targeted resume; previous work was inspected before changes.  
**Recommendation:** **BLOCKED** — Sprint 3 is not ready for closure or release.

## 1. Executive Summary

The prior Task 05 closure report was not present. This report was created after reviewing the available Sprint 3 Task 03/04 reports, the approved Financial Operating Model, current source, tests, and the working tree. Existing uncommitted changes were preserved.

The resumed work verified the D1 Budget Line calculation, completed D2 currency-quality handling, brought project/status-report planned-payment figures onto the installment and settlement model, and corrected Program/Portfolio reservation rollups for Pending and Approved requests. Existing permission definitions and frontend mocks were reviewed and corrected where needed. Cost Management and Project Management builds passed; the API build passed to an isolated temporary output after the normal output directory proved locked. Frontend typecheck, production build, and 41 focused tests passed.

Sprint closure remains **BLOCKED**. No crosswalk proves whether legacy `PaymentObligation` rows overlap new Planned Payments, so reports cannot establish that the separate rails are free of duplicate historical commitments. The Product Owner approved the refund/correction lifecycle on 2026-11-23; Task 05B adds assigned approval and execution confirmation, pending runtime and persistence verification. The latest backend test assembly compiled, but VSTest again failed to connect to testhost after 90 seconds; no backend tests executed. Offline EF model synchronization passed, but there is no current Development identity, physical schema, or SQL Server persistence evidence in this execution.

## 2. Resume Execution Summary

| Work item | Status | Evidence / note |
|---|---|---|
| Read repository instructions, approved model, prior finance reports, source changes, and Git state | Done | Existing report path was absent. Working tree had extensive concurrent changes; none were reverted. |
| Verify previous D1/D2 and permission work | Done — source reviewed | Prior implementation was present; gaps are detailed below. |
| A - Confirm D1 Budget Line reservation integrity | Done - source/build verified; runtime test blocked | Fail-closed amount/currency/release checks are present. No backend assertions executed. |
| B - Resolve D2 status-report currency integrity | Done - source/build verified; runtime test blocked | New snapshot quality flags and settlement-based Actual Paid mapping are in place; historical snapshots remain unchanged. |
| C - Resolve financial permission catalog gaps | Done - catalog/source review | No missing canonical definitions found; labels exist. Persisted role grants were not inspected. |
| D - Restore frontend build reliability | Done | Typecheck and production build pass; temporary-file access denial did not reproduce, so no workaround was needed. |
| E - Repair frontend test mocks and run focused financial tests | Done for available focused suite | 6 files / 41 tests pass. The reviewed frontend test filename inventory had no separate settlement, refund, or replacement test file. |
| F - Execute backend financial regression | Blocked | Latest test assembly compiles, but VSTest cannot connect to testhost after 90 seconds. |
| G - Verify Development DB and persistence | Blocked | No current secure Development connection is available; no live DB query or financial test record was made. |
| H - Consolidated financial integrity review | Done - source review, with blockers | Reporting source gaps fixed; legacy/current-rail overlap remains unresolved, and the approved adjustment workflow still needs runtime and persistence verification. |
| I - Consolidate builds and verification | Done | Current commands, results, and limits are in Section 11. |
| J - Final closure report | Done | This report is updated with the resumed execution evidence and BLOCKED recommendation. |
| Overall acceptance gate | Blocked | Legacy/current-rail overlap, backend runtime tests, and current DB/persistence verification remain open; persisted role grants are unknown. |
| Start Sprint 4 | Not started | No Sprint 4 work was initiated. |

## 3. Completed vs. Remaining Work

### Completed or verified during this resume

- D1 now fails closed when active requests or approved reversals have invalid or incomparable currencies, malformed amounts, over-release, or an unsafe multi-line allocation. Duplicate rows for the same request are collapsed before reservation totals are calculated.
- D2 Actual Paid currency exclusions are now captured as explicit status metadata in new status-report performance snapshots and exposed through the report DTO. Existing snapshots are left untouched; reports without prior metadata map to `NotRecorded` when the amount is null.
- The status-report financial summary now reads `ActualPaidSnapshot` and `OutstandingPlannedPaymentsSnapshot`. It no longer labels the legacy actual-cost and committed-cost fields as cash paid and planned-payment outstanding.
- Project financial summaries and status reports now calculate Planned Payment outstanding/due/remaining amounts from active installments and confirmed settlements through the report cutoff. Linked Payment Requests are not added a second time. Unresolved or inconsistent source data is represented as data-quality metadata rather than a fabricated zero.
- Program/Portfolio annual availability now counts Pending and Approved request allocations once, by Annual Budget, for active request states; terminal and draft states contribute no reservation. A focused annual-allocation calculator regression source was added.
- Existing project-scoped financial permission definitions now declare `PermissionScope.Project` explicitly. The protected Tenant Admin annual-budget bootstrap exception was removed; existing persisted grants were not changed.
- Finance workspace test mocks now include mutation `mutateAsync`, query error state, and `refetch` members matching the production hook contracts.
- Frontend production build and typecheck pass. The current build reproduced no Windows esbuild temporary-file denial, so no TMP/TEMP workaround was added.

### Remaining

- Legacy `PaymentObligation` and current Planned Payment records have no deterministic crosswalk. No fuzzy de-duplication was added, so historical overlap cannot be ruled out from current source evidence.
- Settlement refunds/corrections no longer affect Net Actual Paid when merely requested or approved. Task 05B adds Current PM submission, an eligible assigned approver with maker-checker enforcement, explicit Current PM execution confirmation, cumulative refund limits, immutable original settlements, and audited transitions. Effective execution date drives net reporting. This path is built but awaits backend runtime and persistence verification.
- Backend financial regression did not execute because VSTest could not connect to testhost; SQL Server persistence and current database schema evidence are also unavailable.

## 4. D1 Budget-Line Fix Verification

**Previous Agent work:** The reservation/release fix was already present in `ProjectAnnualBudgetLineWorkflow.cs` and `ProjectPaymentRequestLineFunding.cs`. The write path uses active Pending/Approved requests and approved reversals, validates funding currency and release limits, and runs the request workflow with serializable/project-budget locking.

**Resume changes:** The line page no longer reports zero reservation when an active request has a different currency from its Annual Budget. It returns a conflict instead of publishing a misleading free balance. Approved reversals must use the request currency; an over-release or nonpositive active request amount also fails closed. Duplicate rows are collapsed per request, and a request assigned to multiple Budget Lines without a safe split is rejected for display calculation.

**Verification:** Current API build passed. No focused D1 regression test was found, and backend tests could not execute because testhost did not connect. Therefore D1 is **source-reviewed and build-verified, but runtime-regression-unverified**. Annual Budget availability, Budget Line limits, and concurrent/idempotent behavior still need executable backend evidence.

## 5. D2 Currency Gap Resolution

**Previous Agent work:** `ProjectStatusReportService.CaptureFinancialSnapshotAsync` already used `ActualPaidSettlementCalculator` across both settlement rails through the report end date, normalized currency codes, and set Actual Paid to null when a settlement currency was unresolved or differed from project currency.

**Resume changes:**

- New snapshots persist `Available`, `UnresolvedCurrency`, `CurrencyMismatch`, or `ProjectCurrencyUnavailable` in the existing performance snapshot JSON. This additive metadata does not require a schema migration and does not rewrite historical reports.
- The DTO exposes `actualPaidDataQualitySnapshot`; the UI displays a localized data-quality message when Actual Paid is unavailable. Legacy null snapshots display “not recorded,” not zero or confirmed EGP.
- The UI reads the dedicated settlement-based Actual Paid and planned-payment snapshot fields, instead of displaying `ActualCostAmountSnapshot` and `CommittedCostAmountSnapshot` under cash-settlement labels.
- Added focused backend classifier/mapping tests. They compiled with the focused test assembly but did not execute due to testhost failure.

**Frontend evidence:** `project-status-reports-parity.test.tsx` passed within the 41-test focused frontend run, including a currency-mismatch case that verifies no cost figure is substituted for Actual Paid.

**D2 resolution:** New status reports now calculate outstanding, due-within-period, and remaining-unpaid values from active Planned Payment installments and settlement facts through `PeriodTo`. Linked requests are not counted in addition to the originating plan. The report preserves historical snapshots and marks unavailable, mismatched, or unresolved financial data explicitly. A focused test source covers the report cutoff and settlement mapping; it compiled but could not execute because testhost did not connect. The separate unresolved reporting risk is legacy/current-rail overlap described in Sections 12-14.

## 6. Permission Catalog Findings

- No missing canonical financial permission definitions were found. Existing definitions already have English and Arabic labels; no aliases or new keys were introduced.
- Explicit Project scope was added to these existing project-owned keys: `ProjectFinance.View`, `ProjectPaymentRequest.View/Create/EditDraft/Submit/Cancel/Approve`, `PaymentSettlement.Record`, `ProjectSettlement.View`, `ProjectPaymentReversal.Create/Approve`, `ProjectFinancialAuditTrail.View`, and `ProjectFinancialCeiling.ApproveChange`. `ProjectFinancialFoundation.View` was already Project-scoped.
- Removed the special allowlist that permitted newly adding selected Annual Budget bootstrap permissions to the protected Tenant Admin role. The custom-role bootstrap delegation list remains separate and retains its Active Deny check. No additional financial authority was automatically granted.
- No database role grants were changed or verified. Permission definitions and persisted grants are reported separately; current persisted grants remain **unknown** because no live Development database query was made.
- The existing permission matrix contains stale proposed retired `.Manage` aliases. These are documentation drift, not missing canonical runtime definitions, and were not converted into permission keys or grants.
- Authorization tests compiled in the focused test assembly but did not execute because of the testhost failure.

## 7. Financial Integrity Verification

Source review confirms the existing workflow preserves the intended separation between lifetime reservation, annual funding, approval, and settlement:

1. Planned Payment registration is intended to reserve the lifetime amount once.
2. Payment Request funding uses Annual Budget and Budget Line allocations.
3. Approval does not itself count as Actual Paid.
4. Confirmed settlements are the Actual Paid source across both payment rails.
5. D1 reservation/reversal display now fails closed on currency and over-release inconsistencies.
6. Program/Portfolio reporting reserves Pending and Approved request allocations once by Annual Budget.
7. Project and status-report outstanding metrics use active Planned Payment installments and settlement facts through the report cutoff; the linked Payment Request is not an extra payable.
8. Task 04 source and reports describe atomic Quick One-off creation of a Planned Payment, installment, linked Payment Request, and lifetime reservation.

Runtime evidence is incomplete: backend workflow, concurrency, idempotency, rollback, audit atomicity, and SQL Server persistence tests did not execute. No deterministic crosswalk proves that old obligations and new plans do not overlap; the Product Owner-approved adjustment workflow still lacks runtime and SQL persistence verification. These gaps prevent closure.

## 8. Database & Persistence Evidence

| Required field | Result |
|---|---|
| EF model synchronized | **Yes - offline model check only.** The final `ef-database-gate.ps1 model` reported `NEXUS.Persistence.AppDbContext`, migration assembly `NEXUS.Persistence`, 39 source migrations, and `EF Model synchronized: Yes`. |
| Repository migrations | 39 migrations are present in source, including `20261123090738_Sprint3Task05BRefundExecutionGovernance`. |
| Pending migration count and names on target database | **Not verified.** The gate did not open a database connection, so target pending count/names are unknown. |
| Development target environment and identity | **Not verified in this resume.** No secure Development connection or verified current API Development target was available, so the read-only inspection gate was not run. Historical identity information was not reused as current proof. |
| Backup status | **Not requested.** No database operation was performed. |
| Migrations applied | **No.** The new additive migration was generated/reviewed but not applied. |
| Required seed mode and idempotency | **Not run / not verified.** No seed is required for this schema-only migration; seed idempotency was not tested. |
| Physical schema, foreign keys, and decimal precision | **Not verified against SQL Server.** |
| SQL Server financial persistence tests | **Not run.** No isolated SQL Server database or rollback-safe target was verified. |
| Shared Development financial test records | None created. |
| Production or destructive operations | None performed. |

The database task is not complete. The offline model result does not prove current Development database identity or physical-schema compatibility. Repository documentation contains an older host-connected recheck dated 2026-10-31 (then `SHADY/NexusPmo`, database ID 9, six applied of seven repository migrations, with baseline and current-model schema differences). That historical state predates the current repository's 39 migration files and is not current target evidence; its migration counts and schema findings must not be carried forward as today's state.

## 9. Authorization & Security

- Existing Project-owned financial permission definitions now carry explicit Project scope; Program and Portfolio rollup permissions remain at their corresponding scopes.
- The code continues to evaluate canonical keys and Active Deny. Organization-owned role behavior and existing grants were not rewritten.
- No Finance role, System Role, or new persisted grant was added.
- The protected Tenant Admin exception for adding selected financial bootstrap permissions was removed. This change affects assignment policy only; it does not remove existing grants.
- Runtime authorization, cross-project scope, reviewer assignment, self-approval, and persisted-grant state remain unverified because backend tests and live database reads were unavailable.

## 10. Backend Regression Results

**Focused test run attempted:**

```text
dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore -p:MSBuildEnableWorkloadResolver=false -p:FocusedPlannedPayments=true --logger "console;verbosity=minimal" -m:1
```

The Planned Payments test assembly **compiled successfully**. VSTest then reported: "failed to connect to testhost process after 90 seconds." The run was aborted before test results; passed/failed counts are **not available**. This reproduces the current testhost blocker despite earlier reports that it had been recovered.

After the latest reporting changes, the `FocusedActualPaidReporting=true` test source set (including reporting/persistence regression sources) compiled to temporary output with **0 warnings and 0 errors**. Running that assembly again matched one test file, then VSTest failed to connect to testhost after 90 seconds and aborted; **0 tests executed**. VSTest reported the testhost process was still running. This is an infrastructure/runtime launch failure, not a failing assertion.

Other focused test source sets were built separately without running tests:

| Focused build property | Build result |
|---|---|
| `FocusedActualPaidReporting=true` | Passed; 0 warnings, 0 errors. Includes the new status-report currency-quality test source. |
| `FocusedPaymentRequestPolicy=true` | Passed; 0 warnings, 0 errors. |
| `FocusedUserPermissions=true` | Passed; 3 nullable warnings, 0 errors. Includes the financial permission delegation/scope test source. |

The combined-property command was not used as evidence that these suites ran together: the test project uses conditional `Compile Remove` groups, and MSBuild evaluation left only the final Planned Payments source set in that combined evaluation. Each additional source set above was compiled individually.

| Requested regression area | Current executable evidence |
|---|---|
| Planned Payment immediate reservation / installment reconciliation | Test source compiled; no tests executed. |
| Concurrency and idempotency | No current executed result. |
| Payment Request annual funding / Budget Line limits / approval order / self-approval / return and resubmission | Policy test source compiled; no tests executed. |
| Full and partial settlement / over-settlement / duplicate Actual Paid / current PM authorization | No executed backend result. |
| Replacement and preservation of paid history | No executed backend result. |
| Refund/correction gross and net calculations | Existing tests do not establish approval gating; no tests executed. |
| Quick One-off atomic commit/rollback/idempotency/orphan prevention | Prior Task 04 implementation report exists; current backend tests did not execute. |
| SQL Server persistence and transaction rollback/audit atomicity | Not run. |

No unexecuted test is marked as passed.

## 11. Final Builds & Verification Summary

| Check | Command / method | Result | Counts / limitation |
|---|---|---|---|
| Cost Management Build | `dotnet build apps/api/src/NEXUS.Modules.CostManagement/NEXUS.Modules.CostManagement.csproj --no-restore -v:minimal -p:MSBuildEnableWorkloadResolver=false -m:1` | **Passed** | 0 warnings, 0 errors. |
| API Build | `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore --no-dependencies -v:quiet -p:MSBuildEnableWorkloadResolver=false -p:OutputPath=C:\Users\shady\AppData\Local\Temp\fopx-task05b-api-build-final\ -m:1` | **Passed in isolated output** | Final build after Task 05B fixes: 352 warnings, 0 errors; warnings are primarily IKVM. The standard project output build failed after 10 copy retries because a process held DLLs in the API `bin` folder; no process was stopped. |
| Project Management Build | `dotnet build apps/api/src/NEXUS.Modules.ProjectManagement/NEXUS.Modules.ProjectManagement.csproj --no-restore -v:quiet -p:MSBuildEnableWorkloadResolver=false -m:1` | **Passed** | 0 warnings, 0 errors. |
| Frontend Typecheck | `npm run typecheck` in `apps/web-react` | **Passed** | `tsc -b`, exit 0. |
| Frontend Production Build | `npm run build` in `apps/web-react` | **Passed** | Vite transformed 3006 modules, built in 2m20s. Rollup annotation and large-chunk warnings remain. No esbuild temporary-file access denial reproduced. |
| Focused Frontend Tests | `npm run test -- src/test/project-finance-workspace.test.tsx src/test/project-finance-overview-permissions.test.tsx src/test/planned-payment-section.test.tsx src/test/planned-payments-project-id.test.tsx src/test/quick-one-off-payment-request.test.tsx src/test/project-status-reports-parity.test.tsx` | **Passed** | 6 files, 41/41 tests; 90.41s. |
| Backend Reporting Test Assembly | `dotnet build apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --no-dependencies -p:BuildProjectReferences=false -p:MSBuildEnableWorkloadResolver=false -p:FocusedActualPaidReporting=true -p:OutputPath=C:\Users\shady\AppData\Local\Temp\fopx-task05-financial-tests\ -m:1` | **Passed** | 0 warnings, 0 errors. |
| Backend Focused Tests | `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --no-build -p:BuildProjectReferences=false -p:MSBuildEnableWorkloadResolver=false -p:FocusedActualPaidReporting=true -p:OutputPath=C:\Users\shady\AppData\Local\Temp\fopx-task05-financial-tests\ --logger "console;verbosity=minimal" -m:1` | **Blocked / aborted** | VSTest failed to connect after 90s; 0 tests executed. |
| Authorization Tests | `dotnet build apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --no-dependencies -v:quiet -p:BuildProjectReferences=false -p:MSBuildEnableWorkloadResolver=false -p:FocusedUserPermissions=true -p:OutputPath=C:\Users\shady\AppData\Local\Temp\fopx-task05-auth-tests\ -m:1` | **Compiled / not executed** | 3 nullable warnings, 0 errors; no runtime authorization results. |
| Financial Reporting Tests | `FocusedActualPaidReporting=true` focused build; frontend parity tests passed | **Partially verified** | Backend source compiled with 0 warnings/errors; frontend 41/41; no backend test results. |
| SQL Server Persistence Tests | No isolated, verified SQL Server target available | **Not run** | Physical schema and transactional persistence remain unverified. |
| EF Model Check | `.\scripts\database\ef-database-gate.ps1 model` | **Passed offline** | Model synchronized; connection not opened; target migration state/schema unknown. |
| UPI Index | `.\tools\upi\upi.ps1 refresh --root .` then `validate --root .` | **Passed** | Validation `ok: true`, 0 errors and 0 warnings. |

## 12. Reporting Reconciliation

### Confirmed P0 gaps

- **Legacy/current-rail overlap:** Project and status-report metrics now calculate Planned Payment installment balances and legacy obligations separately. There is no deterministic crosswalk proving that a legacy `PaymentObligation` and a Planned Payment never represent the same commitment. No fuzzy de-duplication or invented identity match was used. Until source data can establish separation or an approved reconciliation rule is defined, duplicate historical exposure cannot be ruled out.
- **Refund/correction lifecycle:** Product Owner decision approved on 2026-11-23. New API/UI require Current PM initiation, an eligible assigned approver with ProjectPaymentReversal.Approve, no self-approval, and Current PM execution confirmation. Only executed adjustments affect Net Actual Paid; the original settlement and Lifetime Reservation remain unchanged. Backend runtime, SQL persistence, and persisted-grant state remain unverified.
- **Execution/persistence evidence:** Backend testhost did not start the compiled focused suite, and no current Development identity, physical schema, or isolated SQL Server financial persistence test was verified.

**Resolved source gaps:** Project/status-report planned payment balances now use active installments and confirmed settlements through the cutoff, and Program/Portfolio availability includes Pending and Approved request allocations once by Annual Budget. The new calculator/reporting regression sources compile; runtime results are unavailable because testhost failed.

### Status-report time basis

Status-report Actual Paid is cumulative through `PeriodTo`; project financial summaries are period-bounded and rollups are fiscal-year-bounded. This may be valid for an as-of snapshot, but the display does not clearly label the cumulative basis. This is recorded as a labeling follow-up; it is not the primary closure blocker.

## 13. Remaining Defects and Deferred Backlog

| Priority | Item | Status / required next action |
|---|---|---|
| P0 | Legacy obligation / Planned Payment overlap cannot be reconciled | Establish deterministic source-data mapping or an approved exclusion rule; prove the same commitment is not counted on both rails. |
| P0 | Refund/correction workflow lacks runtime/persistence proof | Source gating and UI are implemented under the approved decision; execute authorization, gross/net, partial-refund, idempotency, rollback, and SQL persistence scenarios after testhost recovery and verified isolated target access. |
| P0 | Backend testhost cannot connect | Recover VSTest/testhost in the current environment and execute the compiled focused suites. |
| P0 | SQL Server database/schema and transaction proof unavailable | Verify Development identity through the documented gate, then run persistence checks only against an isolated or rollback-safe test target. |
| P1 | Reporting and persistence regression sources have no runtime evidence | Execute after testhost recovery; includes cutoff, settlement mapping, Pending/Approved reservation, rollback, and audit atomicity. |
| P1 | D1 behavior has no executed focused regression | Add/run coverage for reservation, release, currency mismatch, and no-double-reservation behavior after testhost recovery. |
| P2 | Status-report cumulative/as-of time basis is unlabeled | Clarify whether the snapshot label should state “through report end date”; update EN/AR if approved by the existing semantics. |
| P2 | Existing matrix contains retired `.Manage` proposals | Clean documentation drift in a separate targeted matrix update. |

## 14. Release Readiness

**Release readiness: BLOCKED.** The refund/correction policy is approved and source gating is implemented, but builds do not establish legacy/new commitment separation or runtime financial correctness. Backend financial tests did not execute; current Development identity, physical schema, SQL Server persistence, and persisted grants remain unverified. The additive migration was not applied. No shared Development data was changed and Sprint 4 was not started.

## 15. Final Sprint Closure Recommendation

**Recommendation: BLOCKED.** Task 05C migration application is DONE — VERIFIED based on the supplied PowerShell evidence. The P0 blockers carried forward from Task 05B remain open: legacy PaymentObligation/Planned Payment overlap is unreconciled; backend testhost startup is blocked; SQL Server financial persistence evidence is missing; and persisted RolePermission grants are unverified. Do not close or release Sprint 3. The Product Owner retains final acceptance authority. Sprint 4 was not started.

## Task 05B - Final P0 Blocker Remediation (2026-11-23)

This continuation preserves the earlier D1/D2 and 41/41 frontend evidence. These updates supersede earlier statements in this report that the refund/correction policy was undecided or that the adjustment path still wrote directly to Net Actual Paid.

### A. Payment Request cancellation and refund governance

The Product Owner decision is approved. Unapproved Payment Requests can be cancelled by the Current Project Manager using the canonical cancellation permission. The workflow closes pending assignments and approval cycles, transitions request status once, audits submitted cancellations, and releases request-level Annual Budget/Budget Line availability through terminal status. Independent Planned Payments and their Lifetime Reservations are preserved.

Quick One-off requests now carry an ownership marker. If their single Plan/installment has no active sibling request or settlement dependency, cancellation marks the Plan Cancelled and releases its remaining Lifetime Reservation in the same serializable transaction. Existing Plan/installment records remain linked; protected dependencies preserve the Plan. Missing or inconsistent reservation state fails closed. Cancellation is rejected if the request has any settlement.

Approved unpaid requests remain on the existing approved reversal path; no cash refund is created. For approved and paid settlements, the Current PM requests a Refund/Correction, selects a distinct active eligible assigned reviewer with ProjectPaymentReversal.Approve, and the exact assigned reviewer decides through the existing ApprovalRequest/action/assignment infrastructure. The Current PM then confirms actual execution. The original PaymentSettlement is never edited; approval alone does not affect Actual Paid; only Executed adjustments affect Net Actual Paid, by execution date. Refund limits include pending, approved, and executed requests and cannot exceed the original settlement. Lifetime Reservation is not released by an adjustment. Each transition is audited and protected by row-version/idempotency checks. EN/AR UI supports request, decision, and execution confirmation.

**Verification limit:** source review and builds passed, but backend workflow, permission, concurrency, rollback, and SQL persistence tests did not execute. The migration that adds this state has not been applied.

### B. Legacy and new commitment reconciliation

The legacy api/payment-obligations write/read/settlement controller and PaymentObligationService are still active and registered alongside the new Planned Payment endpoints. PaymentSettlement rows have one owner, so a row is counted once by the Actual Paid calculator. However, there is no deterministic identity crosswalk between a legacy obligation and a new Planned Payment representing the same underlying commitment. Project and portfolio rollups retain both source paths; duplicate exposure across distinct records therefore cannot be ruled out without current source data. No fuzzy matching, historical backfill, or compatibility migration was added. This remains a material P0 blocker.

### C. Backend regression and testhost

The focused NEXUS.Api.Tests assembly, including the new NetActualPaid_changes_only_after_adjustment_execution_is_confirmed regression source, compiled with **0 warnings / 0 errors**. The test run matched one test file but timed out after 90 seconds during VSTest/testhost startup; **0 test cases executed**. VSTest opened a loopback listener at 127.0.0.1:55768 but did not complete the testhost handshake. The bounded prior diagnostic recorded SocketException 10013 (access denied) on the loopback callback. This is an environment/testhost transport failure; no assertion result is available. No financial validation or assertions were removed.

The new regression source covers pending/approved/rejected exclusions from Net Actual Paid, execution confirmation, execution-date reporting, and stable Gross Actual Paid. The following remain unexecuted: cancellation and release, approval authorization/self-approval, refund execution and partial caps, reservation consistency, legacy/new reconciliation, Quick One-off atomicity/rollback, concurrency/idempotency, tenant/scope isolation, and SQL Server persistence.

### D. EF model and SQL Server persistence

- **EF model synchronized:** Yes, offline using ef-database-gate model; 39 source migrations, including 20261123090738_Sprint3Task05BRefundExecutionGovernance.
- **Migration contents:** additive CreatedFromQuickOneOff, adjustment status/approval/execution fields, filtered unique indexes, status check constraint, and restrictive ApprovalRequest FK. The generated idempotent SQL new migration block was reviewed; it contains no table/column drops, updates, or deletes.
- **Generated SQL SHA-256:** 47F9EF2538A7614E977A6CCAE8E84CAB73B931CCF25326BBB0606FD4911CC956.
- **Target environment/database identity:** Not verified; no secure Development connection or current API Development target was available. An incidental direct EF migration-list probe failed with TCP access denied and is not treated as identity evidence.
- **Pending migration count/names on target:** Unknown because target identity/history could not be queried. The new source migration is pending application to a verified target.
- **Migrations applied:** None during this task. The generated script is not target-specific and was not executed.
- **Backup:** Not requested.
- **Seed mode/idempotency:** Not run; no seed is required for this schema-only migration. Seed idempotency remains unverified.
- **Physical schema:** Not verified.
- **SQL Server persistence tests:** Not run; no isolated, verified test database or safe rollback-based SQL Server setup was available.
- **Shared Development data / Production / destructive work:** None accessed or changed; no reset, Demo Seed, or Production operation occurred.

### E. Persisted authorization grants

Canonical permission definitions and EN/AR labels were verified by the earlier catalog audit and were not re-audited here. New operations reuse ProjectPaymentRequest.Cancel, PaymentSettlement.Record, and ProjectPaymentReversal.Approve; no Finance role, new permission grant, or persisted role assignment was added. Actual persisted role-permission assignments could not be read without a verified Development connection. Grant state is **unknown**, not proven missing or present. No financial authority was automatically granted.

### F. Regression evidence and final closure status

- D1 Budget-Line and D2 currency source fixes remain as previously verified; this continuation did not change those paths.
- Frontend TypeScript typecheck passed; production build passed (3,006 modules transformed); the affected Finance Workspace test passed **9/9**. Prior unaffected focused frontend evidence remains **41/41** across six files.
- Cost Management build passed with 0 warnings/errors. Persistence rebuild passed with 0 errors (one existing unrelated Project Lifecycle nullable warning on the dependency build). Design-time build passed with 0 warnings/errors. The final isolated API build, after fixing a nullable dictionary inference compile error caught by the verification build, passed with 352 existing IKVM warnings and 0 errors. Focused backend test assembly build passed with 0 warnings/errors; execution count remains 0.
- UPI refresh/validate passed: 1,884 files, 14,738 symbols, 74,266 edges; ok true, 0 errors and 0 warnings.
- The Master UAT Checklist now includes cancellation, Quick One-off dependency handling, approved-unpaid reversal, assigned approval/maker-checker, execution confirmation, idempotency/rollback, cross-scope isolation, and persisted-grant verification cases. No Agent UAT was performed.

**Remaining P0 blockers:** active legacy/new commitment overlap is not reconciled; backend regression tests executed zero cases because loopback testhost startup is blocked; target SQL Server identity/schema/persistence are unverified; persisted role grants are unknown. The approved refund/correction policy is no longer a blocker, but its runtime and persistence behavior is not yet verified.

**Release readiness: BLOCKED.** The P0 safeguards are implemented and build-verified, but required financial runtime, persistence, and authorization evidence is still missing. Product Owner retains final acceptance authority. Sprint 4 was not started.

## Task 05C - Apply Pending Migration & Update Closure Report (2026-11-23)

**Execution status: DONE — VERIFIED.** The migration `20261123090738_Sprint3Task05BRefundExecutionGovernance` was successfully applied locally through PowerShell against the authorized Development database. EF Build succeeded. Migration applied successfully. Required columns, indexes, foreign key, and check constraint were created. Migration recorded in `__EFMigrationsHistory`. EF returned `Done.` and `MIGRATION SUCCESSFUL`. This task updated the closure report in place and preserved all Git changes.

### UPI and change-preservation evidence

- UPI routing was used before application-source inspection. The focused persistence route selected migration `20261123090738_Sprint3Task05BRefundExecutionGovernance.cs`; the UPI index identified `AppDbContext`, the API startup, migration assembly, and EF database-gate implementation. The route freshness pass reported 0 changed and 1,884 unchanged indexed files. The router did not surface the task-specific Markdown, PowerShell gate wrapper, appsettings, and SQL artifact, so only those explicit required paths were opened directly after routing. No unrelated source modules were inspected.
- Git status showed extensive pre-existing concurrent changes. They were preserved. This task changed no application source, migration, SQL artifact, data, or schema; only this closure report was updated.
- The repository workflow documentation is `docs/development/ef-core-database-workflow.md`. The configured API Development endpoint was read without printing credentials: `tcp:localhost,1433` / `NexusPmo`. `localhost` is an endpoint alias, not evidence of the actual SQL Server identity.

### Migration and database execution

| Check | Result |
|---|---|
| Source migration | `20261123090738_Sprint3Task05BRefundExecutionGovernance` is present. |
| Reviewed SQL artifact | SHA-256 `47F9EF2538A7614E977A6CCAE8E84CAB73B931CCF25326BBB0606FD4911CC956`; matches the previously recorded Task 05B review hash. The 05B Up block adds the Quick One-off marker and refund approval/execution fields, indexes, status constraint, and restrictive ApprovalRequest FK; it contains no drop, delete, or data-update operation. The file also includes earlier idempotent migration blocks, so it was not executed as a standalone script. |
| Offline EF model | **Synchronized: Yes.** `NEXUS.Persistence.AppDbContext`, migration assembly `NEXUS.Persistence`; 39 source migrations. Command: `.\scripts\database\ef-database-gate.ps1 model` (exit 0). |
| Environment and connection | The inspection process was set to Development and supplied the connection from the existing API Development configuration without logging it. The gate compared it to the configured API endpoint. |
| Read-only identity/history inspection | Command: `.\scripts\database\ef-database-gate.ps1 inspect --environment Development --expected-server Shady --expected-database NexusPmo` (exit 1). It failed with a SQL Server network error: TCP Provider error 0, “Access is denied.” No `SERVERPROPERTY('ServerName')`, `DB_NAME()`, or database ID was returned. |
| Migration status | **BLOCKED.** The target may already contain the migration or may have it pending; the connection failure prevented checking. Target pending count and migration names are **unknown**. |
| Migration application | None by Task 05C. No SQL was executed and migration history was not manually changed. |
| Refund schema / physical schema | **Not verified.** No target schema query completed. |
| Backup | Not requested. |
| Seed | Not run; no seed is required for this schema-only migration. Seed idempotency was not checked. |
| SQL Server persistence tests | Not run. |

**Database identity: BLOCKED.** Although the task's requested target is Development / Shady / NexusPmo, the actual SQL Server identity was not verified. Per the database gate, no write was attempted after identity verification failed.

### Outstanding P0 blockers carried forward from Task 05B

- Legacy `PaymentObligation` and Planned Payment overlap remains unreconciled; no deterministic crosswalk exists.
- Backend testhost startup remains blocked; the focused backend suite executed zero cases.
- SQL Server financial persistence test evidence remains missing.
- Persisted RolePermission grants remain unverified.
- Task 05C could not verify the Development target, migration history, or Refund Governance physical schema because the read-only connection failed.

### Sprint 3 recommendation

**Recommendation: BLOCKED.** Task 05C migration application is DONE — VERIFIED based on the supplied PowerShell evidence. The P0 blockers carried forward from Task 05B remain open: legacy PaymentObligation/Planned Payment overlap is unreconciled; backend testhost startup is blocked; SQL Server financial persistence evidence is missing; and persisted RolePermission grants are unverified. Do not change Sprint 3 acceptance status based on the offline model check or SQL review. Product Owner retains final Sprint acceptance authority.
