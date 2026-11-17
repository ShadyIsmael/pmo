# RBAC Implementation Final Report

**Date:** 2026-11-15  
**Readiness classification (build-only phase):** **IMPLEMENTATION COMPLETE — BUILD VERIFIED — TESTS DEFERRED**  
**Database:** **DATABASE APPLICATION PENDING — HOST ACTION REQUIRED**  
**Runtime/API:** **NOT VERIFIED**

> **Status reconciliation (2026-11-15):** This build-only disposition supersedes the historical BLOCKED label and test-execution gate below for the current phase. All requested source builds now pass with zero errors, including API test-project compilation only. The historical details remain as evidence for earlier attempts. No tests or runtime/API/UAT checks were run in this phase. See [RBAC build-only handover](RBAC-BUILD-ONLY-HANDOVER-2026-11-15.md) for exact commands, per-project results, warning counts, database status and host instructions.

The final integrated compile-only build of `NEXUS.Api.Tests.csproj` used project references and compiled SharedKernel, referenced API modules including MppAdapter, Persistence, API, and test sources together. SDK `11.0.100`; exit 0; 389 warnings; 0 errors; 5:27.78. No tests were discovered or executed. This supersedes the older body below that describes the test project compilation as blocked; earlier failed attempts remain historical.

## Executive status

This coordinated execution implemented the approved source-level direction for two System Role identities, separate canonical role identity, explicit delegation authority, scoped Organization Admin configuration checks, direct override validation, React alignment, and preservation-oriented migration/seed source. Independent reviews found a SQL CHECK NULL-semantics defect and a seed reconciliation risk for extra grants on an existing Organization Admin role. Explicit non-null guards and a fail-closed seed guard were added; the updated Persistence project compiles in isolated output. Neither guard's regression tests executed.

The task is not verified as complete. The Development DB could not be identified from this execution context, so the migration was not applied and live role/membership/grant counts are unknown. The authenticated Development API P0 journey was not run; the previous `POST /api/users -> 403` root cause and resolution remain unconfirmed. The React production build now passes after replacing the missing permission reference with a canonical key. Focused React coverage passes, including the new row-level Organization scope guard. .NET API tests did not reach discovery due IKVM reference generation; ResourceManagement seed tests compiled but VSTest could not connect to testhost. No .NET cases ran. Product Owner browser UAT was not performed.

## Scenario baseline and ratings

- **90 baseline scenarios:** 83 original rows plus 7 approved Organization Admin rows.
- **42 Direct / 48 Corner.** These are reviewed baseline counts, not passing coverage counts.
- The 83 original IDs, classifications, weights, and severities are preserved. Independent static comparison found **0 ID/classification/weight/severity mismatches**.
- OA-01..OA-07 retain Product Owner-provided classifications and W/S: OA-01..03 Direct (5/S1); OA-04..06 Corner (5/S0); OA-07 Corner (4/S1).
- The existing 16 additional RBAC candidates remain proposals and are not counted as approved implementation requirements. Workflow checks WF-01..WF-19 are acceptance/test suggestions, not new approved business authority.
- No original Weight or Severity adjustment was made. No candidate was promoted merely because source or tests support it.

Detailed row status and acceptance coverage: [RBAC scenario implementation traceability](RBAC-SCENARIO-IMPLEMENTATION-TRACEABILITY.md). The historical analysis documents are superseded for implementation status by this report and the linked evidence reports; original scenario IDs and change history remain preserved.

## Role inventory and architecture

| Inventory view | System roles | Ordinary roles | Evidence boundary |
|---|---:|---:|---|
| Prior seed source analyzed | 8 | 0 of those eight were classified Non-System | Source only: Tenant Admin plus seven operational roles; Organization Admin was absent from that source seed. |
| Current seed source | 2 | 7 | Source only: Tenant Admin and Organization Admin are `IsSystem=true`; Project Manager, Portfolio Manager, Executive, PMO Admin, Finance Controller, Resource Manager, and Team Member are Non-System. |
| Actual Development DB before/after | Unknown | Unknown | No live identity/history/role query succeeded; no migration/seed ran. |

