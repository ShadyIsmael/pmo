# Permission audit - executive summary

**Audit date:** 2026-11-02  
**Scope:** canonical permission rebuild across the supported `apps/web-react` frontend, API policies/services, source catalog, existing audit matrix, and UAT documentation.

## Result

The current source catalog contains **154 unique active permissions**: **39 View** and **115 Action**, with unique seed IDs and Arabic/English labels. Frontend constants match all 154 keys, and every permission policy registered in `Program.cs` resolves to an active catalog entry. The protected tenant Admin baseline contains **48 keys** by source assertion. These are source findings; they do not establish persisted grants or runtime effective access.

The prior repository audit reports recorded 133 active keys. This rebuild adds 24 financial capabilities, retires `ProjectFinance.Manage`, and removes redundant/unsafe `Resource.Allocate` and `Resource.ManageRates` keys, for a net increase of 21 and the current count of 154. Resource allocation is keyed by `Resource.Assign`; rate visibility and editing are separated, with editing dependent on view. The task brief also referenced an earlier 136-active snapshot; no matching catalog snapshot is present in the repository, so the three-key difference between that number and the 133-key audit baseline cannot be reconciled from available evidence. Current source count is verified directly.

The source authorization gaps recorded in the earlier audit have been addressed: Issue and Plugin reads have dedicated tenant-scoped View permissions; route guards use canonical View permissions; optional project-setup budget creation uses `ProjectAnnualBudget.Create`; and status-report export has a server-authorized endpoint restricted to Published reports.

Static inventories contain **602 HTTP method attributes** in **855 API source C# files** (`GET 268`, `PUT 84`, `POST 194`, `DELETE 48`, `PATCH 8`), **130 route declarations** in `AppRoutes.tsx`, and **1,774 selected UI event tokens** across 408 React source files. The updated matrix has 202 records: 154 active catalog capabilities, 15 additional source action/surface/exception records, 32 retired/history records, and 1 documentation count record. Those are not a deduplicated semantic business-action denominator; full action coverage is not computed.

## Current blockers

- SQL Server identity verification failed in this execution session with `Cannot generate SSPI context`; no database read or write succeeded.
- The EF gate includes a guarded `reset` path for only `Shady` / `NexusPmo`. Its project builds, and the offline EF model check reports synchronization with 9 repository migrations. No live database was opened. Under the current policy, a reset requires successful target identity verification and explicit Product Owner authorization; no backup is required for this exact target, while ordinary database writes retain the backup gate.
- Persisted Permission rows, RolePermissions, direct overrides, and effective runtime grants remain uninspected.
- API solution and React production builds passed. The React test suite had 724 passes and 38 failures; the API authorization test project failed before tests executed. Direct runtime and browser verification remain pending. See `SECURITY-TEST-RESULTS.md`; no runtime UAT claim is made here.

## Canonical rebuild behavior

The source catalog contains no inactive compatibility entries. Retired aliases are not valid effective grants. The repository still contains an opt-in reconciliation tool, but the current approved operation is a clean Development database recreation through EF migrations followed by the full seed; neither operation has run in this session.

The audit matrix retains its existing row IDs and source references. Its active catalog rows now carry canonical bilingual labels from the code catalog; stakeholder policy evidence was corrected. No duplicate audit report was added.

## UAT and database status

Positive, negative, and cross-tenant/project scope scenarios are recorded in `docs/quality/MASTER-UAT-CHECKLIST.md`. They remain unverified at runtime. No database migration or data write was performed.

| EF delivery field | Status |
|---|---|
| EF model synchronized | Yes in offline model check; live database was not opened. |
| Pending migrations | Not checked against the live database; SQL identity could not be established. |
| Development DB identity verified | No. |
| Backup verified | No; the read-only identity probe failed before the recovery gate. |
| Migrations applied | None in this execution. |
| Physical schema verified | Not run. |
| Persistence tests | Not run against SQL Server; the API test project did not execute. |
| Blocker | Current Development identity, explicit Product Owner reset authorization, and secure Admin bootstrap readiness are required before the exact `Shady/NexusPmo` reset and EF seed; the reset-specific backup waiver applies only to this target. |

## Existing audit deliverables

This update modifies the existing action matrix, endpoint inventory, executive summary, catalog review, gap report, proposed catalog, remediation plan, and system action inventory in place. The UI handler CSV remains a raw event-token inventory. Existing matrix row IDs and source references remain intact.
