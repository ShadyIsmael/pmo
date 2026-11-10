# Role Category Simplification and Permission Display Naming

**Status:** Implementation changes are present. React verification passed; backend verification is blocked. Product Owner browser UAT remains pending.
**Date:** 2026-11-09

## Changes

- Removed `System` / `Management` / `Operation` / `Business` category eligibility as a restriction on ordinary permission grants, revocations, and editable role category changes. `PermissionCatalog.AllowedCategories` remains compatibility metadata and returns all selectable custom-role categories.
- Kept Role Category as descriptive role metadata. It no longer disables an ordinary permission checkbox or causes the API to reject an ordinary grant.
- Updated the Roles & Permissions catalog contract: `CanDelegateToCustomRole` represents ordinary custom-role delegation; legacy `CanDelegate` remains for actor/bootstrap delegation to system roles.
- Actors with effective `Roles.ConfigurePermissions` can grant or revoke active, assignable, scoped non-Administration permissions on same-Tenant custom roles, without personally holding each permission. Administration-module permissions remain actor-authorized only.
- Added a self-escalation guard: a role assigned to the actor cannot be changed to add an effective permission beyond the actor's current effective set. User role-assignment authority remains unchanged.
- Changed Roles & Permissions to a two-level screen/feature group → action list. Search includes display text and canonical keys. Category-only disabled reasons were removed.
- Standardized 174 current catalog display rows to concise English and Arabic module/group/action labels. Removed the obsolete `PermissionRoleCategoryPolicy` implementation, category-only grant conflict checks and localized messages, and unused duplicate `permissionLabels` blocks.
- Updated the permission architecture and delegation documentation, catalog review, and Master UAT checklist.

## Display name examples

The complete bilingual before/after mapping for all 174 unique canonical keys is in [`PERMISSION-DISPLAY-NAME-MAPPING.csv`](PERMISSION-DISPLAY-NAME-MAPPING.csv).

| Canonical key | Previous group and action | New group and action |
| --- | --- | --- |
| `Audit.View` | Audit trail / سجل التدقيق — View audit trail / عرض سجل التدقيق | Audit Trail / سجل التدقيق — View / عرض |
| `ChangeRequest.Approve` | Change requests / طلبات التغيير — Change request: Approve or reject / طلب التغيير: اعتماد أو رفض | Change Requests / طلبات التغيير — Approve or reject / اعتماد أو رفض |
| `Directory.ExecuteSync` | Directory integration / تكامل الدليل — Run directory synchronization / تنفيذ مزامنة الدليل | Directory Integration / تكامل الدليل — Run sync / تشغيل المزامنة |

Display metadata changed; canonical keys and permission identities did not.

## Security behavior retained

- `Roles.ConfigurePermissions` remains required to manage grants. Ordinary scoped non-Administration custom-role grants no longer require actor possession; Administration-module permissions remain actor-authorized only.
- The separate 21-key bootstrap set and 13-key protected Admin subset remain. They do not automatically grant any permission.
- Tenant, organization, project, resource, and workflow scope checks remain. Platform, CrossTenant, and SystemOwner scopes remain unavailable to tenant role management.
- Dependencies and effective permission evaluation remain; Active Deny continues to override Allow.
- Protected Admin baseline permissions remain locked against removal or denial; protected/system-managed role safeguards and last-admin protection remain.
- Workflow-specific category rules, such as Annual Budget approver eligibility, are separate from permission grants and remain unchanged.
- No permission was granted automatically. No canonical key or permission ID was renamed, merged, or deleted. All 171 catalog entries present in repository HEAD retain the same seed ID, key, resource, and action tuple; the full current catalog mapping has 174 rows.

## Verification

- React focused test `src/test/access-security-pages.test.tsx`: **35 passed / 35 total**. This includes Budget Revisions selection and save across categories when the actor lacks those permissions, revocation, existing permission screen coverage, dependencies, canonical-key search, and protected Admin behavior.
- `git diff --check`: passed; Git emitted only line-ending normalization notices.
- English and Arabic locale JSON parsed successfully. The mapping CSV has 174 unique keys and no missing new EN/AR labels.
- The focused API test attempt did not reach test execution; it stalled during MPXJ/IKVM conversion and was stopped. Backend tests are **not verified** after the latest changes.
- The sole final API build attempt completed MPXJ/IKVM conversion but failed copying `NEXUS.Modules.ProjectPlanning.MppAdapter.dll` because another process held the API output file (`MSB3027` / `MSB3021`). No build retry was run. NuGet vulnerability lookups also emitted `NU1900` because nuget.org was unreachable.
- No database writes, permission reconciliation, or permission-specific migration were performed. Existing grants and denies were not edited by this change.

## Remaining verification

Backend regression tests and a successful API build remain unverified due to the earlier IKVM conversion stall and locked output DLL. Product Owner browser UAT must verify first-time cross-category grants, save/reload, revocation, effective-permission refresh, Active Deny, protected Admin behavior, and bilingual labels. Role Category grant restrictions must remain suspended until the Product Owner explicitly approves their reintroduction.

## Permission deletion and Admin grant request — blocked