Source changes separate immutable `RoleKey` from `SystemRoleKey` and `IsSystem`. Tenant Admin remains the canonical protected role with mandatory Tenant Administration Protected Core and last-effective-Admin safeguards. Organization Admin is a distinct System identity but is not granted Tenant Admin Core. Category is descriptive metadata. Protected, System, Assignable, Editable, Canonical Identity, and category are not treated as synonyms.

Canonical source references include `Role.cs`, `RoleConfiguration.cs`, `ProtectedTenantAdminPolicy.cs`, `PermissionCatalog.cs`, `SeedDataProvider.cs`, and migration `20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification`.

## Delegation and P0 user creation journey

The source policy allows a verified protected Tenant Admin to assign an eligible ordinary Project Manager role without possessing the role's operational permissions. Assignment still requires `Users.ManageAccounts` and `Users.AssignRoles`, validates same-Tenant role identity and each catalog permission, enforces scope/dependencies/restricted Core/reserved keys, denies self-escalation, and does not grant the delegated operation to Admin or confer approval eligibility.

The exact P0 flow is source-supported and has a DEL03A test fixture: Admin lacks `Project.Edit`, assigns the Project Manager role, the target receives the role/permission, and Admin remains without `Project.Edit`. **The fixture's execution result and the authenticated API success path are not yet confirmed.** The previous `POST /api/users -> 403` remains unresolved as an incident: no request trace tied the failure to any specific controller, actor binding, assignment permission, role policy, persisted role classification, or deployed assembly.

Assigning the Project Manager role to a user is not the same as appointing that user to a Project. Project assignment remains a separate candidate eligibility and Handover transition.

## Organization Admin scope and coverage

Current source uses the exact canonical Organization Admin role identity, active same-Tenant membership, fresh effective Organization Configuration permission, and explicit active per-user/per-Organization settings grant. The Organization setting catalog must permit an Organization override. Known source-supported surfaces include organization profile/settings values, scoped email notification settings, Organization Phase Gate configuration, and Organization Project Lifecycle phase/status catalogs. The lifecycle catalog UI now suppresses edit/default/status controls for inherited Tenant-owned rows while preserving controls for the selected authorized Organization and Tenant-authorized users.

Tenant, Platform, unrelated Organization, Protected Tenant Admin, unrelated operational, finance approval/payment, and project operational authority are kept separate. `ProjectLifecycle.OptionsLookup` remains under its existing operational permission. The UI derives Organization choices from the server-authorized profile list, does not use ManagementScope as scope, hides Tenant-wide lifecycle options for OA-only actors, gates mutations on Manage plus `canManageSettings`, and renders unknown project aggregates as localized N/A.

**Specific gaps:** BusinessUnit is Organization-owned but its current API stays Tenant-configuration protected and its by-ID read lacks Organization-grant validation. Organization Unit/workflow ownership is not established. OA-03 is resolved as Organization-level configuration authority only; Organization lifecycle-state changes and Project operational actions remain separately authorized. OA appointment/removal/protection/recovery/last-OA governance is unresolved and ordinary appointment remains denied. See [Organization Admin verification](RBAC-ORGANIZATION-ADMIN-AUTHORITY-VERIFICATION.md).

## Migration and preservation

Migration source backfills `RoleKey`, reclassifies unapproved Tenant System rows in place, validates collision/malformed cases, adds a unique tenant role-key index and strict two-key check, and does not delete/recreate roles or update membership/grant/audit links. Seed source resolves canonical roles by identity; it adds the canonical Organization Admin role with scoped configuration permissions and no seeded membership. Existing canonical Organization Admin roles with any active grants outside the approved Organization Configuration set now cause seed reconciliation to fail closed before role identity mutation; no grant is silently removed. `Down` fails before mutation.

The independent reviewer found the initial nullable CHECK constraint could accept SQL `UNKNOWN`; explicit `SystemRoleKey IS NOT NULL AND RoleKey IS NOT NULL` guards have been applied consistently to model/migration/designer/snapshot and SQLite theory cases were added. The source fix is confirmed; its tests and SQL generation remain unverified. The seed grant guard has two focused test cases, compile-verified but not executed.

