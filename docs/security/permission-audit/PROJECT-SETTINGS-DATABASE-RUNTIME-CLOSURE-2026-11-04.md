# Project Settings Permissions: Database and Runtime Closure

**Backup-policy supersession (2026-11-10):** Backup and connectivity statuses below are historical observations from the 2026-11-04 execution. Development/UAT operations do not require backups, independent storage, or `RESTORE VERIFYONLY`; a missing backup does not block authorized work. The exact target, migration, schema, and separate authorization checks remain required. Production backup and recovery safeguards remain mandatory.

> **Current closure status (2026-11-04): BLOCKED for authenticated financial configuration UAT.** The API builds and starts, `/health` returns 200, and the live Development database/migration/schema identity is verified. No authenticated settings request or SQL save/reload was made. The database has no `ProjectAnnualBudget.ManageApprovalConfiguration` or `ProjectAnnualBudget.Approve` grants, so end-to-end setup and submission could not be completed.

## Current verification (2026-11-04)

### Entry points and source/UI evidence

- The Project Header Settings Side Sheet remains the unified entry point for General Settings, Workflow & Approval Configuration, Financial Approval Settings, and Annual Budget Approval Defaults. The supported frontend is `apps/web-react`; legacy workflow/settings links request this Side Sheet. Project Reporting remains on its specialized `/settings/reporting` route, as documented by Master UAT PWF-04.
- The current focused React settings suite passed **20/20** with a 15-second per-test timeout. It covers `Both`, mandatory approvers in sequential order, hiding warnings for valid defaults, and the no-eligible-approver explanation. English and Arabic message files parsed as JSON. `npm run typecheck` produced no output for more than five minutes and was stopped; typecheck remains unverified.
- The API project built after current source edits with **0 warnings and 0 errors**. A temporary loopback-only Development API was started with `--skip-development-database-initialization`; `GET /health` returned **200 Healthy**, then the host was stopped. This verifies process health only and did not exercise project settings APIs, authentication, or SQL persistence.
- The generic focused `UserPermissionAdministrationTests` run passed **30/30**. After minimally correcting two existing test-fixture compilation issues, the current targeted `ProjectSettingsAuthorizationTests`, `FinancialPermissionDelegationTests`, and `FinancialApprovalDelegationBoundaryTests` run passed **30/30**. Coverage includes Allow, Active Deny, Tenant/Project scope, settings save/reload and ordered mandatory-approver inheritance with an audit snapshot, and an authorized one-time override that does not rewrite saved defaults. Persistence fixtures use EF InMemory and do not establish SQL Server persistence or transaction behavior.
- A broader filter including `PermissionCatalogArchitectureTests` compiled and ran 52 tests: **49 passed, 3 failed** on general catalog assertions (158 expected/160 actual, `Resource.ViewCostRate` screen-view dependency, and Admin baseline 48 expected/47 actual). These three pre-existing/mismatched assertions were not changed as part of the P0 closure.

### Development target, permissions, and EF delivery gate

The approved read-only EF inspect verified the actual Development target as SQL Server `Shady`, database `NexusPmo`, database ID **5**. The API health route itself does not check SQL connectivity. No authenticated browser/API session was available, and no settings write, role grant, seed, migration, or reset was performed.

| Required database evidence | Current result |
|---|---|
| EF model synchronized | **Yes.** |
| Development DB identity | **Verified:** `Shady` / `NexusPmo`, database ID 5. |
| Source / applied / pending migrations | **10 source, 10 applied, 0 pending.** Pending migration names: none. |
| Migration `20261101114229_AddProjectAnnualBudgetApprovalConfiguration` | **Applied.** |
| Independent backup | **Not created; not needed for the read-only inspect.** No DB write was attempted. |
| Migrations applied during this closure | **None.** |
| Physical schema | **Verified:** matches the model; no missing or extra objects reported. |
| Persisted catalog/grants | `ProjectAnnualBudget.View`: 3 role grants. `ManageApprovalConfiguration`: 0. `Approve`: 0. `ProjectApproval.Override`: 0. `SubmitForReview`: 2 role grants. |
| Direct Allow/Deny users | 0 direct Allow and 0 direct Deny rows for View, Manage, Approve, and Override. |
| SQL Server save/reload/persistence tests | **Not run.** |

