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

## Roles & Permissions presentation (2026-11-05)

The Roles & Permissions editor presents the API's canonical catalog as a three-level hierarchy: Module, then Screen / Feature, then Permission Level. Catalog entries are keyed and rendered by their stable permission IDs; the UI does not create a second permission registry or merge distinct operations that happen to have similar labels. A View row is shown only when the catalog contains a View permission for that screen. The module, screen, and View/Action grouping come from canonical metadata.

The editor supports searching canonical keys, English and Arabic labels, module and screen names, descriptions, and dependency keys. Module, View/Action, assigned/unassigned, and sensitivity filters narrow the catalog shown for the current role draft. The API returns canonical scope plus actor-specific `CanDelegate` state, so the UI can show Tenant, Organization, Portfolio, Program, or Project scope and keep non-delegable active entries visible while disabling their grant controls. Expand/collapse controls are native buttons with expanded state. Module and screen counts describe the currently displayed catalog entries and the selected role draft. Selecting an Action continues to include its declared required permissions; dependency details are presented with the Action, and removing a required View confirms removal of dependent draft Actions.

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