**Database execution: none.** Development target identity, model synchronization, pending migration list, actual role IDs/keys/counts, memberships, grants, overrides, current Protected Core and before/after preserved counts are unknown. No DB write or seed execution occurred. See [migration and data integrity evidence](RBAC-MIGRATION-AND-DATA-INTEGRITY-EVIDENCE.md).

## Build/test evidence

- React Organization Admin tests: the earlier two-file command passed 12 tests; the post-change lifecycle catalog command passed 6 tests in one file. These commands overlap, so 18 is a count of test executions across runs, not unique test cases. The latter run verifies the new inherited Tenant row is read-only in UI and the scoped Organization row remains manageable.
- React production build: **passed** (`tsc` + Vite, 32.09 seconds) after replacing `Permissions.Governance` with canonical `Permissions.AssignmentRecovery.Manage` (`Governance.AssignmentRecovery.Manage`). Existing Rollup annotation, mixed-import and chunk-size warnings remain.
- API source build: succeeded in isolated temporary output with 0 errors and 352 IKVM warnings. A build targeting the normal API output failed copying locked DLLs; no process was stopped. Runtime inspection found no visible API process/listener on the checked Development ports, and CIM inspection was denied, so the serving assembly remains unknown.
- Updated Persistence source build: passed in isolated output, 0 warnings / 0 errors, after adding the fail-closed seed guard. The focused ResourceManagement test project also compiled with 0 warnings / 0 errors; this is compilation only.
- Focused .NET tests: no cases executed. The API test build was interrupted during IKVM reference generation before discovery. The ResourceManagement seed test DLL matched, but VSTest testhost connection timed out; a bounded retry was stopped with 0 totals. The new seed tests and Identity/API authorization tests remain unexecuted.
- Broader React tests produced no completion output and were interrupted; they are neither pass nor fail evidence.
- No Development authenticated API, DB query, seed execution, serving-assembly verification, or browser UAT was performed.

Exact commands and partial/interrupted results: [RBAC test execution results](RBAC-TEST-EXECUTION-RESULTS.md).

## Modified implementation surface (core files)

The coordinated source work is grouped here; the worktree also contains extensive pre-existing/concurrent modifications whose ownership cannot be inferred safely from `git status` alone.

- **Identity / role model:** `Role.cs`, `RoleAssignmentAuthorizationPolicy.cs`, `RoleAdministrationService.cs`, `UserAdministrationService.cs`, `UserEffectivePermissionService.cs`, `ProtectedTenantAdminPolicy.cs`, `RolesController.cs`, `UsersController.cs`, role DTOs and permission catalog.
- **Persistence / seed:** `RoleConfiguration.cs`, `SeedDataProvider.cs`, `AppDbContextModelSnapshot.cs`, the new role-identity migration and designer.
- **Organization / lifecycle:** `OrganizationSettingsAccessService.cs`, organization profile/settings, phase-gate, email configuration, lifecycle catalog controller/service and scoped mutation paths.
- **React/Vite:** `AppRoutes.tsx`, administration navigation/Sidebar/Hub, user/role permissions pages, organization settings and lifecycle catalog UI, API query paths, English/Arabic labels, focused tests.
- **Security/test coverage:** service/API tests for delegation, organization scope, read-only context, direct overrides, identity constraints, finance separation, and lifecycle catalog boundaries. Exact added test inventory is in the scenario traceability and test-results report.
- **Architecture/documents:** `docs/architecture/permission-model.md`, scenario implementation traceability, migration/security/OA/critical/UAT/final reports.

No Git reset, branch switch, stash, merge, database reset, or browser UAT occurred.

## Decisions still required from Product Owner

1. Who may appoint/remove/recover Organization Admin, whether/how it is protected, scope-grant administration, cardinality, and last-Organization-Admin recovery.
2. Whether BusinessUnit and other organization-owned management data are included in OA-01/02; if so, exact list/by-ID and write scope model.
3. Expected user-management experience for `Users.ManageAccounts` without `Users.View` (current route/list permissions are mismatched for least-privilege actors).
4. A concrete Tenant-enforced/inherit-only catalog example for OA-07 production-grade acceptance; a fixture is not a policy approval.

