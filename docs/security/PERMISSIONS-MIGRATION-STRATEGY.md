# Permission Key Migration & Compatibility Strategy

**Status:** This file retains the key-migration design. P0 protected Admin source changes and the 2026-11-02 Development reconciliation are complete. `Shady/NexusPmo` (database ID 5) has 9 applied migrations and 0 pending; a verified SQL Server COPY_ONLY backup preceded a narrow, audited addition of three missing Admin grants. No migration was created or applied, and no broad role reset or legacy-key fan-out occurred.

## Guiding rules

1. Treat permission keys as stable identifiers during the compatibility period. Correct labels and screen metadata without renaming keys.
2. Never reset or recreate roles. Preserve tenant-specific roles, system-role identity, user-role assignments, and direct Allow/Deny overrides.
3. Never expand one broad grant into several sensitive narrow grants automatically. Every one-to-many mapping requires a per-tenant, per-role approval ledger.
4. Keep permission capability separate from tenant, organization, Portfolio, Program, Project, native manager assignment, and workflow assignment.
5. New approval and cross-Portfolio keys receive no automatic role grant.
6. Preserve historical audit and approval records. Do not rewrite the actor, key/permission ID, decision, approval route snapshot, or recorded history.

## Proposed key mapping ledger

| Existing key | Proposed target key(s) | Transition handling | Grant mapping policy |
|---|---|---|---|
| projects.read | Project.View | Temporary alias until route and API clients align. | Review persisted grants and direct overrides. Mapping can change API visibility; do not copy without tenant-owner approval. |
| portfolios.read | Portfolio.View | Temporary alias; no static active feature check found. | Manual review; preserve original row until effective-access comparison. |
| resources.read | Resource.View | Temporary alias; no static active feature check found. | Manual review; preserve original row until effective-access comparison. |
| projects.delete | Project.Delete | Legacy duplicate display label; canonical action is actively enforced. | Never auto-copy because the old key was not found in an active permission check. |
| projects.write | No automatic mapping. Possible Project.Create and/or Project.Edit only by business intent. | Retain until grant/client/historical use review; then deprecate. | Do not fan out to both keys. |
| portfolios.write | No automatic mapping. Possible Portfolio.Create/Edit/Archive/Reactivate only by role intent. | Retain until reviewed; then deprecate. | No automatic fan-out. |
| resources.write | No automatic mapping. Map only selected existing Resource actions after review. | Retain until reviewed; then deprecate. | No automatic fan-out. |
| reports.read | Keep reports.read as Reports View. | Enforce on route/API after query-scope review. | Existing source grants remain unchanged; runtime grant state unknown. |
| reports.export | No current ReportController export endpoint found. | Compatibility hold, then deprecate only after client/dynamic/historical review. | No replacement grant. |
| manager.handover | No direct replacement determined. | Compatibility hold, then deprecate only after semantic and grant review. | Never map automatically to Handover.Approve. |
| Resource.Manage | Existing Resource.Create/Edit/Delete/Assign/Allocate/ManageRates/EditCostRate and other explicitly needed keys. | Temporary alias, then deprecate. | Review each role; do not copy to every action. Preserve ViewCostRate as a separate read/action decision. |
| Team.Manage | Team.Create, Team.Edit, Team.Delete, plus Team.View where required. | Temporary alias, then deprecate. | Manual role-by-role mapping. |
| Directory.Manage | Directory.TestConnection, PreviewSync, ExecuteSync, ManageMappings, ManageProvisioningPolicy, as individually required. | Temporary alias, then deprecate. | Sync execution and provisioning changes require explicit owner approval. |
| Planning.Manage | Existing Planning.Lifecycle, WorkItem actions, and Planning.View where individually needed. | Temporary alias, then deprecate after every AI/planning consumer is mapped. | Do not copy to all planning capabilities. |
| Meeting.Manage | Keep current key; relabel to meeting minutes/comments/work-item-link management. | Not retired by this proposal. | Not equivalent to Meeting.Create/Edit/Delete in current source description. |
| Governance.AssignmentRecovery.Manage | Keep; limit to audited assignment recovery. | Retain. | No mapping to general admin or handover approval. |
| ChangeRequest.Manage | Keep; relabel to create/update/cancel change requests. | Retain; approval remains ChangeRequest.Approve. | No approval grant. |
| Program.Manage | Program.Create, Program.Edit, Program.Archive, Program.Reactivate. | Temporary compatibility key, then retire after mapping and policy cutover. | Manual role review; do not give every Portfolio Manager all lifecycle actions. |
| Portfolio.Manage | Portfolio.Create, Portfolio.Edit, Portfolio.Archive, Portfolio.Reactivate. | Temporary compatibility key, then retire after mapping. | Manual role review; tenant/organization scope is independent. |
| finances.write | ProjectAnnualBudget.Create, EditDraft, SubmitForReview, ReviseApproved, ManageApprovalConfiguration; ProjectPaymentRequest.Create, EditDraft, Submit, Cancel; residual financial write consumers remain to be mapped. | Keep as a transitional/general residual capability. Remove it as a prerequisite for dedicated approval/settlement/reversal/closing/carry-forward/override capabilities only after PO approval. | Do not map to ProjectPaymentRequest.Approve or any approval key. No automatic copying to the new preparer keys. |
| users.write | Keep as a temporary compatibility path for user directory reads and account profile create/update only. Do not map it to Users.SetStatus, Users.AssignRoles, UserPermissions.Manage, password reset, or role mutations. | Retire after client and persisted-grant review. | Manual role-by-role decision; preserve delegation cap. |
| settings.write | Settings.ManageTenantConfiguration and ReferenceData.Manage. Platform settings remain platform-only. | Temporary alias, then retire after mapping. | No automatic copy; tenant grants never become platform grants. |
| Program.CreateAcrossPortfolios | New key; no old key replacement. | Add only after approval, catalog registration, UI display, and server enforcement are implemented together. | Grant only to explicitly approved users/roles; no pmo.admin or Program Manager default. |
| ProjectPaymentRequest.Approve | New approval-only key; no old key replacement. | Add only after PO approval and assigned-review policy is ready. | Never derive from finances.write. |
| Other new ProjectAnnualBudget.* and ProjectPaymentRequest.* keys | New operation-specific keys. | Additive rollout after endpoint map and screen View dependency are approved. | No automatic broad-grant copy; role owners classify preparers vs reviewers. |
| New Portfolio/Program operation keys | Program.Create/Edit/Archive/Reactivate and Portfolio.Create/Edit/Archive/Reactivate. | Replace Manage after staged dual support and manual review. | Do not clone Program.Manage or Portfolio.Manage to every operation. |
| New User/Settings action keys | Users.ManageAccounts, Users.SetStatus, Roles.Manage, Users.AssignRoles, UserPermissions.Manage, Settings.ManageTenantConfiguration, ReferenceData.Manage. | Replace broad write keys after service and policy consumers are separated. | Manual mapping; status, role and direct-user grants are reviewed independently. |
| New financial screen View keys | ProjectAnnualBudget.View, ProjectPaymentRequest.View, ProjectAnnualBudgetClosing.View. | Introduce with filtered read access and dependent action rules. | Do not grant to all finance writers by default. |

