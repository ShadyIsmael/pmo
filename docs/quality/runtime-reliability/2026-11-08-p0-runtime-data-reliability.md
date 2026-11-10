# P0 Runtime and Data Reliability Recovery - 2026-11-08

**Backup-policy supersession (2026-11-10):** This report's backup evidence and status describe the 2026-11-08 execution only. Development/UAT backups are optional; missing backup media, independent storage, or `RESTORE VERIFYONLY` does not block an authorized operation. The schema drift and failed current-runner SQL connection documented here remain separate blockers. Production backup and recovery safeguards remain mandatory.

## Status

**Overall: NOT COMPLETE.** The source changes and focused frontend checks from the prior pass are present. A read-only Windows-host EF inspection verified Development `Shady/NexusPmo` (database ID 5), 15 applied migrations, and one pending migration, `20261108170000_RepairProgramRowVersion`. The inspection exited 2 because it found 16 physical-schema differences from the last applied model. The migration is **not applied**; no independent backup was verified. Product Owner Browser UAT was not run, as requested.

No database reset, seed, migration application, manual SQL patch, or other database write was performed.

**Resume check (2026-11-08):** The repository EF `model` gate passed again: `NEXUS.Persistence.AppDbContext`, `NEXUS.Persistence` migration assembly, 16 source migrations, and no pending model changes. Restore/build emitted 10 `NU1900` warnings because NuGet vulnerability data was unreachable. A fresh read-only `inspect` attempt could not open SQL Server; the TCP provider returned `Access is denied` before the gate could re-verify the live target or schema. No backup or database write was attempted in this resume check. The earlier host inspection remains the latest successful live identity/history/schema evidence, but the target was not reachable from this execution environment.

The task's isolated multi-agent constraint could not be met in this resume: the managed Git worktree tool returned a Codex App request failure. No subagents were started against the shared working tree.

## Issue 01 - Project Plan inline Task dates

- **Original symptom:** Inline Start or End date edits did not survive plan save/reload.
- **Root cause:** Inline edits retained Automatic scheduling. The backend's automatic scheduling path could recalculate a directly entered date before persistence.
- **Fix:** Inline Start and End changes set Manual Schedule, use the existing save mutation and query refresh, and retain the other scheduling values unless date validation requires rejecting the edit. The backend validates Manual date ranges and preserves the existing permission, version, dependency, and project-scope checks.
- **Files:** `PlanningGrid.tsx`, `PlanActivityService.cs`, `PlanActivityServiceTests.cs`, `planning-grid.test.tsx`.
- **Automated evidence:** `planning-grid.test.tsx` passed 53/53 on the Windows host. The focused API `PlanActivityServiceTests` suite was discovered (44 tests) but every test failed in fixture setup before its body ran: SQLite reported `near "max"` during `EnsureCreated`. Thus backend save/reload is not verified here.
- **Database impact:** None. No migration or data write.
- **Remaining limits:** SQL Server persistence and authenticated page-refresh behavior require Development availability and Product Owner Browser UAT.

## Issue 07 - Status Report Financial Summary

