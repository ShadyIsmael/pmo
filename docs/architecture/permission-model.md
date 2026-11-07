# Permission and Authorization Model

**Current source contract:** 2026-11-03. This document describes the current code-defined permission architecture. The existing audit evidence and working matrix remain under `docs/security/permission-audit/`; runtime and persisted database state are reported separately from source counts.

## Permission catalog

`apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs` is the authoritative catalog. It defines stable keys, Module and Screen grouping, View/Action level, English and Arabic labels/descriptions, sensitivity, scope, assignability, action dependencies, and the protected tenant Admin baseline flag. API DTOs project this metadata to the Roles & Permissions screen; grouping metadata is not stored as new database schema. Retired aliases are not kept in runtime compatibility metadata.

The source catalog currently contains 160 unique active keys (43 View and 117 Action). These are code counts, not persisted Permission rows, role grants, user assignments, or runtime effective access. Retired aliases are not accepted as effective permissions.

## Effective permissions and dependencies

Effective keys are resolved from assigned role grants and direct user overrides by the identity authorization service. Deny overrides normally remove a key. For a user assigned the trusted protected tenant System Admin role, the active mandatory Admin baseline is applied after overrides, so a Deny cannot remove a required baseline permission; a conflicting persisted Deny is logged and does not reduce the effective baseline. The resulting set is passed through `PermissionCatalog.EnforceDependencies`: an Action with a missing required permission is removed from the effective set. New role configurations are validated by the role-administration service and the editor selects dependencies only in its unsaved draft.

Approval-only access is a narrow exception to ordinary same-screen View dependencies. `ChangeRequest.Approve`, `ProjectAnnualBudget.Approve`, `ProjectPaymentRequest.Approve`, and `ProjectPaymentReversal.Approve` authorize an assigned workflow decision, not broad list access. `ProjectFinancialCeiling.ApproveChange` additionally depends on `ChangeRequest.Approve` and applies only to the final approval step of a CR that changes the lifetime ceiling. Each workflow API must verify the exact pending assignment, eligible reviewer, scope, current workflow state, separation of duties, and audit requirements.

## Capability, management scope, and workflow assignment

Authorization combines three independent questions:

1. **Capability:** does the actor hold the required permission key?
2. **Management/data scope:** does the actor have authority over this Tenant, Organization, Portfolio, Program, Project, or resource?
3. **Workflow assignment:** is the actor the eligible reviewer/approver for this exact workflow step?

A permission key does not create resource assignments or widen scope. An assigned reviewer can receive one narrowly filtered record through an approval-specific endpoint; that does not grant access to the normal list or unrelated financial records. Normal read and write APIs continue to apply tenant and resource scope.

The authenticated tenant boundary is derived from trusted identity/context. `ManagementScopeResolver` derives Portfolio, Program, Project, and Organization scope from native assignments and explicit tenant-scoped administrative authority. Organization scope is not a substitute for native manager assignment on a record. Cross-Portfolio Program creation requires `Program.Create` plus the explicit `Program.CreateAcrossPortfolios` capability and an eligible Portfolio inside the actor's authorized Tenant/Organization scope. It does not create a lasting manager assignment.

Platform administration is separate from Tenant administration. Tenant-wide management scope requires trusted, explicitly scoped authority (`TenantAdministration.ManageScope`) and remains within that Tenant. Neither the `admin` nor `pmo.admin` role name grants platform or tenant-wide authority by itself; there is no automatic equivalence between those role names.

Organization settings have their own per-user, per-Organization grants. Platform Settings use platform policies and are not assignable through Tenant Roles & Permissions.

## Financial authorization and CR-only ceiling changes

Annual Budgets, Payment Requests, Settlements/Reversals, and Fiscal Year Closing have separate canonical screen and action keys. There are no active `finances.read` or `finances.write` aliases or broad OR fallbacks. Each operation authorizes against its definitive action key; related source rollups require every source View permission explicitly.