The secure first-grant path for `ProjectAnnualBudget.View` and `.ManageApprovalConfiguration` is defined in source: it requires effective `Roles.ConfigurePermissions` and `TenantAdministration.ManageScope`, honors Active Deny, allows a custom role or different same-tenant user, rejects self-assignment and new system-role grants, and preserves existing role keys. The protected `tenant.admin` role and one active member have the delegation prerequisites. No Approve or Override grant exists, and this closure did not choose or grant a recipient. Fresh Admin seed no longer adds Annual Budget View/Create/EditDraft/Submit; existing grants are preserved by ordinary reseeding.

### Closure decision

Source/UI changes, targeted security tests, API build/health, and the Development EF/schema gate are verified. The requested live closure is **not complete**: no authenticated authorized user configured and saved `Sequential`, `Parallel`, or `Both`; no SQL reload or new-submission inheritance/snapshot check was made; no eligible approver exists in the current DB. Active Deny, tenant/project scope, and authorized override passed isolated automated tests, but were not exercised against live Development identities. Resume only after an explicitly approved same-tenant approver recipient is assigned under the existing permission administration process and an authenticated session is available. Do not add grants automatically or apply a migration; the database has no pending migrations.

## Historical 2026-11-03 gate attempt

The following records describe an earlier executor attempt and are retained for audit history; they do not supersede the 2026-11-04 current status above.

**Date:** Historical closure attempt  
**Historical status at the time of the 2026-11-03 note:** The prior blocked attempt was followed by a verified Development bootstrap; authenticated browser UAT was still pending. This historical result does not verify the current 2026-11-04 database target or grants.

## Source implementation review

- The canonical catalog defines `ProjectSettings.View` and `ProjectSettings.Edit` as active, assignable Project-scoped permissions grouped under `Projects > Settings`. Edit depends on View. Both have English and Arabic labels/descriptions.
- The API has exact View and Edit policies. Project workflow reads require View; writes require View and Edit, tenant/project scope, and a mutable context. Existing section View and sensitive financial approval configuration permissions remain required for protected finance operations.
- The stable `tenant.admin` seed grant is additive and keyed by immutable system-role identity. Project Settings permissions are outside the mandatory Tenant Admin baseline, so the baseline Deny archival does not remove an explicit Deny for either key. Other role grants are not changed by this supplemental grant path.
- The React Roles & Permissions screen consumes catalog grouping and localized metadata. The Header Side Sheet requires View, section content uses its own View permission, and saves require Edit plus applicable section/operation permissions. The supported `apps/web-react` project navigation has no standalone Project Settings tab; the legacy settings URL opens the Side Sheet in project context.

These are source-level findings; they do not establish persisted catalog rows, effective permissions, or HTTP outcomes.

## Development database gate

| Required evidence | Result |
|---|---|
| EF model synchronized | **Yes.** `scripts/database/ef-database-gate.ps1 model` passed offline. |
| Source migrations | **10.** `20261029155617_InitialFopxPmBaseline`; `20261029181208_ProjectPaymentRequestPersistence`; `20261029183557_AddTenantRegistry`; `20261030122718_AddProjectAnnualBudgetFoundation`; `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`; `20261031000210_AddFinancialPersistenceSprints3to6`; `20261031142036_AlignResourceProjectTeamsAndInvitations`; `20261101114229_AddProjectAnnualBudgetApprovalConfiguration`; `20261101213237_SynchronizePlanningPhysicalSchema`; `20261102190318_AddStableSystemRoleIdentity`. |
| Live target identity and DB ID | **Not verified.** No `SERVERPROPERTY('ServerName')`, `DB_NAME()`, or `DB_ID()` result was returned. |
| Live applied/pending migration count and names | **Not run.** The gate stopped before querying SQL Server. |
| Fresh backup and `RESTORE VERIFYONLY` | **No.** No backup was created or verified. |
| Migrations applied | **None.** No migration write was attempted. |
| Physical schema | **Not run.** |
| Permission rows, tenant Admin grants, and explicit Deny rows | **Not verified.** |
| Permission seed | **Not run.** |
| Persistence tests against Development | **Not run.** |

