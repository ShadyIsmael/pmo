# Permission catalog review

## Previous source snapshot (2026-11-02)

The authoritative catalog is `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`. It contained 154 unique active keys: 39 View and 115 Action, with 154 unique seed IDs and bilingual labels in this snapshot. There were no inactive compatibility entries. The source-derived protected tenant Admin baseline contained 48 keys by architecture-test assertion. These counts did not represent persisted rows, role grants, direct overrides, or effective runtime access.

Earlier reports recorded 133 active keys. That source update added 24 financial capabilities, retired `ProjectFinance.Manage`, and removed the unsupported `Resource.Allocate` duplicate and broad `Resource.ManageRates` fallback (net +21). The task brief's separate 136-key reference could not be reproduced from that repository snapshot; the reported 3-key difference between those earlier baselines remained unexplained at the time.

The existing `ACTION-PERMISSION-MATRIX.csv` retained its row IDs and source evidence. Missing current catalog keys were added with their catalog Seed IDs; retired keys remained in the matrix as historical rows with `Catalog Active=false`.

## Design controls in source

- Stable resource/action keys, bilingual module/screen labels, sensitivity, and explicit dependencies are defined in code.
- The effective resolver filters unknown/inactive keys and removes Actions whose required permissions are absent; it does not add grants.
- Protected Admin baseline derives from eligible active tenant-scoped Administration and role-management entries plus the approved operational keys. Organization, Platform, CrossTenant, and SystemOwner scope is excluded.
- Existing canonical role grants and direct overrides are not rewritten by ordinary startup. Sensitive financial operations remain separate.
- API policies and frontend constants now cover every active catalog key. `Resource.Allocate` was consolidated into `Resource.Assign`; resource rate editing now explicitly depends on `Resource.ViewCostRate`, and neither `Resource.ManageRates` nor an edit-to-view fallback can expose rate data.

## Source-level findings closed

1. Issue reads use `Issue.View`; Issue queries fail closed without tenant and project management scope.
2. Plugin reads use tenant-scoped `Plugin.View`; no platform-wide authority is implied.
3. React route and navigation guards use canonical View keys for Projects, AI Planning, Audit, Work Items, Settings/Integrations, and Plugins.
4. Optional budget draft creation during project setup uses `ProjectAnnualBudget.Create`.
5. Status report export uses a server-side `StatusReport.Export` policy and only returns Published reports.

## Compatibility and persisted data

Legacy aliases are no longer active catalog entries and do not contribute to effective permissions. The explicit Development reconciliation archives retired permission rows and their role/direct-override links, while leaving active canonical grants untouched. It does not fan a broad alias out to multiple operations. The guarded EF reset path is implemented for the approved `Shady` / `NexusPmo` target, but neither it nor the seed has been executed because the current Development target and recovery path are not verified.

## Remaining evidence gaps

- SQL Server identity verification failed in this session with `Cannot generate SSPI context`; migration state, physical schema, and backup remain unverified.
- Persisted Permission, RolePermissions, UserRoles, and direct-override inventories were not queried.
- Full action-to-route/API/service/scope coverage remains uncomputed; the raw event inventory is not a business-action denominator.
- Runtime browser UAT, build, and tests remain deferred until the single final verification pass requested by the Product Owner.

## Current catalog recount (2026-11-05)

The current source contains **166 unique active keys: 44 View and 122 Action**, with 166 unique seed IDs. Six active `ApprovalRequest.*` permissions (`View`, `Submit`, `Approve`, `Reject`, `Delegate`, and `Cancel`) were omitted from the earlier 160-key tally; they were already present in source, are in active service use, and are outside the protected Admin baseline. This reconciles why the task brief and older reports state 160 or 154. The source metadata corrections changed organization metadata only; stable keys and IDs did not change, and the exact dynamic 47-key protected Admin baseline set is unchanged before and after.

The matrix has 174 rows marked `Catalog Active=true`, representing 159 distinct active Action IDs. Seven current source keys are absent from its mapping: the six `ApprovalRequest.*` keys and `ProjectSettings.View`. Track this as a deferred documentation mapping follow-up; do not rewrite the matrix as part of this UI task. The Roles & Permissions UI reads the complete API catalog. Persisted database rows, role grants, direct overrides, and runtime access remain unverified.