No new administration authority was granted for unresolved cases; current behavior remains fail-closed.

## Proposed sequencing to complete verification

1. Obtain host-side sanitized Development preflight and read-only role/membership/grant inventory.
2. Review the generated role migration SQL and hash against the verified Development target; run pending-model/schema checks and focused persistence tests.
3. Apply only through the documented Development gate after the existing task authorization and exact target review; run the authorized idempotent seed mode and compare role IDs/memberships/grants/overrides/audit before and after.
4. Resolve the .NET testhost/IKVM execution boundary without weakening assertions; rerun relevant Identity, Organization Admin, and seed suites and record totals. React production build and focused OA tests currently pass.
5. Start/identify the actual Development API assembly without replacing a process unexpectedly; exercise authenticated P0 user creation and scoped OA positive/negative API requests with trace/audit/persistence evidence.
6. Close Product Owner decisions, update the decision register, then hand the verified build/runtime to Product Owner for browser UAT.

## Final recommendation

## Recovery classification

| Status | Current disposition |
|---|---|
| **IMPLEMENTED AND VERIFIED** | React canonical permission reference; lifecycle catalog row-level UI scope guard; React production build; focused UI tests (12 in the earlier run and 6 in the final focused run). Persistence project compiles with the new fail-closed seed guard. These are limited build/UI/source-compilation results, not end-to-end verification. |
| **IMPLEMENTED BUT NOT VERIFIED** | RoleKey/SystemRole migration; System role seed/reconciliation; delegation and effective-permission policy; direct override checks; Organization Admin server authorization and scoped configuration journeys; new fail-closed extra-grant seed behavior. Actual database state and .NET test execution are unknown. |
| **FAILED** | No currently executed functional test failed. Earlier React build failed but is now fixed and passes. Do not interpret this as proof of no defects. |
| **BLOCKED** | .NET test execution (IKVM/testhost); verified Development DB identity/history/schema/role inventory; migration SQL review/application and seed run; serving API assembly identification; authenticated Project Manager onboarding and Organization Admin API scenarios; browser UAT (reserved to Product Owner). |
| **REQUIRES PRODUCT OWNER DECISION** | Organization Admin appointment/removal/recovery/protection/cardinality and last-admin policy; whether BusinessUnit and other organization-owned data are included in supported OA configuration; expected least-privilege user list behavior for `Users.ManageAccounts` without `Users.View`; a production catalog example for the OA-07 Tenant-enforced override denial. |

## Final recommendation

**BLOCKED.** React production readiness and scoped UI behavior are now verified by build and focused tests, but the key security acceptance gates—.NET tests, actual Development role/grant inventory and migration preservation, and authenticated API behavior—remain unverified. Do not report the repeated 403 as fixed or the RBAC change as release-ready. Product Owner browser UAT should start only after the missing gates and decisions are closed.

The post-recovery independent reviewer confirmed the canonical React permission mapping, inherited Tenant-row UI guard, and fail-closed Organization Admin seed guard, and found no new critical source regression. It also confirmed that source compilation and UI tests do not establish API/database behavior.


## Final recovery addendum — 2026-11-15

The current recovery remains **BLOCKED — NOT VERIFIED**. The six FinanceMutationActorBindingTests calls now pass the direct ManagementContext fixture required by the production signature, with assertions and values preserved. The test-project build stopped before C# compilation; focused tests discovered/executed 0. The focused restore failed with MSB4276 missing workload locator SDKs under .NET SDK 10.0.302. See RBAC-TEST-EXECUTION-RESULTS.md and RBAC-FINAL-RECOVERY-REPORT.md for commands and outcomes.

The host preflight confirms Shady/NexusPmo DB_ID 5, Windows Authentication, and 27/28 migrations, but actual role inventory and preservation counts remain absent. The reviewed SQL hash matches; no migration or seed ran. No API process/assembly was verified and no authenticated API scenario ran. Thus the Project Manager onboarding 403 is not proven resolved and Organization Admin configuration boundaries are not runtime-verified. Independent source review found no new critical regression but does not clear these gates.