- **Original symptom:** `Create Report > G. Financial Summary` displayed “Could not load financial summary”; a related endpoint returned HTTP 500.
- **Root-cause evidence:** The previous financial query placed `string.Equals(currency, projectCurrency, StringComparison.OrdinalIgnoreCase)` inside an EF SQL predicate. That overload is not translated by the SQL Server provider and is a source-confirmed failure candidate in the request path. The 500 trace/log was not supplied, so the exact runtime exception is not independently confirmed. The previous summary also used `Project.Budget` (the lifetime ceiling) as if it were an approved annual budget and omitted period expenditure/commitment values and an explicit empty-data state.
- **Fix:** The query now filters only by translatable project/tenant/date predicates before applying normalized currency checks in memory. It reads approved `ProjectAnnualBudget` values against configured fiscal-year boundaries, direct and labor actuals within the selected period, active purchase commitments, and settlement/obligation indicators. It does not convert or combine currencies. Invalid or mixed data yields partial/unavailable values instead of fabricated amounts. The DTO distinguishes empty, partial, unavailable, and available data. The API read remains `StatusReport.View` plus authenticated Tenant/Project scope; no finance mutation permission or new permission key was added. The UI has separate loading, API error, empty, partial, and unavailable states and uses only the returned currency code.
- **Files:** `ProjectReportingService.cs`, `ProjectReportingDtos.cs`, `ProjectEvmDirectCostLookup.cs`, `ProjectEvmLaborCostLookup.cs`, `ProjectEarnedValueContracts.cs`, project API DTO/query definitions, `ProjectGovernanceTabs.tsx`, EN/AR message catalogs, `project-status-reports-parity.test.tsx`, the canonical permission map, and `PROJECT-MANAGEMENT-CORE.md`.
- **Automated evidence:** Financial Summary React tests passed 11/11. The seven backend fixture tests were discovered but failed before test bodies during SQLite `EnsureCreated` with `near "max"`. No SQL Server persistence test or authenticated Development request ran.
- **Database impact:** None.
- **Remaining limits:** The original 500 trace is unavailable; SQL Server query translation, persistence, live permission outcomes, and Browser UAT remain unverified. A valid but empty dataset is covered by source and React tests, not by an executed backend test.

## Issue 10 - Program creation HTTP 500

- **Original symptom:** `POST /api/programs` failed with HTTP 500.
- **Confirmed root cause for the supplied Program POST log:** Trace `0HNPTK3B470SR:00000001` reports SQL error 515: SQL Server cannot insert NULL into non-null `Programs.RowVersion`. This differs from the initial task's trace ID (`0HNPTK3B470LB:00000001`), which was not present in the supplied log. The baseline migration defined that column as `varbinary(max)` without store generation, while `ProgramConfiguration` and the current model snapshot define it as SQL Server `rowversion`, which is generated by the database on insert and update. That schema/model mismatch is consistent with the failing insert. SQL Server rowversion semantics are described in [Microsoft Learn](https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql?view=sql-server-ver17).
- **Fix prepared:** Added migration `20261108170000_RepairProgramRowVersion` to replace only `Programs.RowVersion` with generated `rowversion`; its Designer uses the synchronized current model. EF generated a scoped idempotent script from the preceding repository migration to this repair; review shows one guarded drop/recreate of this token column and one migration-history insert. Script SHA-256: `D89F27A7C3C2D5220E3A955A197FB8616134D13B0AE0877169E25E88BE1055A2`. Script was held in the system temporary directory and was not applied.
- **Business rules:** Current creation permits a null manager, requires an active eligible Portfolio, validates currency and tenant/organization scope, and uses the existing `Program.Create`/cross-Portfolio capability checks. `Program.CreateAcrossPortfolios` is already cataloged and mapped in Roles & Permissions; no permission or grant change was made.
- **Files:** New migration and Designer; `ProgramCreationServiceTests.cs`, `program-sheet.test.tsx`; UAT checklist.
- **Automated evidence:** Program backend tests passed 3/3 in an isolated output; Program Sheet React tests passed 9/9. These use the test provider and do not prove SQL Server persistence. EF offline model gate reports synchronized with 16 source migrations.
- **Database impact:** Migration is pending and unapplied. No Program row or database schema was changed.
- **Remaining limits:** The read-only gate verified the physical `Programs.RowVersion` type is `varbinary(max)` and EF expects `rowversion`; it also found other Program column/index drift and Integration schema drift. The pending Program migration overlaps a drifted table, so the gate's unrelated-drift exception cannot be used to bypass review. Independent backup, migration application, and SQL Server create/reload remain open. Program creation on Development cannot be called fixed until a reviewed migration plan is safely delivered.

## Issue D1 - Work Items HTTP 404