Project Financial Overview has its own read-only `ProjectFinancialFoundation.View` key at Project scope. Cost Utilization report reads use `Resource.View`; actual cost-rate values remain redacted unless `Resource.ViewCostRate` is effective. Export data is provided by `GET /api/resource-analytics/cost-utilization/export-data` and requires both `Resource.View` and the separate sensitive `CostUtilization.Export` key. Program and Portfolio financial rollups each require the matching parent read (`Program.View` or `Portfolio.View`) plus their separate read key (`ProgramFinancialRollup.View` at Program scope or `PortfolioFinancialRollup.View` at Portfolio scope). Both rollup services filter to the authenticated Tenant and authorized descendant Project scope. These read and export keys do not grant workflow actions and are not added to the protected Admin baseline.

The approved Project lifetime financial ceiling may change only through an approved Project Change Request. Project Edit rejects a changed ceiling, and ordinary Project mapping does not write it. Creating or submitting a CR does not apply its proposed ceiling. Only final approval of a CR containing a ceiling change can apply it; the final assigned reviewer must hold both `ChangeRequest.Approve` and `ProjectFinancialCeiling.ApproveChange`. The workflow rechecks Tenant/Project scope, assignment, separation of duties, that the recorded prior ceiling is still current, financial commitments and annual allocations, and writes an audit event in the approval transaction. No independent ceiling approval path exists.

Persistent Project approval defaults use `ProjectAnnualBudget.ManageApprovalConfiguration`. `ProjectApproval.Override` is separate and applies to one submission subject to the workflow's reason, reviewer eligibility, mandatory stages, and audit controls; it must not update Project Settings.

Project Settings has its own Project-scoped `ProjectSettings.View` and `ProjectSettings.Edit` permissions, grouped under Projects → Settings. Edit depends on View. The View key opens the Header Side Sheet but does not replace each section's View key. Workflow settings writes require both Project Settings keys and an authorized mutable Project context. Financial approval configuration also retains its section View and `ProjectAnnualBudget.ManageApprovalConfiguration` checks. These Project-scoped keys are outside the dynamic Tenant Admin baseline; the identity seed adds them to the stable `tenant.admin` role. Direct user Deny overrides remain effective, and the Admin grant does not bypass Tenant, Project, section, or read-only checks.

## Role delegation