The Product Owner subsequently requested deletion of database permissions followed by granting all permissions to Admin for diagnosis. A read-only `sqlcmd` connection to `tcp:localhost,1433`, database `NexusPmo`, failed with a socket access-permission error and login timeout. The live server name, database ID, SQL principal, effective API Development connection, tenant inventory, and backup could not be verified. No backup, database write, deletion, grant, deny change, migration, or seed was performed in this attempt.

Source inspection shows that deleting `Permissions` cascades to `RolePermissions` and is restricted by `UserPermissionOverrides`; removing the latter to force deletion would erase existing Allow/Deny records. A database-wide deletion could therefore affect roles and tenants beyond the protected Admin. The standalone permission operation is not the documented full-database reset. Before preparing a reviewed, transaction-scoped Development/UAT operation, verify the live environment and target, tenant and protected role identity, foreign keys, current grants and overrides, and obtain explicit authorization. A backup is optional; Production backup and recovery safeguards remain mandatory.

The database rows do not carry the canonical catalog's scope, active/assignable flags, or dependency metadata. Any Admin grant set must be derived from the current canonical catalog and checked against the live tenant, excluding Platform, CrossTenant, and SystemOwner privileges. Existing ordinary Deny and resource-scope rules remain effective. The running API build/version also remains unverified, so a database-only change would not establish that the updated role-editor behavior is deployed.

## P0 Permissions/Admin database recovery resume — 2026-11-10

**Status: NOT COMPLETE.** The current task supersedes the earlier request to delete and recreate Permission rows: no catalog deletion, full seed, migration, or database write is authorized or performed here. Development backups remain optional; none was requested or used.

### Current source evidence

- `PermissionCatalog` contains 174 unique active canonical permissions (46 View, 128 Action), all assignable. Exactly 129 have `Scope=Tenant`; the other 45 use Organization, Portfolio, Program, or Project scope. The earlier 129 figure is correct for Tenant scope only, not the full source catalog.
- `ProtectedTenantAdminPolicy` currently requires 51 Tenant-scoped keys (Administration, explicitly flagged Tenant baseline, and approved Portfolio/Program/Handover keys). The regular seed additionally includes `ProjectSettings.View` and `ProjectSettings.Edit` as Project-scoped Admin grants.
- The current role editor has no category-only checkbox restriction; `RoleCategoryPolicy` is removed and category remains descriptive metadata. Existing focused React evidence records 35/35 passing, but it was not rerun in this resume.
- The current mapping has 174 unique canonical keys and bilingual action labels. Labels are sourced from the API catalog metadata used by the React screen. Live API labels and browser EN/AR/RTL rendering remain unverified.
- Effective permission source logic subtracts Deny from ordinary Allow, then restores only the protected mandatory Admin baseline. Request middleware reloads persisted effective permissions per request; the React editor refreshes the current user after a successful save. These behaviors were source-reviewed, not tested against the current Development database in this resume.
- Protected Admin first-grant source logic is narrower than the requested 129 Tenant-scoped set: it allows the existing protected Admin annual-budget exception, plus actor-owned and mandatory keys. No code change broadened this path, and no Admin grants were added.
- One final API build attempt completed with 381 warnings and 4 errors. MSB3027/MSB3021 show copy retries exhausted because another process held `NEXUS.Modules.ProjectPlanning.MppAdapter.dll` and `NEXUS.Persistence.dll` in the API output directory. No build retry was run. The offline EF gate build succeeded with 10 `NU1900` warnings; its model check still failed synchronization.

### Database and schema evidence

- The prior task's supplied inspection states `Shady/NexusPmo`, DB ID 5, 17 applied migrations and zero pending at that inspection time. This run could not freshly confirm that identity or history. `sqlcmd -S tcp:localhost,1433` failed before SQL authentication with a socket access-permission error and login timeout. The current process also lacks `ASPNETCORE_ENVIRONMENT` and `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING`, which the repository gate requires to compare the live target with the API Development connection.
- The offline EF gate built successfully and reports 17 repository migrations, but `HasPendingModelChanges()` reports **EF Model synchronized: No** (exit 2). The offline command reports only the boolean, so the exact model/snapshot delta remains unidentified. No migration was applied.
- The last successful live schema inspection (2026-11-08) reported 16 physical differences confined to Programs and Integrations, not the permission tables. The confirmed Program cause is `Programs.RowVersion` stored as `varbinary(max)` while the EF model expects generated SQL Server `rowversion`; additional Program length/index differences and separate IntegrationConnections/IntegrationWebhooks differences were reported. That inspection found the permission, role-grant, user-role, override, and audit tables matched the model at that time. This is historical evidence, not a fresh verification.
- No permission-table schema mismatch was identified in source or the last successful live comparison. Because the present target and physical schema could not be opened, a permission-only transaction could not be proven safe for this run. No manual SQL patch, catalog reconciliation, migration, seed, grant, Deny edit, or audit write occurred. No backup was required or requested.

### Counts and remaining gates

