# Sprint S6 — Assurance and Product Owner Handover

**Date:** 2026-11-14  
**Result:** **PARTIAL - PRIOR FRONTEND TEST/BUILD PASS; LATEST RECOVERY/SCOPE SOURCE AND API/DB/RUNTIME OPEN**

## Integrated source evidence

- Canonical source catalog baseline: 188 active permission keys; the Protected Tenant Admin Core is 63 distinct keys under the recorded decision.
- Route census: 676 routes in the original S0 source snapshot; 683 rows in the current CSV. Exact action/controller metadata resolves 617 rows to Tenant-catalog keys/expressions, 8 to Platform-only policies, and 58 to no direct `Policy =` value. All 683 now have source-level service/resource scope and React consumer or explicit no-current-consumer mapping; zero source/UI fields remain marked unverified. Runtime behavior remains unverified.
- Added or updated source-level regressions cover Protected Core behavior, initial tenant Admin grants, seed/provisioning, Identity API metadata and cross-tenant override isolation, Payment Request/Reversal/Closing recovery, Change Request mandatory Sponsor protection, actor permission freshness, dependency cycles, and Roles grouping.
- New Payment Request recovery records the initial submitted approver IDs, positions, and requirements in the immutable audit event; reassignments record the old/new approver IDs, assignment, reason, actor, and request state.
- Fixed active device-token cross-user/cross-Tenant ownership reassignment and generic workflow provisioning with entity-specific write permission, scope, actor, and read-only checks. Regression source is present; API tests remain blocked.
- No EF migration, DB reset, manual SQL, seed run, Production/UAT write, or merge was performed.
- Focused frontend permission-business-module/access-security suites passed 39/39 and the production build passed before the latest continuation changes. The added recovery controls and expanded API regression sources remain unverified; rerun the frontend tests/build on current source.

## Blocking evidence gaps

- API tests/build were not completed. `dotnet --info`/SDK initialization reports `Cannot open Service Control Manager` with `Win32Exception (5): Access is denied`; the filtered test process stalled at “Build started” and was stopped. React tests/build passed.
- Development DB permission rows, Admin grants, and effective runtime permissions were not reconciled in this task.
- Runtime policy resolution, latest recovery and scope UI behavior, SQL Server lock/concurrency behavior, and approval recovery/decision transitions remain unverified; current code has not been built or tested.
- Runtime scope coverage across all 683 routes, Organizations, Projects, Portfolios, Programs, resource ownership, and malformed ownership chains remains open. The source-only 58-row review does not replace API/runtime verification.
- Payment Request, Reversal, and Year-End Closing recovery source paths and UI controls are implemented; reassignment requires current-assignee ineligibility; Year-End Closing cancellation requires no eligible replacement. Annual Budget uses authorized Withdraw and Change Request guards mandatory Sponsor assignments. The exception policy for request-type self-approval is not persisted or configurable.
- S5 uses the 48 module/screen groups present across all 188 catalog keys. PermissionCatalog has no separate FeatureId field, so the older 47-feature count is not a directly comparable source invariant; any 47-feature parent map remains a product taxonomy choice. Six generic ApprovalRequest keys are marked No verified UI control in EN/AR. Broader Arabic quality, RTL/accessibility, and browser behavior remain open. Scope filtering is available in the role grant editor.
- Licensing/recovery integration is deferred because no authenticated external provider contract was supplied.
- The requested `feature/rbac-approval-governance` branch could not be created because the checkout’s Git metadata is read-only here. Work remains uncommitted on `Document-Center`.

## Product Owner verification sequence

1. Resolve the .NET SDK initialization permission issue, then run focused API regressions for Identity, Protected Admin provisioning/seeding, direct override isolation, dependency cycles, Payment Request/Reversal/Closing recovery, and Change Request Sponsor protection.
2. Complete browser checks for Roles module/feature grouping, EN/AR rendering, RTL, keyboard/screen-reader behavior, and protected/denied/dependency presentation. Focused React tests/build already passed.
3. Build the API, then exercise protected Admin mutations and approval recovery in the verified Development environment.
4. Reconcile source catalog, persisted permissions/grants, and runtime effective permissions without resetting the database.
5. Continue the current 683-route scope matrix and implement/verify recovery for the remaining supported approval types before claiming closure.

No current API/frontend test suites, full API build, DB operations, API runtime checks, or browser UAT were completed for this handover. The WorkItems, Portfolio, and Persistence project builds pass after fixing the Owner-reported compile errors. The prior focused frontend tests/build predate the latest recovery and scope changes; broader runtime behavior remains unverified.
