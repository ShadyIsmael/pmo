# Permission Architecture Implementation Report

**Backup-policy supersession (2026-11-10):** The backup statuses and blockers in the dated execution records below are historical. Development/UAT backups are optional for migrations and other authorized changes; missing backup media, independent storage, or `RESTORE VERIFYONLY` is not a blocker. Exact target, migration, schema, and separate authorization gates remain required. A destructive reset still requires explicit Product Owner authorization. Production backup and recovery safeguards remain mandatory.

## Canonical permission rebuild continuation - 2026-11-02

The current source catalog contains 154 active canonical keys (39 View, 115 Action). Source comparison found no duplicate keys or seed IDs; the protected tenant Admin baseline is dynamically derived and currently contains 48 keys. The React production build and full frontend suite pass. The offline EF gate reports 10 source migrations and a synchronized model. These source and build results do not verify the live Development database or runtime API authorization.

Issue reads require `Issue.View` and fail closed without assigned project scope. Plugin reads require tenant-scoped `Plugin.View`. Project/AI Planning/Audit/Work Item/Integrations/Plugin route guards use canonical View keys. Optional project-setup budget creation uses `ProjectAnnualBudget.Create`. Status report export requires `StatusReport.Export` at a server endpoint and is limited to Published reports. Directory connection writes use the dedicated `Directory.ManageConnection` action with bilingual labels.

Earlier reports described an opt-in `--rebuild-canonical-permissions` row-reconciliation operation. The then-current task requested a verified Development database recreation through the repository EF gate followed by the full application seed, but the operation was not performed. The current executor could not authenticate to SQL Server or verify the target/schema, so no database operation was performed. That historical request does not authorize or resume a reset in the current backup-policy task.

**Current verification snapshot (2026-11-02):** React: 765/765 tests across 141 files; TypeScript and production build passed. API solution build passed with 352 IKVM/MPP warnings and 0 errors. Offline EF model gate passed with 10 source migrations and no database connection. SQL identity, backup, live migration history, physical schema, database reset, seed, seed repeatability, backend tests, and runtime grant/deny scenarios remain unverified. See `permission-audit/FINAL-PERMISSIONS-CLOSURE-REPORT.md`, `SECURITY-TEST-RESULTS.md`, and `DATABASE-MIGRATION-SEED-REPORT.md` for the current closure state.

Current-turn build and focused test results are recorded after execution below. Browser UAT, persisted grants, physical schema, and runtime effective permissions remain unverified.

The following implementation and execution records are historical snapshots from before this canonical rebuild. Their catalog counts and database results must not be treated as current evidence.

## Historical implementation snapshot before canonical rebuild

**Historical continuation status (2026-11-02):** Reference Data granular authorization and Project Finance View/Manage permissions were integrated in source and the role editor. That snapshot reported 154 definitions, 136 active keys (34 View, 102 Action), 18 inactive compatibility definitions, and a 46-key source-derived protected tenant Admin baseline. These counts predate the canonical rebuild. Its earlier API/UI test results and database checks are historical and do not verify current source or the current 48-key baseline. Browser runtime UAT remains open.

## Executive summary

Implemented the code-defined permission catalog and authorization paths in source. The current catalog contains **154 definitions**: **136 active** (34 View and 102 Action) and **18 inactive compatibility definitions**. Compared with the 103-key Phase 01 baseline, 86 baseline keys remain active, 17 are inactive, and 50 active keys are additions. The prior 2026-11-02 Admin reconciliation covered the earlier 43-key baseline only; this continuation did not write the database or reconcile the expanded baseline.

At the earlier 143-definition snapshot, the Phase 02 proposal of 133 transition entries (116 intended active after 17 retirements) reconciled to **10 new active keys**. The proposed `Roles.Manage` entry was reclassified as inactive/deprecated and replaced by five separately controlled role-screen capabilities; this was the additional 18th inactive catalog entry. The net active change at that snapshot was +9 (116 − 1 + 10 = 125). The earlier additions and their source-backed rationale are listed below; this continuation adds 11 more active keys.

This continuation adds three tenant Reference Data operations and eight Finance workspace permissions. Reference Data Create/Edit/Delete require View, use per-operation API and UI checks, retain tenant/platform and referenced-value safeguards, and extend the dynamic protected Admin baseline. Finance parent keys control the shell; each child workspace has independent View/Manage, matching View dependencies, and separately authorized sensitive actions. The existing business rule remains enforced: **the Project lifetime financial ceiling can change only on final approval of an approved Change Request containing that change**.

## Implemented source areas