## Roles & Permissions presentation (current hierarchy; historical verification notes follow)

The current Roles & Permissions editor presents the canonical catalog in two levels: Screen/Feature group, then View and supported Action rows. Module remains a filter/search classifier rather than an outer hierarchy. Entries are rendered by stable permission IDs; the UI does not create a second registry or merge distinct operations with similar labels.

The editor searches canonical keys, English and Arabic labels, module and screen names, descriptions, and dependency keys. Module, View/Action, assigned/unassigned, and sensitivity filters narrow the catalog. Scope remains visible. A delegation state is not a synonym for actor possession: an authorized `Roles.ConfigurePermissions` holder may grant ordinary non-Administration permissions to a user-managed custom role without holding each key. Administration-module permissions remain actor-authorized only. Dependencies, target-role restrictions, no-self-escalation, Active Deny, tenant/resource scope, and protected-role rules continue to be server-enforced.

Protected Tenant Admin entries are identified from the server-projected mandatory flag (`isMandatoryForSystemAdmin`), which is derived by `ProtectedTenantAdminPolicy`. The current source baseline is 47 keys, and the exact set was preserved before and after this task's metadata corrections. The UI does not rely on a historical count. Mandatory entries are included in the protected role draft and rendered locked; this presentation does not add financial approval, settlement, or other financial grants. Existing Active Deny and effective-permission rules remain backend responsibilities.

Focused React regression coverage has been added for canonical-key and description search, dependency visibility and selection, and the module/level/grant/sensitivity filters. The existing protected-Admin regression continues to assert that mandatory permissions are checked and disabled. These tests are authored but not executed in this update; consolidated focused verification must record their result before closure.

No database operation, migration, direct SQL permission edit, or role-grant reconciliation is part of this UI/metadata update. Any catalog label/grouping correction that requires persisted metadata reconciliation must be reported separately and must not change role grants.


## Final focused verification (2026-11-05)

- Roles & Permissions React regressions: **PASS — 27/27** (`src/test/access-security-pages.test.tsx`), including module/screen expansion, search, filtering, dependency display and selection, locked protected Admin entries, bilingual direction, and duplicate-label distinctions.
- React production build: **PASS** — TypeScript check and Vite production build completed; 2,890 modules transformed.
- Identity authorization module build: **PASS** — `NEXUS.Modules.Identity.csproj`, 0 warnings and 0 errors.
- Focused API permission-catalog tests: **BLOCKED before test execution** by compilation errors in existing Approval and WorkflowEngine sources (`ApprovalService` cannot resolve `NEXUS.Modules.Audit` / `IAuditService`; `AuditProjectionActionHandler` has a `Guid`/null conditional error and references missing `WorkflowExecutionContext.EntityType`). No API test results were produced.
- Source review continues to find 166 active canonical keys (44 View, 122 Action) and an unchanged 47-key protected Admin baseline. The action matrix still lacks mappings for six `ApprovalRequest.*` keys and `ProjectSettings.View`; this documentation follow-up is deferred.
- No database, migration, SQL permission row, or persisted role grant was changed or inspected. No browser UAT was run.

## Project Plan permission catalog synchronization (historical snapshot, 2026-11-08)

**Status:** The scoped Development catalog synchronization is complete. The authenticated Roles & Permissions HTTP response and Product Owner browser UAT remain unverified.

### Canonical source and before-state

At the time of this historical 2026-11-08 snapshot, the source catalog contained 171 active keys (46 View, 125 Action). `ProjectPlan.Create` had stable ID `b0000286-0000-0000-0000-000000000286`, Project scope, and dependency `Planning.View`; the label and Management-only grant eligibility stated here describe that snapshot only. `Planning.Edit` had ID `b0000243-0000-0000-0000-000000000243` and dependency `Planning.View`; `Planning.View` retained ID `b0000116-0000-0000-0000-000000000116`. The 2026-11-09 category and actor-possession policy addendum below supersedes the category/actor-grant eligibility statements from this snapshot.