- **Observation:** `GET /api/work-items?projectId=...&type=Task|Issue` returned 404.
- **Investigation/result:** The route exists. Project PublicId middleware resolves a same-Tenant PublicId and returns non-disclosing 404 for stale, foreign, or unsupported internal IDs. The captured request identifier and a matching server trace were unavailable, so a product defect was not reproduced and no code changed.
- **Remaining limits:** Verify with the original Project identifier and actor scope during UAT.

## Issue D2 - Meeting Minutes React Query undefined data

- **Root cause:** `Ok(null)` could be converted to HTTP 204. The API client maps 204 to `undefined`, which React Query rejects.
- **Fix:** A valid scoped Meeting with no Minutes now returns explicit JSON `null` with HTTP 200. Missing/inaccessible Meeting and real API errors remain separate.
- **Files:** `MeetingController.cs`, `MeetingMinutesControllerTests.cs`, focused API test project references and test fixtures, `MEETING-MINUTES-COMPLETION-FIX.md`.
- **Automated evidence:** `MeetingMinutesControllerTests` passed 4/4. A broader Meeting run reported 15 passed and 17 failed; corrected fixtures were not rerun to completion. No browser/runtime API UAT ran.
- **Database impact:** None.

## Issue D3 - Meeting details HTTP 404

- **Investigation/result:** Existing details routes use Meeting PublicId; Minutes and lifecycle operations use the resolved internal Meeting ID; deleted details remove their cached entry. No ambiguous ID fallback or scope bypass was found. The supplied observation lacked a reproducible Meeting identifier/trace; no code change was made.
- **Remaining limits:** Confirm the original route ID and deletion timing during UAT.

## Issue D4 - `/api/auth/me` 401

- **Investigation/result:** 401 is expected for a missing, invalid, revoked, or expired persisted session. The captured requests did not include enough session evidence to establish a genuine session defect. Authentication code was not changed; HTTPS, CSRF, secure-cookie, and refresh behavior were preserved.
- **Remaining limits:** Compare absent/expired sessions with a valid persisted session during UAT.

## Issue P0 - Approval List API retry loop - 2026-11-10

- **Browser evidence supplied by the Product Owner:** Opening My Work → Approval List triggered 21 HTTP 400 responses from `GET /api/handover-requests/pending-for-me`, 21 HTTP 403 responses from `GET /api/manager-handovers/pending-for-me`, and repeated `refreshAfterForbidden` executions within seconds. This was not independently reproduced in the current authenticated browser session.
- **Source finding — retry policy:** The shared React Query policy returns no retry for `ApiError` 4xx responses. It allows one retry for a transient server/network failure. The reported 400/403 request counts are not explained by deterministic 4xx retry configuration alone.
- **Source finding — authentication refresh:** Each authenticated 403 dispatches `nexus:authorization-denied`. The previous listener could refresh current user permissions repeatedly while permission-gated query observers were being toggled. It now refreshes once per local session and does not clear permission state during the request.
- **Source finding — project handover 400:** `HandoverRequestController.GetPendingForMe` previously returned 400 for every service failure, including a `Forbidden` error. It now preserves the service status, so a Forbidden result returns 403. The live service result behind the supplied 400 was unavailable; this is the confirmed mapping defect and the likely explanation if the service denied the user.
- **Source finding — manager handover 403:** Backend eligibility requires `Handover.Approve` and an active Management-category role. A 403 is expected when either condition is missing. The affected user's live grant/category and project scope were not verified, so no access bypass was added.
- **Frontend behavior:** Project and manager handover calls are gated on the required permission/category, each queue has its own error/retry state, and failure in either queue leaves separately authorized Change Request and Annual Budget approvals visible. Annual Budget authorization remains permission- and scope-based.
- **Regression tests:** Query retry status policy, repeated 403 permission-refresh deduplication, independent approval-category loading, and project handover controller status mapping were added. Focused frontend tests passed 24/24 across four files. The single API test/build attempt compiled `NEXUS.Api` but the API test project failed before test execution due two syntax errors in Annual Budget test files; those errors were fixed afterward without a retry. The controller regression remains unexecuted.
- **Database impact:** None. No migration or schema change is required; no database read or write was performed for this frontend/auth/status-mapping recovery.
- **Remaining verification:** API controller regression execution and authenticated browser UAT remain open. The user-reported 21-request pattern and affected user's live role/grant state were not reproduced or inspected here. Report source fixes, frontend results, and browser behavior separately.