- **Catalog and editor:** code-defined Module/Screen/Level metadata, bilingual labels/descriptions, sensitivity, dependencies, legacy status/mappings, effective dependency filtering, and hierarchical Roles & Permissions editor.
- **Role security:** granular View/Create/EditDetails/ConfigurePermissions/Delete checks; remove `users.write` as role mutation authority; enforce actor delegation, tenant/role ownership, dependencies, and protected-role rules. Sensitive workflow adapters and the legacy CR service no longer use `IsInRole(...)` permission bypasses.
- **User administration:** separated account profile management, role assignment, direct permission overrides, password reset, and account activation/deactivation. `Users.SetStatus` is required for status changes, legacy `users.write` cannot confer status authority, and status changes are audited. Role-only and status-only administrators cannot change profile fields through the shared update endpoint.
- **Permission seeding:** new protected Admin roles receive the active tenant Administration baseline plus the explicit approved Portfolio/Program/Handover operations. Existing roles are not changed by ordinary startup seeding. The opt-in Development reconciliation only adds missing mandatory Admin grants, soft-deletes conflicting mandatory Denies, and audits the change; unrelated grants and memberships are preserved. `Program.CreateAcrossPortfolios` and sensitive financial approvals remain excluded.
- **Management scope:** explicit tenant-scoped administrative authority and assignment-derived scope; no hardcoded `admin`/`pmo.admin` role-name bypass; Organization IDs are modeled independently.
- **Program creation:** `Program.CreateAcrossPortfolios` is registered and configurable; normal creation follows native Portfolio assignment; exceptional creation requires the explicit key, `Program.Create`, Portfolio View, and eligible Tenant/Organization scope. GET target choices and POST creation use aligned eligibility rules.
- **Financial separation:** Annual Budget, Payment Request, Settlement/Reversal, and Fiscal Year Closing capabilities remain separate. `finances.write` is not an approval/settlement/reversal/closing/override/ceiling authority.
- **Project Settings:** persistent Annual Budget approval defaults and financial approval mode are presented at `/projects/{id}/settings`; the route uses `ProjectAnnualBudget.View` with the retained `finances.read/write` view compatibility, and saving requires `ProjectAnnualBudget.ManageApprovalConfiguration` plus writable project scope. The settings editor is no longer embedded in Payment Requests.
- **Financial activity:** the financial audit panel uses `ProjectFinancialAuditTrail.View` with the retained legacy Finance compatibility policy. Generic `Audit.View` alone does not expose the financial detail endpoint.
- **Legacy Budget Revisions:** added `BudgetRevision.View` and `BudgetRevision.Approve`; approve/reject APIs and custom workflow transitions into Approved/Rejected require the dedicated action. The service also applies management scope, status, row-version, maker/checker, and allocation checks. The legacy entity has no per-reviewer assignment field, so this permission is not described as assignment-bound.
- **Project Edit UI:** the shared Create/Edit form now exposes the lifetime ceiling only during Project creation. Edit displays the current value as read-only and omits `budget` from its update payload; backend validation independently rejects a changed ceiling.
- **Reviewer access:** added narrow single-record assigned-review paths for Annual Budgets, Payment Requests, and Payment Reversals; normal list/history APIs remain separately permission/scope gated.
- **Reports and dashboards:** Reports read policy is applied to its API and route; `Dashboard.View` is catalogued and applied to Home route, navigation, and API while widget checks remain independent.
- **Localization and machine matrix:** catalog Arabic source strings were verified as UTF-8. The final CSV was reconciled with the catalog and current endpoint evidence and contains no replacement characters.
- **Regression tests:** catalog/dependency, Project Settings, role grant, and direct-override regression tests are present. Ten focused React tests were previously reported as passing before the last JSX correction; this recheck did not run TypeScript, the production build, or React tests because the Application Insights egress remains unresolved. Backend security tests were not run because the full API build failed in the MPP adapter.

See [canonical API/route mapping](PERMISSIONS-CANONICAL-API-MAPPING.md), [final catalog](PERMISSIONS-FINAL-CATALOG.md), [role delegation policy](PERMISSIONS-ROLE-DELEGATION.md), and [owner UAT checklist](PERMISSIONS-OWNER-UAT.md).

## Inventory and disposition

| Measure | Result |
|---|---:|
| Phase 01 source-seeded baseline | 103 |
| Phase 02 proposed transition entries | 133 |
| Phase 02 proposed active target after 17 retirements | 116 |
| Code catalog definitions including inactive compatibility entries | 154 |
| Active View keys | 34 |
| Active Action keys | 102 |
| Active keys | 136 |
| Retained inactive/deprecated keys | 18 |
| Baseline keys remaining active | 86 |
| Baseline keys now inactive | 17 |
| New active keys versus baseline | 50 |
| Persisted runtime grants inspected | No |
| Permission rows or role grants changed | No |

No current keys were deleted. Inactive compatibility keys remain source-defined for historical references and are excluded from new role assignment. Broad legacy grants were not fanned out into sensitive child operations.

### Phase 02 to implementation delta

The 133 Phase 02 transition entries reconcile to the final catalog as follows: **133 + 10 newly registered active keys = 143 definitions**. The planned 17 inactive entries remain, and `Roles.Manage` is additionally inactive, resulting in 18 inactive entries. Of the Phase 02 intended 116 active entries, `Roles.Manage` is no longer active and the ten new entries are active, giving 125 active keys.

| Additional active key | Justification from implemented operations |
|---|---|
| `Roles.View` | Dedicated view access for the Roles & Permissions workspace. |
| `Roles.Create` | Separates role creation from editing role details and assigning permissions. |
| `Roles.EditDetails` | Restricts name/description/category edits independently of permission grants. |
| `Roles.ConfigurePermissions` | Independently gates grant changes and is enforced with delegation boundaries. |
| `Roles.Delete` | Separately gates deletion of eligible unused tenant roles. |
| `ReferenceData.View` | Adds a Level 1 view capability for the tenant reference-data screen. |
| `TenantAdministration.ManageScope` | Provides explicit tenant-administration authority without role-name-based global scope. |
| `BudgetRevision.View` | Separates access to the implemented Budget Revisions workspace from general financial access. |
| `BudgetRevision.Approve` | Separates budget-revision decisions from general financial editing and checks maker/checker constraints. |
| `Users.SetStatus` | Separates account activation/deactivation from profile editing and role assignment. |

`Roles.Manage` remains catalogued as inactive for compatibility/audit references, with no automatic grant mapping. No broad legacy grant is expanded into the new sensitive permissions. Persisted runtime grants remain unverified.

### Current continuation catalog additions

The continuation registers `ProjectFinance.View`, `ProjectFinance.Manage`, `ProjectAnnualBudget.Manage`, `ProjectPaymentRequest.Manage`, `ProjectSettlement.View`, `ProjectSettlement.Manage`, `ProjectAnnualBudgetClosing.Manage`, and `ProjectFinancialAuditTrail.View`, plus `ReferenceData.Create`, `ReferenceData.Edit`, and `ReferenceData.Delete`. Existing View keys for Annual Budgets, Payment Requests, and Fiscal Year Closing and `ReferenceData.View` are reused. No import/export key was added because no supported operation was identified. Manage-to-operation boundaries and dependencies are recorded in [the Project Finance workspace mapping](../modules/PROJECT-FINANCE-WORKSPACE.md) and [canonical API mapping](PERMISSIONS-CANONICAL-API-MAPPING.md).

## Financial ceiling rule

Source paths reviewed:

- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs`: Project Edit rejects a changed `Budget` value.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestService.cs`: legacy approval service rejects ceiling-changing CR approval; it no longer mutates Project ceiling.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectChangeRequestWorkflowService.cs`: only final workflow completion of an assigned CR with a ceiling delta applies the ceiling mutation. It requires `ChangeRequest.Approve` and `ProjectFinancialCeiling.ApproveChange`, verifies the old ceiling is still current, validates finance state, and writes an audit event transactionally.

Project creation still permits initial ceiling configuration. Creating/submitting a CR only records a proposed change; it does not apply it.

## Persistence and documented database workflow

| Required status | Result |
|---|---|
| Permission catalog metadata schema change | None; metadata is code/DTO-backed. |
| EF model/snapshot synchronization | No schema change intended; full EF synchronization checks **Not run**. |
| Pending migration count/names | **Not inspected**. |
| Development database identity | **Not verified**. |
| Independent backup verification | **Not verified**. |
| Migrations created/applied | None created or applied. |
| Physical Development schema | **Not inspected**. |
| Persisted Roles/RolePermissions/UserRoles/direct overrides | **Not inspected**. |
| Production data | Not accessed. |

There was no authorized, verified Development database identity/backup gate in this execution context. Therefore no database connection, role/grant comparison, migration, or data write was made. Persisted grants and their compatibility impact remain unknown.

## Verification status

| Check | Result |
|---|---|
| Static catalog parse | Current source count: 154 definitions; 154 unique keys and seed IDs; 136 active (34 View / 102 Action). |
| Catalog Arabic UTF-8 and replacement-character scan | The current API architecture test confirms every active definition has EN/AR labels and descriptions with no U+FFFD. |
| Final CSV static validation | Current matrix: 154 rows, 154 unique canonical keys, 136 active rows; exact key set matches catalog. |
| Active localization metadata | All active catalog definitions have EN/AR labels and descriptions; full locale JSON parses successfully. |
| Role-name bypass source scan | Zero `IsInRole(...)` references remain in API C# source after replacing the CR, Finance, and Risk workflow shortcuts with permission checks. |
| Catalog dependency consistency | Static review confirms declared dependencies exist; no active View/Action dependency gaps except documented reviewer-only workflow exceptions and the ceiling qualifier. |
| API build | `NEXUS.Api` and test assemblies compiled during the current focused `dotnet test` runs; no separate API build command was run. |
| React type check | `npm run build` advanced through `tsc -b` and reached Vite; the overall build did not complete. |
| React production build | **Not completed** in this continuation; the attempted Vite build was interrupted after it stopped making progress. |
| Focused frontend tests | **Passed:** Reference Data and Project Finance UI tests, 30/30. |
| Backend security/domain tests | **Passed:** API catalog/Admin protection 16/16; Admin seed/reconciliation 10/10; Reference Data category and Project Role services 14/14. |
| Browser UAT/runtime authorization | **Not run.** No runtime claims are made. |
| EF synchronization/persistence tests | **Not run.** No schema change was intended and DB was not inspected. |

Source integration and the focused tests above are verified. Current SQL Server state and browser runtime authorization remain unverified. Historical build failures are retained in their dated execution records below.

## Outstanding gates and risks

1. Complete and capture a React production build; the current attempt did not finish.
2. Reconnect to the verified Development SQL Server, establish a fresh verified backup, inspect current migrations and grants, then reconcile the protected Admin only if the read-only comparison shows the current baseline is missing.
3. Complete browser runtime UAT for protected Admin locking, Reference Data scope/integrity, independent Finance workspaces and sensitive actions, assigned reviewers, EN/AR, RTL, and responsive layouts.

**Delivery assessment:** P0 source implementation and documentation are present, but the task's “integration verified / owner UAT ready” completion criteria cannot be claimed until the checks above run successfully in the required environment and persisted Development authorization state is reviewed.

## Execution recheck — 2026-11-01

### Permission source changes and static evidence

- Static source inventory matched the final target matrix: **143 unique keys**, **125 active** (31 View and 94 Action), and **18 inactive**. EN/AR JSON parsed successfully; the catalog, matrix, and locale scan found no U+FFFD replacement characters. This is source evidence, not proof of persisted database synchronization.
- Effective permission resolution now excludes inactive and uncatalogued keys while retaining their persisted rows. Role create/update rejects new inactive or uncatalogued grants. Existing inactive role grants are retained during role edits, and actors can inspect/edit a role without those inactive rows granting authority.
- Direct user override updates reject new or changed inactive/uncatalogued overrides. Unchanged persisted inactive overrides survive updates to other active overrides. The API now returns `isActive`; the direct override UI disables inactive rows.
- Focused backend regression cases were added/updated for inactive effective permissions, role grants, and direct overrides. They were **not executed** because the SDK restore/build gate failed before compilation. React fixtures were aligned to the new `isActive` contract; no React tests were run.
- Source inspection found no `IsInRole(...)` calls or literal `pmo.admin` authorization bypass in API source. Financial approval policies use their dedicated permission keys; `finances.write` alone is not a settlement or approval permission. Program scope source checks native Portfolio assignment or explicit `Program.CreateAcrossPortfolios` with tenant/organization eligibility. These remain static findings pending executed regression tests and runtime UAT.

### SDK and build

- `global.json` pins SDK `10.0.302`; `where.exe dotnet` resolves only `C:\Program Files\dotnet\dotnet.exe`, and `dotnet --list-sdks` reports only `10.0.302`.
- The installed SDK is incomplete: `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` resolver SDK directories are absent under `C:\Program Files\dotnet\sdk\10.0.302\Sdks`. `dotnet --info` and `dotnet workload list` also encounter a Windows Service Control Manager access denial. No Visual Studio/MSBuild installation was found in the execution environment.
- A full API restore failed with MSB4276 while resolving those missing SDK resolver paths. Compilation was not reached; zero compiler errors is not a successful build. The exact-version official installer download attempt failed at DNS resolution. No system files, PATH entries, SDK selection, or target frameworks were modified.
- Microsoft documents `/repair` as the supported Windows Installer option to restore damaged .NET installation components. A machine administrator should download the official x64 .NET SDK 10.0.302 installer, verify its published SHA-512, and run from an elevated terminal: `./dotnet-sdk-10.0.302-win-x64.exe /repair`. If repair cannot restore the SDK, reinstall the same pinned SDK from the official .NET downloads page. See [SDK troubleshooting](../development/dotnet-sdk-troubleshooting.md).

### Telemetry and React verification

- The earlier automated-review record attributes the blocked egress to a TypeScript process attempting Application Insights telemetry. This execution did not repeat the React commands. Process ID, executable command line, exact destination/provider, payload contents, and transmitted data categories are **not available** from the restricted environment; no claim is made about the specific data that could have left the process.
- Repository source, manifest, lockfile, and checked environment names contain no Application Insights SDK/endpoint configuration and no approved offline/local-test telemetry setting was found. `DOTNET_CLI_TELEMETRY_OPTOUT=1` was used for .NET CLI commands only; it does not disable Application Insights SDK telemetry.
- Typecheck, production build, focused permission tests, Roles & Permissions tests, and React EN/AR test validation are **Not run** pending an approved telemetry configuration or the security owner’s review of process and egress evidence. The `OrganizationPage.tsx` JSX correction has not been verified by a successful post-correction compiler run.

### Database readiness and runtime authorization

| Required evidence | 2026-11-01 result |
|---|---|
| EF model synchronized | **Yes** — repository offline model gate passed. |
| Source migration count/names | **8**: `20261029155617_InitialFopxPmBaseline`; `20261029181208_ProjectPaymentRequestPersistence`; `20261029183557_AddTenantRegistry`; `20261030122718_AddProjectAnnualBudgetFoundation`; `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`; `20261031000210_AddFinancialPersistenceSprints3to6`; `20261031142036_AlignResourceProjectTeamsAndInvitations`; `20261101114229_AddProjectAnnualBudgetApprovalConfiguration`. |
| Pending migration count/names on current Development target | **Unknown** — no SQL connection/query completed in this restricted execution. |
| Development DB identity verified | **No** — prior host-connected 2026-10-31 evidence is historical and was not re-established in this execution. |
| Independent backup verified for any write | **No** — no current backup verification was performed. |
| Migrations applied | **None by this execution.** |
| Physical schema verified | **No** — no current target query completed. |
| Persisted permission/role/user/direct-grant comparison | **Not run.** |
| Persistence tests | **Not run.** |
| Runtime UI/API authorization UAT | **Not run.** Designated Development identities and host runtime were unavailable. |
| Blockers | Host SQL Server access, current target identity/history, and completed build/test gates. Backup status is informational under current Development/UAT policy. |

No database records were modified. No role/grant reset, migration apply, or deployment occurred. Owner UAT remains entirely open; the existing checklist is intentionally unchecked.

## Current continuation recheck - 2026-11-01

This section supersedes the earlier SDK/build and telemetry summaries above where they conflict.

### Build and SDK evidence

- global.json selects SDK 10.0.302; where.exe dotnet resolves C:/Program Files/dotnet/dotnet.exe, and dotnet --list-sdks reports only 10.0.302. The runtime list includes ASP.NET Core/.NET 8.0.31 and 10.0.10.
- The pinned SDK is incomplete: Microsoft.NET.Sdk.WorkloadAutoImportPropsLocator and Microsoft.NET.Sdk.WorkloadManifestTargetsLocator are absent under C:/Program Files/dotnet/sdk/10.0.302/Sdks. The workload resolver directory contains only its XML manifest. dotnet --info and dotnet workload list encounter a Windows Service Control Manager access denial. vswhere found SQL Server Management Studio 22.8.1 only; no Visual Studio/MSBuild executable was found.
- dotnet restore apps/api/src/NEXUS.Api/NEXUS.Api.csproj exited 1. Diagnostic output showed _GetAllRestoreProjectPathItems and _GenerateRestoreProjectPathWalk failing but surfaced zero MSBuild/NuGet error codes. An earlier attempt recorded MSB4276 for the absent resolver SDK paths; that exact diagnostic was not reproduced in this recheck.
- dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1 --verbosity minimal exited 1. Project references compiled through NEXUS.Persistence; the first actual error was MSB4018 in NEXUS.Modules.ProjectPlanning.MppAdapter. IKVM could not open obj/Debug/net10.0/ikvm/stage/1/1ef14f78975de40d85569d4de24e8418/jakarta.activation.dll.log because another process held it. No dotnet, MSBuild, or Java process was visible immediately after failure, so the lock owner is unknown. The full API build did not pass.
- No SDK repair was performed: the official installer download previously failed at DNS resolution and this environment cannot repair the system installation. No system files, PATH entries, SDK selection, or target frameworks were modified. A machine administrator must repair the existing installation and identify the transient IKVM file-lock owner before the full build is retried.
- The exact host procedure is in docs/development/dotnet-sdk-troubleshooting.md. Microsoft documents /repair as the supported Windows Installer operation for restoring damaged .NET components.

### Telemetry, React, and authorization tests

- Earlier automated-review evidence attributes the blocked egress to a TypeScript process attempting Application Insights telemetry. The process ID, exact executable/command line, destination, payload, and transmitted data categories are unavailable. Available evidence does not establish whether it belonged to the application, test/development tooling, or the Agent environment.
- Repository source, frontend manifest/lockfile, API configuration, and checked environment names contain no Application Insights SDK/endpoint configuration; no approved offline/local-test telemetry setting was found. DOTNET_CLI_TELEMETRY_OPTOUT=1 was used for .NET CLI commands only; it does not disable Application Insights SDK telemetry. No React build or test process was started in this recheck.
- Backend permission regression tests are present in source but were not run because the full API build failed in the MPP adapter. Typecheck, production build, focused permission tests, Roles & Permissions tests, and RTL runtime UAT remain open.

### Database and runtime

- The offline EF model gate passed: model synchronized = Yes; source migrations = 8 (names listed in the Database readiness table above).
- NEXUS_EF_DEVELOPMENT_CONNECTION_STRING is not configured and ASPNETCORE_ENVIRONMENT is unset in this execution. The Development database identity, migration history/pending count, independent backup, and physical schema remain unverified. The checked-in localhost connection value is not proof of the actual database target.
- No migrations or database records were changed. Persisted permission/role/user/direct-override comparisons and persistence tests were not run. Runtime authorization UAT remains open.

## P0 Admin Role Permission Access Regression — 2026-11-01

### Root cause and runtime evidence

- **Root cause confirmed from source and live role detail:** before this rebuild, `RoleAdministrationService.GetActorPermissionsAsync` used `users.write` as its blanket gate for role administration. The current canonical service requires role-specific capabilities instead. `Roles.Manage` is inactive and `PermissionCatalog.EnforceDependencies` removes that grant from effective permissions. `SeedDataProvider` adds grants only when creating a role and deliberately leaves existing `RolePermissions` unchanged. The live Admin role detail now confirms it has no `Roles.View`, `Roles.Create`, `Roles.EditDetails`, or `Roles.ConfigurePermissions` grants; the UI consequently disables both requested controls. This confirms the persisted role-config gap through the application response. Direct SQL rows and `UserOverrides` are still unread, so override interactions remain unverified.
- The expected Admin capability is full use of the tenant role editor. An authorized `Roles.ConfigurePermissions` holder can grant ordinary non-Administration permissions to user-managed custom roles without already holding each permission; Administration-module permissions remain actor-authorized only. This fix does not turn the Admin into a Platform Administrator or silently add high-impact business permissions.
- **`users.write` compatibility:** the previous service did use `users.write` as a broad role-administration gate; the new service intentionally does not treat it as a canonical permission-editor grant. Existing roles need an explicit, reviewed grant reconciliation rather than a runtime `users.write` bypass. `users.write` alone cannot open or update the editor under the current model.
- **Trusted authority model:** `ManagementScopeResolver.IsTenantAdministratorAsync` requires an active user whose `User.TenantId` matches the requested Tenant and whose freshly resolved effective permissions contain `TenantAdministration.ManageScope`. The catalog marks that key Critical and tenant-only, with `settings.read` as a required dependency. No `admin`/`pmo.admin` name check is used.
- **Historical source fix (actor-possession policy superseded 2026-11-09):** at that snapshot, effective `TenantAdministration.ManageScope` opened the tenant role workspace and permitted role permission updates, with each requested grant checked against actor permissions and catalog dependencies. Current role operations require their exact `Roles.*` capabilities; tenant scope alone does not authorize them. The current policy below supersedes that possession ceiling for ordinary non-Administration custom-role grants. Role changes remain audited, and last-effective-administrator safeguards remain.
- **Session/cache finding:** API authorization claims are discarded and rebuilt from the current database grants on every authenticated request; no server permission cache was found in this path. React loads `/api/auth/me` into its current-user context and can retain that snapshot during a session. The role-save flow now reloads the current user and invalidates the permission-catalog query, so a permitted grant change updates the current UI.
- **Observed UI/API state:** the open React app is signed in as Alex Admin (`admin@nexus.com`), assigned role `admin`, in the FOPX Development Demo Tenant. The live Edge accessibility snapshot at `/administration/access/roles` shows **New role disabled** and **Edit disabled**. The Admin role detail screen displays 95 permissions and one assigned user; under Administration it lists Audit, Directory, Tenant settings, and Users/access, with no Roles & Permissions grants. This confirms the existing Admin role lacks the canonical `Roles.View`, `Roles.Create`, `Roles.EditDetails`, and `Roles.ConfigurePermissions` grants needed by the requested controls. `/api/auth/me` permission keys and direct `UserOverrides` remain unread, so override effects are not ruled out. The previously observed `GET /api/settings/access` response is `{ canReadTenant: true, canWriteTenant: false, canReadPlatform: false, canWritePlatform: false }`; this is Settings evidence only and does not establish Role permission grants.
- **Frontend gate regression found and fixed:** before this follow-up, the Access & Security landing card required explicit role keys or tenant scope, but the route guard and `RolesPage` also allowed `users.read` as view access. A `users.read` viewer could therefore reach the Users directory but see no Roles & Permissions card. `AccessPage` now includes `users.read` for the role workspace entry, and a focused React regression case was added. This restores navigation only. Role data access and mutations still require their exact `Roles.*` capabilities; `TenantAdministration.ManageScope` is an additional prerequisite only for the separate bootstrap delegation policy.

### Controlled recovery and verification status

No grant reconciliation was executed. The environment variables were absent at the start of this execution. A read-only repository database-gate inspection was then attempted using the Development connection value from `appsettings.Development.json` and the previously recorded expected identity `Shady` / `NexusPmo`; the gate built successfully but could not connect (SQL Server Named Pipes provider error 40). A follow-up local check found no `MSSQLSERVER` service and no listener on local TCP 1433 in the workspace shell. The current Development target identity, Tenant Administrator, role rows, effective grants, direct overrides, and independent backup therefore remain unverified. The configured `localhost` value is not evidence of the actual database identity. This is why the source navigation fix did not unlock the buttons in the running account: navigation is view-gated, while the actions require persisted canonical management grants.

Before any persisted grant change, use the database gate to verify the actual Development target and capture a read-only inventory of the active Admin user's Tenant, role assignments, `Permission` rows, `RolePermissions`, `UserOverrides`, and resulting effective permissions. A backup may be created when explicitly requested, but it is not a prerequisite. If the verified before-state confirms the assigned Admin role is missing the canonical grants needed by the requested controls, the narrow recovery candidate is to add only `Roles.View`, `Roles.Create`, `Roles.EditDetails`, and `Roles.ConfigurePermissions` to that verified tenant role; these are the exact source gates for view, add, edit, and permission assignment. Preserve every existing role grant and user override. `Roles.Delete` is not part of this recovery unless separately authorized. Do not add `TenantAdministration.ManageScope`, `Roles.Manage`, `users.write`, or Platform permissions as a shortcut. Apply through a verified Tenant Administrator's normal audited path when possible. If no such actor exists, stop and use the organization's approved, tenant-bounded break-glass process with independent approval and an audit record; the locked-out account must not grant itself access. The previous actor-possession ceiling described in this recovery note is superseded for ordinary non-Administration grants to user-managed custom roles; exact role capabilities, dependencies, scope, Active Deny, protected-role controls, and no-self-escalation still apply.

Focused backend and React regression tests were added for tenant-scope editing, save/reload, the delegation ceiling, role operation separation, direct-deny revocation, and frontend refresh behavior. The focused API test command built referenced modules, including `NEXUS.Modules.Identity` and `NEXUS.Modules.ProjectPlanning.MppAdapter`, but failed before test discovery while copying referenced assemblies to `NEXUS.Api/bin/Debug/net10.0`: MSB3026 reported that multiple DLLs were in use by another process. The process identity was not established, and it was left running. A second attempt with `BuildProjectReferences=false` still entered IKVM processing; it was stopped before test discovery. Thus the new API assertions were **not run**. After the landing-card and test-fixture fixes, the focused React check `vitest run src/test/access-security-pages.test.tsx -t 'Tenant Administration scope configure role permissions|opens both new-role and edit-role|explains why Financials Read|users.read viewers|Tenant Administrator|users.write-only' --pool=forks --poolOptions.forks.singleFork` passed (6 passed, 16 skipped). Running the whole `access-security-pages.test.tsx` file reported 15 passed and 7 failed. Remaining failures cover user role/status/profile controls and direct-override labels/payloads; the file is not green and those assertions need triage. React typecheck and production build remain unverified; the previous broader build attempt remains blocked pending review of the observed Application Insights egress. EN/AR message JSON parses successfully, and the new Arabic message contains Arabic script without question-mark replacement. No full build, runtime Role Permissions UAT, or successful role-grant HTTP update is claimed for this P0 fix.

### Current read-only Development gate attempt - 2026-11-01

- The repository gate tool built successfully with 0 warnings and 0 errors.
- Read-only `inspect` was attempted against the Development connection value in `appsettings.Development.json`, with expected identity set to the previously recorded `Shady` / `NexusPmo` values. SQL Server connection failed with Named Pipes provider error 40 before the gate could verify server identity or read migration history.
- A separate local check found no `MSSQLSERVER` Windows service and no TCP 1433 listener in the workspace shell; this shell cannot reach the SQL Server used by the live browser session.
- No role, permission, assignment, override, or audit rows were read. No database write was attempted. The current database identity, backup verification, and recovery actor remain blockers.

### Seed reconciliation and database reset follow-up - 2026-11-01

- **Seed root cause and fix:** the existing seed granted role permissions only when creating a role, so the rebuilt catalog did not repair the persisted seeded Admin role. New Admin roles now use an explicit reviewed permission snapshot that includes `Roles.View`, `Roles.Create`, `Roles.EditDetails`, and `Roles.ConfigurePermissions`; `Roles.Delete` is excluded from this recovery, and future catalog additions do not flow into Admin automatically. The existing Admin seed grants are otherwise preserved in the snapshot, including the established tenant-scoped authority; no Platform or cross-tenant grant was added.
- **Existing-role compatibility path:** `SeedDataProvider` adds only the four canonical role-workspace grants when all checks pass: environment is Development, `NEXUS_RECONCILE_SEEDED_ADMIN_ROLE_PERMISSIONS=true` is explicitly set, the target is the deterministic system Admin role in the seeded tenant, its only non-deleted member is the deterministic active seeded Admin user, and the persisted role already carries an un-denied legacy `users.write` or `Roles.Manage` grant. It never removes role grants or changes user overrides. If any precondition fails, the reconciliation does nothing. A successful add records a tenant-scoped `AuditEntries` event in the same `SaveChanges` operation. Enable the switch only after verifying the Development environment and database identity and required migration/schema readiness; a backup is optional.
- **No role-name bypass or self-escalation shortcut:** this historical seed reconciliation path targeted the seeded role by its fixed identifier and tenant, not by a role name. It did not grant `Roles.Delete`, `TenantAdministration.ManageScope`, `Roles.Manage`, `users.write`, Platform authority, or any new catalog key. That point-in-time seed operation is separate from the current role-grant API policy; current ordinary non-Administration custom-role grants are not capped by actor possession. Dependency validation, tenant checks, audit, and Direct Deny precedence remain.
- **Source verification:** `NEXUS.Persistence` built successfully with 0 warnings and 0 errors. The seed test project compiled, including tests for the four new-role grants, add-only reconciliation, preserved overrides/effective Deny, audit entry, and refusal when the legacy authority is denied. The tests did not execute: VSTest failed to connect to its testhost after 90 seconds; a single-test retry failed after 180 seconds. No passing test run is claimed. The live application was not restarted, so the observed Edit and New role buttons remain disabled in that running session pending database reconciliation and a fresh session.
- **Database/reset gate:** the user requested a database reset, but it was not executed. The Development SQL Server target could not be reached (Named Pipes provider error 40; no local SQL Server service or TCP 1433 listener), so the actual database identity, migration history/pending names, and independent backup cannot be verified. The repository EF delivery gate also prohibits dropping/recreating the database or resetting migration history. No reset, permission grant write, migration, or SQL data change occurred.

**Historical policy note (2026-11-04; superseded by the 2026-11-09 policy update):** The status above records the earlier policy and unavailable host connection for that task. At that time, only an explicitly Product Owner-authorized reset of Development `Shady/NexusPmo` was exempt from backup after exact target verification; it still required every EF migration and the full deterministic seed. The earlier execution was not retroactively authorized. Under the current policy, backups are optional for Development/UAT operations; exact target, migration, and schema checks remain required, and destructive reset still requires explicit Product Owner authorization.

| Required database completion evidence | Status |
|---|---|
| Model synchronized | Yes for the repository model; the offline EF model gate passed, and this seed-only fix adds no persisted model/schema object |
| Pending migration count and names | Not run; Development database was unreachable |
| Development database identity verified | No |
| Independent backup verified | No |
| Migrations applied | No |
| Physical schema verified | Not run |
| SQL persistence tests | Not run; focused InMemory seed tests did not start because testhost connection failed |
| Database reset | Not run; target verification is a blocker; backup status is informational |
| Blockers | Host-connected Development inspection is required before any database write or reset |

### Database update and seed request follow-up - 2026-11-01

- The user authorized updating the Development database and seeding data, then confirmed that `NexusPmo` is actually absent. The supplied API log reports SQL Server error 4060 while opening it for integrated-auth login `SHADY\shady`. This is consistent with the confirmed missing database; EF's transient-failure wording does not establish a transient outage, so retry configuration is not a fix for this error.
- Source inspection confirms the API uses `localhost;Database=NexusPmo;Trusted_Connection=True` in Development and does not apply migrations or create the database automatically. Startup reaches the seed calls only after the context can connect. Use the repository gate for reviewed migrations; do not use `EnsureCreated` or a reset/drop as a repair shortcut.
- Read-only check from the restricted workspace found `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` unset, no local SQL Server service or TCP 1433 listener, and `sqlcmd -S Shady -d master -E` failed with a Named Pipes connection error before executing its query. A host-connected record from 2026-10-31 identified `SHADY\MSSQLSERVER` / `NexusPmo` online at that time and a valid but older 2026-10-30 backup; neither verifies the current database state or provides a fresh pre-update backup.
- No database update, migration, seed, grant write, create, or drop was performed. Current SQL Server identity, migration history/pending count, and physical schema could not be verified from this runtime, even though the user confirmed the database is absent. Blocker: a host-connected SQL session is required to verify the Development instance, create the explicitly requested database, apply the reviewed current migration script, run the seed, and verify the resulting schema and seed state. No SQL data was changed from this workspace.

### Seeded Admin and role-scope verification recheck — 2026-11-01

- Following the user's explicit approval to grant the canonical tenant Admin the full applicable Administration set, `AdminRoleSeedPermissionKeys` now includes every active Administration catalog permission, including all five `Roles.*` operations and `Roles.Delete`, plus the six approved tenant Governance hub permissions. The snapshot retains `TenantAdministration.ManageScope` as tenant-only, and excludes Platform/global authority, cross-Portfolio authority, and unapproved financial approvals. `pmo.admin` remains on its separate four role-editor grants. Ordinary reseeding leaves existing role grants and direct overrides untouched.
- The existing-role reconciliation is gated by Development plus `NEXUS_RECONCILE_SEEDED_ADMIN_ROLE_PERMISSIONS=true`, the deterministic seeded role and Tenant identity, and exactly one active member: the deterministic seeded Admin user. It adds only missing keys from a fixed 29-key allowlist comprising active Administration catalog entries and the approved Governance hub entries. It is add-only, idempotent, leaves direct Allow/Deny overrides unchanged, and writes one audit record with before/after sets and added keys. It no longer depends on an inactive `users.write` or `Roles.Manage` grant. No schema migration is required for the source-only seed correction.
- Tenant settings writes now use active `Settings.ManageTenantConfiguration`; currency, work-item status, and vendor-category writes use active `ReferenceData.Manage`. Corresponding React routes/actions use the same keys, while Platform settings remain under separate Platform policies. Existing English and Arabic catalog labels are reused; this correction adds no permission key.
- Tenant-scope review found that the role service already limits role IDs, assignable permission IDs, user assignments, effective grants, and direct overrides to the authenticated Tenant. The effective-permission resolver excludes foreign-Tenant permission rows and inactive catalog keys, applies direct Deny overrides, and removes Actions whose dependencies are missing. API role policies and service checks use canonical permissions or verified tenant scope; role names do not grant authority. The frontend exposes create, detail-edit, configure, and delete actions through separate permission checks.
- A read-only role-detail projection did not explicitly filter `RolePermissions` by `Permission.TenantId` for a view-only actor. Because the many-to-many join has no TenantId column, a malformed cross-Tenant join row could appear in permission details/counts even though it was not effective. `RoleAdministrationService` now filters role permission counts and detail projections to the requested Tenant. A regression case was added for a malformed cross-Tenant join row.
- Verification: the focused React role-access command passed (6 passed, 16 skipped). `npm --prefix apps/web-react run build` passed (TypeScript and Vite); Vite reported the existing mixed static/dynamic `PlannedPaymentsPage` import and large chunks. `NEXUS.Persistence` rebuilt with 0 warnings and 0 errors. `NEXUS.Modules.Identity` rebuilt with 0 errors and one existing CS8604 nullable warning in `UserAdministrationService.cs`.
- The focused in-memory seed test run matched its test assembly but VSTest failed to connect to the testhost after 90 seconds; no tests executed. The focused `NEXUS.Api.Tests` command exited before diagnostics; its diagnostic build entered unrelated MPXJ/IKVM conversion and was stopped before C# compilation or test discovery. No backend test pass is claimed.
- The offline EF gate passed: model synchronized = Yes, eight repository migrations, no database connection opened. The migrations are `20261029155617_InitialFopxPmBaseline`, `20261029181208_ProjectPaymentRequestPersistence`, `20261029183557_AddTenantRegistry`, `20261030122718_AddProjectAnnualBudgetFoundation`, `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation`, `20261031000210_AddFinancialPersistenceSprints3to6`, `20261031142036_AlignResourceProjectTeamsAndInvitations`, and `20261101114229_AddProjectAnnualBudgetApprovalConfiguration`.

| Database delivery evidence | Status |
|---|---|
| EF model synchronized | Yes (offline gate; no pending model changes) |
| Pending Development migrations | Not run; count and names unknown because no explicit connection was available |
| Development DB identity verified | No |
| Independent backup verified | No |
| Migrations applied | None |
| Physical schema verified | Not run |
| SQL Server persistence tests | Not run; the in-memory seed tests did not reach execution |
| Database rows read or changed | None; no Development `RolePermissions`, overrides, or effective persisted grants were inspected |
| Blockers | A host-connected session must provide the explicit Development connection, verify server/database identity and migration/schema readiness, then inspect role grants before separately authorized data writes. A backup is optional for Development/UAT. |

### Development database migration, seed, and persisted Admin verification — 2026-11-01

This section supersedes the earlier same-day blocked database status above. After the user explicitly requested migration application and database update, and waived an independent backup for this Development initialization, the verified Development target was SQL Server `Shady`, database `NexusPmo` (database ID 5; Windows integrated login `SHADY\shady`). `NexusPmo` was confirmed absent before creation; no existing database was dropped or reset.

- **EF model and migrations:** the repository EF gate reported the model synchronized. The eight pre-existing migrations were applied, followed by `20261101213237_SynchronizePlanningPhysicalSchema`, a reviewed repair for 11 nullable planning columns where the current model requires non-null columns. The three affected tables were confirmed empty before the repair. All nine repository migrations are now applied and zero are pending. The post-apply gate verified the physical schema, reported zero missing current-model objects, and confirmed the acknowledged baseline drift was fully resolved.
- **Backup handling:** the user waived the independent-backup prerequisite. The gate still created and verified a same-volume copy-only backup on SQL Server's `C:` volume before migration writes; `RESTORE VERIFYONLY` passed. No independent/off-volume backup is claimed.
- **Persisted seed verification:** the exact `SeedDataProvider.SeedIdentityOnlyAsync` source path completed against verified `Shady/NexusPmo`. The canonical `admin` role has 111 permission rows, includes all 29 approved tenant Administration/Governance keys, and its effective permission set contains all 29. It has zero Platform role grants. The seeded `pmo.admin` role retains its four role-management grants and has none of the prohibited expanded grants. There are no direct user overrides in this newly initialized database. Because this was a fresh database with no pre-existing Admin grants, the existing-role add-only reconciliation had nothing to add; its source behavior remains gated, audited, idempotent, and non-destructive.
- **Security boundaries:** the seeded Admin snapshot is an explicit allowlist, not all catalog permissions. Platform/global privileges remain excluded. Existing direct Deny precedence, tenant boundaries, dependency filtering, protected-role safeguards, and no-self-escalation remain in the implementation. Ordinary non-Administration grants to user-managed custom roles are not capped by actor possession under the current policy. The database contains no direct override rows, so persisted Deny behavior was not exercised in this run.
- **Verification:** the final EF gate inspection passed with model synchronized, 9/9 migrations applied, 0 pending, and physical schema verified. The focused React parity tests passed (2 files, 21 tests), and the React production build passed with existing chunk-size and mixed-import warnings. The focused backend seed test assembly compiled, but VSTest failed to connect to its testhost; no backend tests are claimed as executed. A full API build was not completed because it entered the existing MPP/IKVM conversion path. The user will perform runtime application testing.

| Database delivery evidence | Status |
|---|---|
| Model synchronized | Yes |
| Pending Development migrations | 0; none pending |
| Development DB identity verified | Yes — `Shady` / `NexusPmo`, database ID 5 |
| Independent backup verified | No — explicitly waived by user; same-volume `C:` copy-only backup passed `RESTORE VERIFYONLY` |
| Migrations applied | Yes — all 9 repository migrations |
| Physical schema verified | Yes — zero missing objects and zero remaining acknowledged baseline drift |
| SQL persistence tests | Not run; focused backend testhost failed to connect |
| Blockers | None for database synchronization; runtime application UAT is for the user |

### Protected Admin baseline and Development reconciliation — 2026-11-02

- Source dynamically derives the mandatory set from active Tenant-scoped Administration, role, recovery, and workflow catalog entries and adds only the approved Portfolio, Program, and Handover operations. The earlier reconciliation checkpoint had 43 baseline keys; the current source baseline is 46 after adding three tenant Reference Data actions. `Program.CreateAcrossPortfolios`, Platform, CrossTenant, and SystemOwner permissions are excluded.
- The trusted protected role is identified by its tenant, System marker, canonical name, and seed creator. API role replacement rejects omitted mandatory permissions; direct Deny on a mandatory permission is rejected and audited. Runtime resolution retains the baseline despite stale Deny rows. Serial tenant-level mutation guards protect the last effective administrator from concurrent role, user, override, and directory changes.
- Portfolio and Program initial manager assignment requires `Handover.AdminInitiate` in both API and UI; CRUD permission keys alone do not allow assignment. CRUD create, edit, archive, and reactivate permissions are enforced by their matching endpoints and UI actions.
- Before-state inspection verified the Admin role had 108 grants, three missing mandatory grants, one user, and no direct overrides. `pmo.admin` had 65 grants. Reconciliation added only `Portfolio.Create`, `Portfolio.Edit`, and `Portfolio.Reactivate`; the resulting Admin has 111 grants and all 43 mandatory keys. Nonbaseline Admin grants, Admin membership, and all `pmo.admin` grants stayed unchanged. No direct Deny needed removal. One role compatibility-grant audit entry was written.
- The backup `NexusPmo_P0_ProtectedAdmin_20261101_224459_290740aa1dfa47ee89c9cc14c3042762.bak` was created on SQL Server's default backup volume using `COPY_ONLY` and `CHECKSUM`; `RESTORE VERIFYONLY` passed. Target: `Shady/NexusPmo`, database ID 5. Nine migrations were applied and none pending before and after; no migration was created or applied for this earlier source/data change. It does not verify the later 46-key baseline.
- Verification: API build succeeded with three nullable warnings in `ProgramService`; focused `PermissionCatalogArchitectureTests`, `UserPermissionAdministrationTests`, and `PortfolioProgramAuthorizationTests` passed (38/38); `SeedDataProviderTests` passed (10/10). Portfolio/Program UI tests passed (12/12), and the React production build passed with existing Vite chunk/import advisories. These backend seed tests use their test provider; database state was separately verified through the targeted SQL Server reconciliation. Browser UI/runtime UAT was not performed.

### Reference Data and Project Finance continuation recheck — 2026-11-02

- The catalog now contains 154 definitions (136 active: 34 View and 102 Action; 18 inactive). The protected source baseline is 46 keys: every active Tenant-scoped Administration catalog permission plus the explicitly approved tenant Portfolio/Program/Handover operations. ReferenceData.View is reused; ReferenceData.Create/Edit/Delete are added. No Import/Export permissions were introduced because no supported operation was found.
- Finance adds `ProjectFinance.View/Manage`, `ProjectAnnualBudget.Manage`, `ProjectPaymentRequest.Manage`, `ProjectSettlement.View/Manage`, `ProjectAnnualBudgetClosing.Manage`, and `ProjectFinancialAuditTrail.View`. Matching View dependencies and operation boundaries keep approval, settlement recording, reversals, closing approval, configuration, and ceiling decisions separate. Audit history remains View-only and immutable.
- The final CSV has 154 rows and 154 unique canonical keys, with 136 active rows; its full key set matches the source catalog. English and Arabic locale JSON both parse.
- Tests passed: API catalog/Admin protection selection 16/16; protected Admin seed/reconciliation 10/10; Reference Data category and Project Role service selection 14/14; Reference Data and Project Finance React tests 30/30. Existing test/build warnings include nullable-reference diagnostics and a low-severity Moq package advisory.
- The React production build did not complete during this continuation. SQL Server inspection using the configured localhost target and explicit `Shady` target failed to connect. Current database identity, migration status, fresh backup, and persisted permission rows are unverified. No database write, grant reconciliation, migration, or reset was performed.
- Browser runtime UAT, direct HTTP authorization scenarios, cross-Tenant runtime calls, Platform-managed record probes, and SQL referential-integrity checks remain unexecuted. The checklist additions above capture those required runtime cases.

## Role delegation policy clarification (2026-11-09)

This report contains prior implementation observations that predate the current Product Owner delegation rule. Any earlier statement limiting ordinary custom-role grants to the actor effective permission set is superseded. An actor authorized through `Roles.ConfigurePermissions` may grant/revoke any active, assignable, scoped non-Administration permission to a same-Tenant user-managed custom role regardless of target category or actor possession. Administration-module permissions remain actor-authorized only. Tenant/resource scope, dependencies, Active Deny, Platform/CrossTenant/SystemOwner boundaries, protected/system-managed role restrictions, the exact protected Admin exception, last-admin safeguards, and no-self-escalation remain. No-self-escalation means a role assigned to the actor cannot be changed to add an effective permission beyond the permissions currently effective for that actor. The current browser/API UAT status is recorded in `docs/quality/MASTER-UAT-CHECKLIST.md`.