Read-only inspection verified Development SQL Server `Shady`, database `NexusPmo`, database ID `5`. Before synchronization the database contained 160 active Permission rows. `ProjectPlan.Create` was absent; `Planning.View` and `Planning.Edit` each had one active canonical row. The persisted `Planning.Edit` description was stale and was normalized to the current English source description. Before-state assignment fingerprints were: `RolePermissions` 689 rows / `CHECKSUM_AGG` -1464860630; `UserRoles` 4 rows / -2065786207; `UserPermissionOverrides` 0 rows (0 active Allow, 0 active Deny). No trigger exists on `dbo.Permissions`.

### Sync path and exact effects

No existing catalog-only command was present. `--rebuild-canonical-permissions` reconciles unrelated catalog entries and may retire legacy rows/links, add protected Admin grants, or archive Admin Deny overrides. `--seed-only`, `--seed-identity`, and `--seed-minimal-only` also have broader effects. A guarded EF gate action, `sync-project-plan-permissions`, was added for only `Planning.View`, `Planning.Edit`, and `ProjectPlan.Create`. It verifies Development `Shady/NexusPmo`, EF model/history readiness, and physical schema for the permission, grant, assignment, override, and audit tables. It runs a serializable transaction with the tenant mutation lock, rejects duplicate/conflicting/soft-deleted target rows, writes only `dbo.Permissions` plus one audit event, and does not touch grants, role assignments, direct overrides, or protected roles.

Exact first synchronization command:

```powershell
.\scripts\database\ef-database-gate.ps1 sync-project-plan-permissions -NoBuild --environment Development --expected-server Shady --expected-database NexusPmo --task-authorization-reference 'FOPX PM P0 Project Plan Permission Catalog Database Synchronization 2026-11-08'
```

Before the successful run, a fresh `COPY_ONLY, CHECKSUM` Development backup was created and `RESTORE VERIFYONLY` passed at `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo-Development-20261108-da894b0fd84243a5852801a0dc1581ec.bak`. A first attempt stopped before commit because the trusted tenant context was not active; a direct SQL check confirmed rollback and unchanged before-state counts/fingerprints. The context was corrected in the gate invocation before the successful run. The first successful call followed the independent backup verification and used the exact command above. The task-specific catalog-only action does not make an independent backup a prerequisite. When `--backup-verified-path` is supplied, the gate checks `RESTORE HEADERONLY` for exactly one full backup matching the verified server/database, then runs `RESTORE VERIFYONLY` before syncing.

Current guarded command, also used for the successful no-change rerun:

```powershell
.\scripts\database\ef-database-gate.ps1 sync-project-plan-permissions -NoBuild --environment Development --expected-server Shady --expected-database NexusPmo --task-authorization-reference 'FOPX PM P0 Project Plan Permission Catalog Database Synchronization 2026-11-08' --backup-verified-path 'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo-Development-20261108-da894b0fd84243a5852801a0dc1581ec.bak'
```

Successful sync results: one catalog row inserted (`ProjectPlan.Create`), one existing row normalized (`Planning.Edit` description), and `Planning.View` unchanged. The active `ProjectPlan.Create` row now exists exactly once with the canonical ID, Tenant ID `11111111-1111-1111-1111-111111111111`, key, and source description. Total active Permission rows increased from 160 to 161. One `PermissionCatalog.ProjectPlanEntriesSynchronized` audit entry was written. No role grant, assignment, Allow, Deny, or protected Admin role link changed.

Idempotency rerun used the same action with `--backup-verified-path` set to the backup above. The gate verified the backup header and `RESTORE VERIFYONLY`; records inserted: 0; records normalized: 0; changed keys: none; all three target keys were unchanged. A final no-backup-argument rerun exercised the task-specific exception, reported backup verification as not required, and also changed 0 records. Direct SQL after-state checks returned exactly one active `ProjectPlan.Create`, 689 `RolePermissions` with the same fingerprint, 4 `UserRoles` with the same fingerprint, and 0 user permission overrides. The protected Admin role-link set therefore remained unchanged.