The process, user, and machine environments had no `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` or `ASPNETCORE_ENVIRONMENT` override, and no relevant user-secret connection key was configured. The Development configuration contains an integrated-auth connection setting. For the read-only gate attempt, its value was loaded into the gate process environment only; it was not printed or persisted. `inspect --environment Development --expected-server Shady --expected-database NexusPmo` failed before identity/history queries with SQL TCP provider **“Access is denied.”** A read-only `sqlcmd` diagnostic to `tcp:localhost,1433` independently failed with **“An attempt was made to access a socket in a way forbidden by its access permissions”** followed by login timeout. This is an executor socket-access restriction; it is not evidence that the SQL target or credentials are valid or invalid.

Only C: and a Temp alias on C: were visible to the executor. The SQL target, database file locations, and backup destination could not be reverified in that execution. No backup, seed, migration, reset, or other database write was attempted. The unavailable backup destination is historical evidence and does not block Development/UAT operations under the current policy.

## Runtime and regression verification

| Check | Result |
|---|---|
| React regression tests | The previous implementation turn recorded 18 focused settings/navigation/Side Sheet tests passing. They were not rerun in this closure attempt. |
| React production build | The previous implementation turn recorded a successful Vite production build. It was not rerun in this closure attempt. |
| Focused API regression tests | **No test results.** `dotnet test .\apps\api\tests\NEXUS.Api.Tests\NEXUS.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ProjectSettingsAuthorizationTests --verbosity minimal -m:1` compiled dependencies through Persistence, then spent about five minutes rebuilding `NEXUS.Modules.ProjectPlanning.MppAdapter` with IKVM warnings and did not reach test compilation/execution. The exact test process was stopped. |
| Running API / Roles & Permissions catalog response | **Not verified.** |
| Admin, View-only, Edit, No Permission, Explicit Deny HTTP scenarios | **Not run.** No authenticated runtime responses or no-mutation checks were obtained. |
| Project Header Side Sheet browser runtime | **Not run.** Source review confirms the intended gates; there is no runtime browser evidence from this closure attempt. |

## Earlier blocker and next safe step (historical)

The configured Development connection could be supplied to the gate without exposing a secret, but this executor could not open the local SQL TCP socket. Run the read-only EF gate from a host-connected PowerShell process that can reach SQL Server and verify the exact `Shady` / `NexusPmo` identity, migration state, and physical schema before separately authorized work. A backup may be created when explicitly requested, but it is not a Development/UAT prerequisite. Then run only the approved identity seed and verify the exact catalog rows, additive `tenant.admin` grants, unrelated-role preservation, and explicit Deny behavior. Follow with authenticated API and Side Sheet UAT for all five requested actor scenarios. Do not report closure until those persisted and runtime checks pass.

**Policy supersession note (2026-11-10):** This report's earlier backup prerequisite applied under the policy in force during the recorded task. Development/UAT backups are now optional for standalone seed, reconciliation, migration, and other authorized operations; missing backup media, independent storage, or `RESTORE VERIFYONLY` does not block them. Destructive reset still requires explicit Product Owner authorization and all exact target, migration, and schema gates.

## Skills applied

Read and applied the repository security-audit and frontend-data-integrity skills, and followed the documented EF Core database workflow; the global `secure-coding-standards` and security verification checklist; `efcore-patterns`; `entity-framework-core`; and `universal-software-engineering` validation guidance. No dedicated testing skill was found in the installed project/global skill directories.