## Safe rollout sequence

### 1. Read-only access and consumer inventory

Before any future mutation, export per tenant:

- Permission records and stable IDs.
- RolePermission rows and role state (system/custom, active/deleted).
- UserRole assignments.
- Direct per-user Allow/Deny overrides.
- Effective permission claims for representative users.
- Authorization policy aliases and service-level literal checks.
- Frontend route/control key references.
- External API clients, scheduled jobs, reflection/dynamic checks, and historical audit references.

Compare these sources; do not infer runtime grants from SeedDataProvider.

### 2. Approve a mapping manifest

For every old key and tenant role, record:

- Old Permission ID/key and proposed target key(s).
- Whether the old grant currently has an effective backend, frontend, or API effect.
- Whether the target mapping preserves, narrows, or expands access.
- Role owner and approval reference.
- Expected changes in screen visibility and operation authority.
- Rollback mapping and compatibility expiry.

One-to-many broad mappings stay unmapped unless the role owner explicitly selects each needed action. A grant expansion requires separate explicit approval.

### 3. Add keys and preserve old data

Add new catalog keys and bilingual labels without deleting old Permission or RolePermission records. Do not change stable key names in place or reuse permission IDs for a different meaning. Catalog metadata may be code/DTO-backed or persisted; the choice must be reviewed before implementation because the current Permission entity has no Module/Screen fields.