| Measure | Before | After |
|---|---:|---:|
| Source active canonical catalog | 174 | 174 |
| Source `Scope=Tenant` assignable permissions | 129 | 129 |
| Live database catalog rows | Not freshly verified | No change made |
| Protected Admin role grants/effective set | Not freshly verified | No change made |
| Existing Deny records and audit history | Not freshly verified | No change made |

The approved Admin recovery set must be reconciled against the current canonical 129-key Tenant scope list and exact Development tenant before any write. A scoped transaction must add only missing links to the stable `tenant.admin` role, preserve all other role grants, preserve every existing Deny override and audit record, and verify before/after counts and effective results. Platform/global scopes remain excluded. Last-admin protections remain required. Browser grant/revoke, first-grant, refresh, Active Deny, Admin access, and live bilingual label checks remain pending after the host-connected database step.

No directly related category-policy code remains active; the compatibility `AllowedCategories` response field is still used for API contract stability. No additional legacy cleanup was performed because the required live permission state could not be checked.

## P0 Admin permission recovery execution — 2026-11-10

**Status: AUTHORIZED TENANT-SCOPE RECONCILIATION COMPLETE.** This execution supersedes the earlier “NOT COMPLETE” checkpoint above. It did not reset the database, apply migrations, alter the connection string, or change database schema.

### Target and before-state

- Windows PowerShell resolved the API Development configuration to the existing `tcp:localhost,1433` / `NexusPmo` endpoint. A live query returned `SERVERPROPERTY('ServerName') = Shady`, `DB_NAME() = NexusPmo`, and `DB_ID() = 5`; the target was checked again through the configured EF connection immediately before each write.
- All 17 repository migrations were present in `dbo.__EFMigrationsHistory`; the EF connection reported zero pending migrations. No migration was applied.
- The stable protected role was the single active Tenant `tenant.admin` role (`A0000001-0000-0000-0000-000000000001`) with one active member. Before reconciliation the database contained 161 active canonical permission rows and the Admin role had 110 active grant links. The active user override table was empty; seven existing Admin-role audit entries were preserved.
- The source catalog defined 174 active permissions, including 129 active, assignable `Scope=Tenant` keys. The recovery set was derived from those 129 current catalog entries. Platform and other non-Tenant scopes were excluded.

### Reconciliation and verification

- Ran the Development `--seed-identity` path once. It added 13 missing source-canonical permission rows and restored four missing protected Admin baseline links. As documented above, this seed path did not append an audit event for those four baseline links; the pre-existing seven audit entries remained intact.
- A separate serializable EF transaction rechecked the exact SQL target, required zero pending migrations, acquired the tenant Admin mutation lock, and added only the 19 missing active, assignable Tenant-scoped permission links. It appended one `ProtectedAdminPermissions.Reconciled` audit event listing the added keys. It did not modify any other role grant, role membership, or user override.
- Post-commit checks returned 174 active canonical rows, 133 active Admin links (129 Tenant-scoped plus the four pre-existing non-Tenant grants), and one active Admin member. `UserEffectivePermissionService` computed 133 effective permissions, including all 129 authorized Tenant-scoped keys. The live override table still contained zero rows and zero Deny overrides.
- All seven other system-role grant fingerprints matched their before-state; all role membership fingerprints matched; the user override fingerprint matched. The Admin role audit count increased from seven to eight, with the new event recording the 19-key transaction.
- The 19 added keys were: `ApprovalRequest.Approve`, `ApprovalRequest.Cancel`, `ApprovalRequest.Delegate`, `ApprovalRequest.Reject`, `ApprovalRequest.Submit`, `ApprovalRequest.View`, `Budget.Create`, `Budget.Delete`, `Budget.Edit`, `Budget.View`, `CostUtilization.Export`, `Issue.View`, `PaymentSettlement.Record`, `ProjectFinancialAuditTrail.View`, `ProjectFinancialCeiling.ApproveChange`, `ProjectPaymentRequest.Approve`, `ProjectPaymentReversal.Approve`, `ProjectPaymentReversal.Create`, and `ProjectSettlement.View`.

### Completion fields

| Check | Result |
|---|---|
| EF model synchronized | No — the prior offline gate reported pending model/snapshot drift; it was not rerun in this permission-only recovery. No schema changes were made. |
| Pending migrations | 0; all 17 source migrations are applied. |
| Development target verified | Yes — `Shady / NexusPmo`, database ID 5, through Windows PowerShell and the configured EF connection. |
| Backup | Not requested; Development backup is optional. |
| Migrations applied | None. |
| Seed mode and idempotency | `--seed-identity` completed; source regression tests cover repeatability and preservation, but tests were not rerun here. The 19-key EF transaction is add-only and set-difference based; post-commit state has no missing authorized keys. |
| Physical schema | Touched permission, role, role-link, membership, override, audit, and migration-history tables were inspected read-only. No schema was modified; a full all-table schema comparison was not rerun. |
| Persistence tests | Not run. Live post-commit effective permissions were read through `UserEffectivePermissionService`. |
| Remaining issue | The earlier EF model/snapshot drift and unrelated full-schema differences remain separate follow-up work. Baseline links restored by `--seed-identity` have no distinct audit event; the subsequent 19-key transaction is audited. |