Role administration distinguishes `Roles.View`, `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, and `Roles.Delete`. The retired `users.write` key is not accepted by any role policy or service. The actor may only configure permissions within their own effective delegation authority; the server checks tenant/role ownership, binds actor arguments to the current authenticated principal, and re-reads persisted grants. Role updates must not let the actor grant themselves or another assignable role powers outside that delegation boundary. System roles cannot be deleted or renamed, and roles with user assignments cannot be deleted through the current service.

Catalog `IsAssignable` metadata does not itself make a permission available to a particular role administrator. `RoleAdministrationService.GetAssignablePermissionsAsync` returns only active catalog entries already held by the actor, including their dependencies. A permission with no approved initial grant-holder therefore cannot be delegated through the Roles UI; do not bypass this boundary or add an unapproved Admin grant. The initial grant path for the new financial read/export keys remains a release/UAT blocker until an explicit authorization policy and holder are approved.

See `docs/security/PERMISSIONS-ROLE-DELEGATION.md` for the implementation rule and `PERMISSIONS-TWO-LEVEL-UX.md` for the editor behavior.

## Protected tenant Admin baseline

The mandatory baseline dynamically includes all active Tenant-scoped catalog entries in the `administration` module or with `IsTenantAdminBaseline` set, plus the approved tenant Portfolio (`View/Create/Edit/Archive/Reactivate`), Program (`View/Create/Edit/Archive/Reactivate`), and Handover (`View/Manage/Approve/AdminInitiate`) keys. It excludes Organization-scoped `Program.CreateAcrossPortfolios` and all Platform, CrossTenant, and SystemOwner permissions. Portfolio and Program initial manager assignment requires `Handover.AdminInitiate`. Protected tenant Admin identity is the immutable, set-once `Role.SystemRoleKey = tenant.admin`, assigned only by trusted seed/provisioning code to a system role in its Tenant. Runtime authorization does not use the role display name or `CreatedBy` to identify the protected role. Role permission replacement cannot omit mandatory keys; protected System roles cannot be deleted. A serializable tenant guard with a SQL Server application lock prevents concurrent access changes from removing the last effective tenant administrator.

## Compatibility, seeds, and persistence

The source catalog has 160 active entries. Retired aliases such as `finances.write`, `projects.read`, `Roles.Manage`, and `ReferenceData.Manage` are historical examples only; they are absent from active authorization code and cannot authorize requests or be selected as role grants. Workflow permission checks use fresh effective permission claims; API source does not treat `pmo.admin`, `admin`, or `SystemAdministrator` role-name membership as an authorization substitute.

New tenant Admin roles receive the catalog-derived mandatory baseline from that Tenant's own permission rows. Tenant provisioning does not copy grants from another Tenant. Repeated identity seeding adds missing mandatory grants to the keyed protected Admin role without changing other roles or removing unrelated Admin grants. The source-defined current baseline contains 48 keys. The 2026-11-02 P0 delivery adds a stable system-role identity through an EF migration; its one-time backfill assigns `tenant.admin` to uniquely identified, active tenant system roles with the exact legacy Admin seed name, and runtime checks use only the stable key. The requested Development rebuild is a clean EF reset followed by the deterministic application seed; it does not patch legacy permission rows or fan out historical broad grants. Only an explicit Product Owner-authorized reset of Development `Shady/NexusPmo` is exempt from backup; the exact environment/target checks, all EF migrations, full seed, and post-seed verification remain mandatory. All other writes retain the verified-backup requirement.

Catalog grouping metadata remains code-backed and requires no new database columns. The separate SystemRoleKey model change is persisted by its EF migration. Before ordinary schema changes, verify the Development database identity, backup, migration history, model snapshot, and reviewed migration SQL. A database reset is permitted only through the narrowly scoped Product Owner-authorized `Shady/NexusPmo` reset procedure in the [EF Core database workflow](../development/ef-core-database-workflow.md); that reset requires no backup and must still apply all EF migrations and run the full seed.

## Source and delivery references

- Code catalog: `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`
- Role configuration: `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationService.cs`
- Management scope: `apps/api/src/NEXUS.Persistence/Management/ManagementScopeResolver.cs`
- Final matrix: `docs/security/PERMISSIONS-FINAL-TARGET-MATRIX.csv`
- API mapping: `docs/security/PERMISSIONS-CANONICAL-API-MAPPING.md`
- Migration constraints: `docs/security/PERMISSIONS-MIGRATION-STRATEGY.md`
- Implementation and verification status: `docs/security/PERMISSIONS-IMPLEMENTATION-REPORT.md`

## Role category eligibility — 2026-11-05

Role categories are controlled composition metadata for the Roles & Permissions editor: custom roles use `Tenant`, `Organization`, `Management`, `Operation`, or `Business`; `System` is reserved for protected system roles. Every permission exposes an explicit, code-backed multi-category `AllowedCategories` list through the catalog DTO. The editor leaves the actor's delegated permission list visible across category choices, disables out-of-category grants with a localized reason, and keeps backend enforcement authoritative.

`RoleAdministrationService` validates the category and grants on create and update. A category change cannot retain mismatched grants; the API returns the conflicting keys and leaves the role untouched. An administrator may explicitly remove existing mismatched grants while retaining the current legacy category, then change category after all remaining grants are compatible. Unchanged existing grants and system-role categories are not silently rewritten. Permission dependencies, actor delegation, Active Deny, tenant scope, and the protected Tenant Admin baseline remain independent enforcement layers.

The role category property already exists, so this change adds no database column or EF migration. The source-only seed conflict inventory and safe rollout procedure are documented in [Role Category Eligibility and Migration Notes](../security/ROLE-CATEGORY-ELIGIBILITY-AND-MIGRATION.md). Persisted Development roles and grants were not inspected or changed in this source task.