### 4. Dual compatibility and controlled cutover

Keep old keys readable for a defined compatibility window. Where both old and new policy paths are temporarily accepted, the old path must preserve only the old capability and must never imply newly sensitive grants. For the sensitive Payment Request approval change, cut over only after approved reviewer grants are mapped; do not leave a generic finances.write-only path as an alternate approval gate after target enforcement.

Apply screen View and Action dependencies at save time. Existing roles with Action but no View are flagged for manual review; adding View may expose records and is not an automatic migration.

### 5. Claims, sessions, and caches

Phase 01 source review reports UserPermissionClaimsMiddleware recalculates effective permission claims from current role/direct-grant data per request rather than trusting stale permission claims embedded in long-lived JWTs. Confirm that behavior at implementation time. Invalidate or refresh:

- API effective-claim caches, if any are introduced.
- Frontend auth/permission context.
- Role catalog and role detail query caches.
- User permission query caches.
- Existing browser sessions after a grant removal or narrowing.

A permission change must not appear saved in the UI while the server still evaluates stale claims.

### 6. Historical records and audit

Role/permission updates must be audited with actor, tenant, role/user, before/after Permission IDs, reason, and timestamp. Historical workflow decisions continue to reference the original approver and immutable approval-plan snapshot. Historical audit should display the old key/label as recorded or provide a versioned label lookup; do not rewrite old events to the new key.

### 7. Rollback

Retain old permission rows and mapping manifests through the rollback window. Rollback restores previous policy/route compatibility and effective grants from the approved ledger; it does not reset roles or restore from a blanket seed. Any additive grant migration must be reversible by exact tenant/role/key mapping. No data deletion is part of this plan.

## Explicit expansion and loss risks

- Mapping projects.delete to Project.Delete may newly enable deletion because the old key had no active check found.
- Adding Project View to an old Action-only role may widen read access.
- Removing finances.write from dedicated approvals while keeping the operation key can enable an existing key holder previously blocked by the extra conjunction; verify actual grants and assigned-review eligibility first.
- Copying finances.write, users.write, settings.write, Program.Manage, Portfolio.Manage, or any Manage umbrella into all child permissions would expand privileges and is prohibited.
- Retiring a legacy key before external/API/reflection and persisted-grant review can break access or leave misleading dormant grants.

## Financial ceiling Change Request mapping

The lifetime ceiling is created with the Project and may change only through an approved Project Change Request. Project.Edit does not authorize the change. Keep ChangeRequest.Manage for the CR request lifecycle. For a CR with BudgetChangeAmount, require ChangeRequest.Approve plus the new ProjectFinancialCeiling.ApproveChange and the exact assigned review step. Do not automatically map old ChangeRequest.Approve grants to ProjectFinancialCeiling.ApproveChange; the new key must be granted only after role-owner review. A separate ProjectFinancialCeiling.RequestChange key is not proposed because submitting a CR does not modify the ceiling.

## Phase 03 implementation overlay