## Build, test, and database gates

| Gate | Result |
|---|---|
| API dependency/test build | Program focused test build succeeded in an isolated output; the API dependency graph compiled. |
| Full API build | Default output failed after MSB3027/MSB3021 retries because the active API process held `apps/api/src/NEXUS.Api/bin` files. The process was left running. Rebuilding to isolated output succeeded with 0 errors and 9 NU1900 vulnerability-source warnings. |
| EF offline model gate | Passed. Context `NEXUS.Persistence.AppDbContext`; migration assembly `NEXUS.Persistence`; 16 source migration IDs; model synchronized. No database connection opened. |
| React typecheck | Passed: `npm run typecheck`. |
| React lint | Failed: one existing `no-regex-spaces` error in `src/test/container-runtime-security.test.ts`; 10 warnings elsewhere. No unrelated lint file was changed. |
| React production build | Passed on Windows host after sandbox `spawn EPERM`; Vite reported existing large-chunk and dependency annotation warnings. |
| Focused React tests | Planning grid 53/53; Financial Summary 11/11; Program Sheet 9/9. |
| Focused API tests | Program creation 3/3; Meeting Minutes controller 4/4. Plan activity 0/44 and financial summary 0/7 test bodies ran because SQLite schema setup failed with `near "max"`. |
| Read-only Development inspection | Windows-host gate verified `Shady/NexusPmo`, database ID 5, and Development. It found 15 applied migrations and one pending Program RowVersion migration. Physical schema check returned No with 16 differences; baseline drift fingerprint: `94EB2B3BE9926C9397C1CAF8952111C36F9BDF822DB5EB8F4876678AFEC0457E`. The earlier sandbox attempt failed with `Cannot generate SSPI context`. |
| Resume read-only Development inspection | **Not completed:** current runner was denied at SQL Server TCP connection setup (`Access is denied`), before live identity/history/schema could be rechecked. |
| Resume EF offline model gate | **Passed:** model synchronized; 16 source migrations. Build completed with 10 `NU1900` warnings and 0 errors. |
| Database writes/migrations/seeding | None. Migration `20261108170000_RepairProgramRowVersion` is not applied. Seed mode/idempotency: Not run. |
| SQL Server persistence tests | Not run; there was no verified SQL Server test target. |
| Product Owner Browser UAT | Not run; owner retains UAT. |

## Required database completion fields

- **Model synchronized:** Yes (offline EF model gate).
- **Pending Development migrations:** 1: `20261108170000_RepairProgramRowVersion`.
- **Development DB identity verified:** Yes in the earlier host inspection: `Shady/NexusPmo`, database ID 5, Development. Fresh verification: Not run; the current runner was denied before opening SQL Server.
- **Backup status at report time:** No independent backup was verified. Under the current Development/UAT policy, this is informational and does not block an operation.
- **Migrations applied:** None.
- **Seed mode/idempotency:** Not run; no reseed requested or performed.
- **Physical schema verified:** No against the last applied model; the live read-only gate found 16 differences, including `Programs.RowVersion` (`varbinary(max)` actual vs. `rowversion` expected), Program name/code/status length differences and indexes, and Integration schema differences.
- **Persistence tests:** SQL Server: Not run. SQLite-backed financial and planning fixtures failed during schema setup, before test bodies.
- **Blockers:** The current runner cannot connect to SQL Server (`Access is denied`), so live target/schema re-verification and SQL Server persistence checks remain blocked. The last successful inspection found overlapping physical Program drift plus Integration drift; resolve those in a reviewed migration plan before applying. A missing backup is not a blocker under the current Development/UAT policy. Full API build passed to isolated output; React full lint has the existing regex error listed above.