### Database gate and automated verification

- EF model synchronized: **Yes**. Offline model check passed; 15 repository migrations.
- Development identity: **Verified** as `Shady/NexusPmo`, database ID 5.
- Migration history: **15 applied, 0 pending, 0 history-only**; no migrations applied.
- Backup: **Verified** with `COPY_ONLY, CHECKSUM` and `RESTORE VERIFYONLY` as listed above.
- Physical schema: **No** globally. Inspection reports 16 pre-existing differences confined to `IntegrationConnections`, `IntegrationWebhooks`, and `Programs`; the `Permissions`, `AuditEntries`, `RolePermissions`, `UserRoles`, and `UserPermissionOverrides` tables used by this operation match the current EF model.
- EF database gate build: **Passed**, 0 errors; 10 `NU1900` warnings because NuGet vulnerability metadata was unreachable.
- Default API test-project compilation: **Blocked** by unrelated concurrent test-file edits (`LocalStorageProviderPathTraversalTests.cs`: obsolete assertion/API diagnostics; `ForwardedHeadersTrustTests.cs`: unresolved `ForwardedHeadersOptions`). Temporary external build filters excluded only those files for the focused Project Plan tests; the concurrent files were not changed by this work.
- Project Plan delegation/category/Active Deny test class: **8/8 passed** after correcting its fixture to enter the expected trusted tenant context. These tests use EF InMemory, not Development SQL Server.
- Project Plan API/UI source-contract and catalog metadata tests: **2/2 passed**. They verify the exact create/edit gates, dependency, scope, category eligibility, and protected Admin exclusion.
- Full `PermissionCatalogArchitectureTests`: **24/25 passed**; one existing protected Admin count assertion expected 47 but observed 51. No Admin role grants were changed by this sync. Resolve the source/test count mismatch separately.
- SQLite-backed `PlanServiceTests` / `PlanningImportServiceTests` did not complete: test schema setup fails with SQLite `near "max"` syntax. The relevant authorization source contracts and service-level delegation tests passed; authenticated HTTP API and browser runtime were not exercised.

### Remaining evidence gaps

- At the time of this 2026-11-08 snapshot, the source catalog had 171 active keys while the targeted Development database had 161 active rows. The current source count and delegation policy are recorded in the 2026-11-09 addendum below.
- The database contains the canonical row and the role administration service tests return its category/delegation metadata, but an authenticated live Roles & Permissions HTTP response was not run.
- Browser UAT was not run, as requested; the Product Owner should confirm the permission appears in the role editor, then verify role grant and reload behavior.
- The 16 unrelated physical-schema differences and the 47-versus-51 protected Admin source/test count discrepancy remain open and were not modified.

## Current delegation policy clarification (2026-11-09)

The current catalog contains 174 active canonical keys (46 View and 128 Action). The current Roles & Permissions hierarchy is Screen/Feature group followed by View and Action rows; Module is used for filtering and search. The full old-to-new bilingual display mapping is in `PERMISSION-DISPLAY-NAME-MAPPING.csv`.

The latest Product Owner clarification supersedes earlier actor-possession ceilings for ordinary custom-role grants. An actor authorized through `Roles.ConfigurePermissions` may grant or revoke active, assignable, scoped non-Administration permissions to a same-Tenant user-managed custom role regardless of the target category or whether the actor holds each key. Administration-module permissions remain actor-authorized only. This does not grant authority over Platform, CrossTenant, SystemOwner, protected/system-managed roles, or another Tenant. Dependencies, Active Deny, scope, protected Admin/last-admin safeguards, and no-self-escalation remain. A role assigned to the actor cannot be changed to add an effective permission beyond the permissions currently effective for the actor.

The catalog `CanDelegate` value must not be interpreted as “the actor already holds this permission.” It represents permission-administration authority under the applicable rules; target-role eligibility and self-escalation are rechecked during mutation. BudgetRevision cross-category grant, save, reload, and revoke cases are pending in the [Master UAT Checklist](../../quality/MASTER-UAT-CHECKLIST.md). This clarification records policy and documentation only; it does not claim runtime verification.