- `PermissionCatalog` is code-backed. It contains 143 definitions: 125 active (31 View, 94 Action) and 18 inactive definitions retained for legacy references. No schema change is required for module/screen metadata.
- `UserEffectivePermissionService` filters Actions whose declared dependencies are not effective. It does not add missing View grants. Existing stored RolePermission and direct-override rows are unchanged; invalid combinations remain stored and require explicit role-owner repair before the Action becomes effective.
- `SeedDataProvider` dynamically derives the protected Admin baseline from active Tenant-scoped Administration catalog entries plus the approved tenant Portfolio/Program/Handover operations. It excludes Platform and Organization-scoped `Program.CreateAcrossPortfolios`. Ordinary startup seeding does not rewrite existing roles. The Development-only, explicitly enabled `NEXUS_RECONCILE_SEEDED_ADMIN_ROLE_PERMISSIONS=true` path targets the trusted seeded Admin identity, adds only missing mandatory grants, soft-deletes conflicting mandatory Denies, and audits the before/after key set. The 2026-11-02 reconciliation added three keys after backup verification and preserved unrelated Admin grants, membership, direct overrides, and `pmo.admin` grants. Seed mappings are not evidence of runtime assignments.
- `Program.Manage` and `Portfolio.Manage` remain inactive compatibility definitions. Fresh seeded roles that previously used these keys receive the equivalent non-sensitive operation set; existing stored roles are not rewritten. No cross-Portfolio permission is inferred from either broad key.
- `Roles.Manage` remains as an inactive compatibility key; effective permissions exclude it. `users.write` alone no longer authorizes role operations; `Roles.Delete` is an explicit grant. New role-management and Administration permissions are not copied into persisted roles except through the narrow, opt-in seeded-Admin reconciliation above.
- `BudgetRevision.Approve` is a new sensitive action for the existing legacy Budget Revision approve/reject operations and custom workflow decision transitions. `finances.write` is not mapped to it; existing finance roles receive no automatic grant. The legacy entity has no per-reviewer assignment field, so deployments using this path must not assume assignment from this permission alone.
- `Users.SetStatus` is a separate status-change action. Neither `Users.ManageAccounts` nor legacy `users.write` authorizes account activation/deactivation; the dedicated action is required and status changes are audited. Do not map broad persisted grants to it automatically.
- No Development or Production database identity, backup, migration history, persisted role, permission override, cache, or user-role assignment was queried or modified. No migration was required or applied. A future grant migration must first inventory actual Dev grants and produce an exact before/after diff; approval permissions and `Program.CreateAcrossPortfolios` require explicit grants.
- Rollback for the source change is code rollback. If a future data migration is approved, it must be transactional, repeatable, auditable, tenant-scoped, and reversible from the exact role/key manifest; no blanket seed reset is allowed.

## P0 clean-slate delivery override - 2026-11-02

The staged compatibility guidance above is historical and does not authorize runtime aliases or broad-grant fan-out. The approved P0 delivery uses 154 active canonical permissions (39 View, 115 Action), no inactive compatibility keys, and one authoritative key per supported capability. Existing Development permission rows are not repaired or reconciled as a substitute for the requested database recreation; any reset still requires explicit Product Owner authorization recorded in the active task/conversation, exact target verification, the EF gate, every migration, and the full deterministic application seed and post-seed checks. Development/UAT backups are optional for resets and ordinary authorized changes. Production backup and recovery safeguards remain mandatory.

Protected tenant Admin identity is `Role.SystemRoleKey = "tenant.admin"`, a trusted set-once key independent of `Name` and `CreatedBy`. Migration `20261102190318_AddStableSystemRoleIdentity` adds that marker and a one-time, duplicate-checked conversion for active tenant system roles with the exact legacy Admin seed name. It changes no grants. New Tenant provisioning derives Admin grants from that Tenant's active catalog and mandatory baseline; it does not copy a role's grants across Tenants. Ordinary authorization has no compatibility OR fallback. Database reset, seed, persisted catalog, and runtime grant/Deny behavior remain unverified until their required gates complete.
