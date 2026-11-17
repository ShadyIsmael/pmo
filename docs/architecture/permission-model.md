# Permission and Authorization Model

**Current source contract:** 2026-11-17 (see current addendum below). This document describes the code-defined permission architecture. The existing audit evidence and working matrix remain under `docs/security/permission-audit/`; runtime and persisted database state are reported separately from source counts.

## Product Owner permission-protection reset - 2026-11-17

This section supersedes older statements below about catalog assignability, bootstrap delegation, and a protected Tenant Admin permission baseline.

- `PermissionProtectionPolicy` is the single permission-protection mechanism. It reads `Security:PermissionProtection:ProtectedPermissionKeys`; the checked-in Development default is empty because the Product Owner has not designated any protected keys. Only an active canonical key explicitly listed there is protected. Configuration rejects unknown, inactive, or duplicate keys.
- Catalog entries no longer define per-permission `IsAssignable` or `IsTenantAdminBaseline` policy. `IsAssignable` and `AllowedCategories` remain compatibility metadata only: active entries report assignable, and every selectable role category is returned. Category does not limit grants.
- Granting a permission still requires authenticated role-administration authority. Tenant/Organization ownership, scope, required View dependencies, Active Deny, self-escalation checks, and resource authorization remain enforced. Platform, cross-Tenant, and SystemOwner scopes cannot be attached to Tenant- or Organization-owned Roles.
- Tenant-owned Role grants are effective inside the authenticated Tenant. Organization-owned Role grants are effective only when the request resolves to that Role's exact owning Organization. A Project request resolves Organization scope through the persisted Project ownership graph; mismatched Project/Organization identifiers are rejected. No unscoped permission union is created.
- Direct user Allows remain limited to Tenant-scoped permissions because direct overrides carry no Organization or resource owner. Active Deny overrides remain effective and restrictive. No permission is automatically granted to a user by the policy.
- Tenant Admin and Organization Admin remain protected Role identities for role mutation, assignment, and integrity controls. Their identity no longer unions, locks, or denies any permission key. The last-effective-Tenant-Admin guard remains independent of the protection policy.
- Documents authorization uses `Document.View`, `Document.Create`, `Document.Edit`, `Document.Archive`, and `Document.TemplateManage` claims plus the existing document/project/organization ownership checks. Previously, project-scoped requests hydrated claims without first resolving their Organization, so Organization-owned Role grants were omitted; ordinary Tenant-owned Roles with null `OrganizationId` were also treated as unresolved in effective-permission resolution. The middleware now resolves Project ownership before rebuilding claims, and effective resolution treats an ordinary null-OrganizationId Role as Tenant-owned while leaving unresolved legacy Roles inactive. Focused tests cover Tenant grants, exact Organization scope, and mismatched scope.

The source change and remaining verification limits are recorded in `docs/security/permission-audit/PERMISSION-PROTECTION-POLICY-RESET-2026-11-17.md`.

## Current RBAC identity and Organization Admin addendum — 2026-11-14

This section supersedes earlier Role/System/Delegation statements below where they conflict.

- Only canonical `tenant.admin` (Tenant Admin) and `organization.admin` (Organization Admin) are System Roles. `Role.RoleKey` is the stable identity for seeded roles; `SystemRoleKey` is present only for the two System Roles. Project Manager, Portfolio Manager, Executive, PMO Admin, Finance Controller, Resource Manager, Team Member, and custom roles are Non-System. Categories remain descriptive metadata.
- Protected status is separate from System classification. Tenant Admin and Organization Admin remain protected canonical role identities, but neither receives an implicit permission union or a non-removable permission core. Active Deny applies to every permission. The last-admin safeguard preserves an active membership in the canonical Tenant Admin role and is independent of permission grants. Assignment, editability, deletion, appointment, removal, and recovery remain distinct policies; Organization Admin appointment/removal/recovery remain denied pending Product Owner policy.
- Delegation Authority is separate from Operational Permission Possession. The protected Tenant Admin may assign an eligible ordinary role and configure/delegate eligible operational permissions without gaining the target's operational access. Both user-role assignment and role/direct-override configuration still validate active catalog identity, assignability, scope, dependencies, reserved keys, protected permissions, tenant isolation, self-escalation, and Active Deny. Operational permission grants are not implicitly copied to the actor.
- Organization Admin authority is the combination of exact canonical role identity, active same-Tenant membership, a fresh effective `OrganizationConfiguration.View` or `OrganizationConfiguration.Manage` permission, and an active explicit per-user Organization settings grant. The role grants only Organization-scoped configuration authority. It does not authorize Tenant or Platform settings, unrelated Organizations, Tenant Admin mutation, financial approvals/payments, or operational work. The API rechecks the effective permission and grant at the service boundary.
- Supported Organization-scoped configuration paths currently include organization profile/settings values where the setting catalog allows an Organization override, Organization-scoped email notification profiles, Organization Phase Gate configuration, and Organization Project Lifecycle phase/status catalog configuration. Tenant-only or unscoped data models (including Platform settings and tenant-wide Organizational Unit/Workflow configuration) remain outside this Organization Admin authority. Project Lifecycle option lookup continues to require its existing project/status operational permission.
- The settings hierarchy remains **Platform → Tenant → Organization → Project → User**. Organization values may be changed only where the catalog allows an Organization override; inherited and Tenant-enforced values remain read-only. Organization Admin cannot change Tenant settings.
- The source migration `SeparateCanonicalRoleIdentityFromSystemClassification` adds RoleKey and backfills/reclassifies existing role rows in place, with collision checks and unchanged role IDs/membership/grant links by design. Its `Down` fails before DDL because restoring the old classifications would violate the approved rule. The migration has not been executed: Development database identity/history could not be verified from this execution context. No live roles, memberships, or grants were counted here.

For implementation verification, migration limitations, scenario coverage, open Product Owner decisions, and UAT steps, see `docs/security/permission-audit/RBAC-IMPLEMENTATION-FINAL-REPORT.md` and its linked evidence reports.

## Permission catalog

`apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs` is the authoritative catalog. It defines stable keys, Module filter classification, Screen/feature groups, View/Action level, English and Arabic labels/descriptions, sensitivity, scope, and action dependencies. `PermissionProtectionPolicy` alone defines protected keys. Roles & Permissions displays Screen/feature groups with action rows; Module is used for filtering and search. API DTOs project this metadata to the editor; grouping metadata is not stored as new database schema. Retired aliases are not kept in runtime compatibility metadata.

The source catalog currently contains 185 unique active keys (49 View and 136 Action). These are code counts, not persisted Permission rows, role grants, user assignments, or runtime effective access. Retired aliases are not accepted as effective permissions.

## Effective permissions and dependencies

Effective keys are resolved from assigned role grants and direct user overrides by the identity authorization service. An Active Deny removes an inherited or directly allowed key. There is no role-identity permission union or non-deniable Admin permission set. The resulting set is passed through `PermissionCatalog.EnforceDependencies`: an Action with a missing required permission is removed from the effective set. New role configurations are validated by the role-administration service and the editor selects dependencies only in its unsaved draft.

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

Project Financial Overview has its own read-only `ProjectFinancialFoundation.View` key at Project scope. Cost Utilization report reads use `Resource.View`; actual cost-rate values remain redacted unless `Resource.ViewCostRate` is effective. Export data is provided by `GET /api/resource-analytics/cost-utilization/export-data` and requires both `Resource.View` and the separate sensitive `CostUtilization.Export` key. Program and Portfolio financial rollups each require the matching parent read (`Program.View` or `Portfolio.View`) plus their separate read key (`ProgramFinancialRollup.View` at Program scope or `PortfolioFinancialRollup.View` at Portfolio scope). Both rollup services filter to the authenticated Tenant and authorized descendant Project scope. These read and export keys do not grant workflow actions.

The approved Project lifetime financial ceiling may change only through an approved Project Change Request. Project Edit rejects a changed ceiling, and ordinary Project mapping does not write it. Creating or submitting a CR does not apply its proposed ceiling. Only final approval of a CR containing a ceiling change can apply it; the final assigned reviewer must hold both `ChangeRequest.Approve` and `ProjectFinancialCeiling.ApproveChange`. The workflow rechecks Tenant/Project scope, assignment, separation of duties, that the recorded prior ceiling is still current, financial commitments and annual allocations, and writes an audit event in the approval transaction. No independent ceiling approval path exists.

Persistent Project approval defaults use `ProjectAnnualBudget.ManageApprovalConfiguration`. `ProjectApproval.Override` is separate and applies to one submission subject to the workflow's reason, reviewer eligibility, mandatory stages, and audit controls; it must not update Project Settings.

Project Settings has its own Project-scoped `ProjectSettings.View` and `ProjectSettings.Edit` permissions, grouped under the Project Settings screen. Edit depends on View. The View key opens the Header Side Sheet but does not replace each section's View key. Workflow settings writes require both Project Settings keys and an authorized mutable Project context. Financial approval configuration also retains its section View and `ProjectAnnualBudget.ManageApprovalConfiguration` checks. Direct user Deny overrides remain effective, and role grants do not bypass Tenant, Project, section, or read-only checks.

## Project Plan creation and content editing

`ProjectPlan.Create` creates a new Plan aggregate through `POST /api/projects/{projectId}/plans`; it is Project-scoped, depends on `Planning.View`, and is eligible for Management custom roles. `Planning.Edit` authorizes changes to existing Plan contents, including activities, WBS, dependencies, assignments, calendars, and importing into an existing Plan; it is Project-scoped, depends on `Planning.View`, and is eligible for Management and Operation custom roles. The standalone empty-Plan creation form requires `ProjectPlan.Create` only. Plan content controls and write APIs require `Planning.Edit`.

MPP import writes plan contents: creating an import review session or importing into an existing Plan requires `Planning.Edit`, while creating a Plan from MPP requires both `ProjectPlan.Create` and `Planning.Edit`. Creating new Resource master records from imported rows additionally requires `Resource.Create`; mapping or ignoring Resources does not. The commit service rechecks this permission before creating any Resource so a permission revoked after review cannot be used at commit. Plan creation and content editing remain distinct operations; the catalog has no separate Plan metadata edit permission because no Plan metadata edit API exists. These keys are not automatically granted to existing roles.

## Role delegation and protected administrator Roles

Role administration distinguishes `Roles.View`, `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, and `Roles.Delete`; each operation requires its exact capability. `RoleAdministrationService` binds actor and tenant arguments to the authenticated principal and checks role-operation authority, central protection policy, dependencies, ownership scope, self-escalation, and Active Deny. An authorized `Roles.ConfigurePermissions` holder can grant any active canonical permission allowed by the target Role's scope unless the Product Owner explicitly lists it in `Security:PermissionProtection:ProtectedPermissionKeys`. Role category is descriptive metadata and is not a grantability rule. System Roles cannot be renamed or deleted through ordinary role administration. Role assignment requires authenticated `Users.AssignRoles` authority in each changed Tenant/Organization scope.

Tenant Admin and Organization Admin protected status applies to their canonical Role identities and lifecycle safeguards. It does not provide operational permission grants, prevent Deny, or prevent permission removal. `TenantAdminMutationGuard` uses the independent active Tenant Admin membership check and a SQL Server application lock to prevent concurrent changes from removing the final active canonical Tenant Admin assignment. The check does not require a hardcoded permission baseline. Existing Role IDs, memberships, and grants are not rewritten by this policy change.

## Compatibility, seeds, and persistence

The source catalog currently has 185 active entries. Retired aliases such as `finances.write`, `projects.read`, `Roles.Manage`, and `ReferenceData.Manage` are historical examples only; they are absent from active authorization code and cannot authorize requests or be selected as role grants. Workflow permission checks use fresh effective permission claims; API source does not treat `pmo.admin`, `admin`, or `SystemAdministrator` role-name membership as an authorization substitute.

Tenant provisioning does not copy grants from another Tenant. Seed defaults are distinct from runtime authorization: this policy change does not alter existing Role grants, re-add removed grants, or assign operational permissions to users. The 2026-11-02 P0 delivery adds a stable system-role identity through an EF migration; its one-time backfill assigns `tenant.admin` to uniquely identified, active tenant system roles with the exact legacy Admin seed name, and runtime checks use only the stable key. Every Development/UAT operation still requires exact environment/target checks, migration readiness, and required schema validation. Production database operations remain outside the permitted write scope.

Catalog grouping metadata remains code-backed and requires no new database columns. The separate SystemRoleKey model change is persisted by its EF migration. Before Development/UAT schema changes, verify the environment and database identity, migration history, model snapshot, and reviewed migration SQL; a backup is optional and is not a prerequisite. A database reset is permitted only through the narrowly scoped Product Owner-authorized `Shady/NexusPmo` reset procedure in the [EF Core database workflow](../development/ef-core-database-workflow.md); it must still apply all EF migrations and run the full seed. Production backup and recovery requirements remain unchanged.

## Source and delivery references

- Code catalog: `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`
- Role configuration: `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationService.cs`
- Management scope: `apps/api/src/NEXUS.Persistence/Management/ManagementScopeResolver.cs`
- Final matrix: `docs/security/PERMISSIONS-FINAL-TARGET-MATRIX.csv`
- API mapping: `docs/security/PERMISSIONS-CANONICAL-API-MAPPING.md`
- Migration constraints: `docs/security/PERMISSIONS-MIGRATION-STRATEGY.md`
- Implementation and verification status: `docs/security/PERMISSIONS-IMPLEMENTATION-REPORT.md`

## Role Category as descriptive metadata - 2026-11-17

Role categories remain descriptive composition metadata for selectable custom roles (Tenant, Organization, Management, Operation, and Business). System remains reserved for protected system roles. Category eligibility no longer filters or rejects ordinary Tenant-, Organization-, or Project-scoped permission grants. The Roles & Permissions catalog DTO may retain AllowedCategories for compatibility, but it returns all selectable custom-role categories and is not an authorization boundary.

Permission assignment requires `Roles.ConfigurePermissions` and remains bounded by active catalog status, the central Product Owner protection policy, declared dependencies, tenant/role ownership, resource scope, Active Deny, protected-role lifecycle rules, and platform/global boundaries. Category does not affect grantability. A role category change does not force removal of grants. No grants are added automatically.

Category may still be used by independent business eligibility rules. Management-category manager eligibility and System/Management Annual Budget reviewer eligibility remain separate domain rules and are not permission grant restrictions. Do not reintroduce category-based grant eligibility until the Product Owner explicitly approves it.

## Project Phase and Status catalog permissions - 2026-11-11

Project Phase and Project Status are separate concepts and catalogs. The tenant-scoped Reference Data catalogs use `ProjectLifecyclePhase.View/Create/Edit/ActivateDeactivate` and `ProjectLifecycleStatus.View/Create/Edit/ActivateDeactivate`. Edit covers localized labels/descriptions, display order, valid default selection, and configurable transition rules. Activate/deactivate preserves stable IDs and historical project references; there is no hard-delete permission. These Administration-module catalog permissions are grantable under the default-open policy, subject to authenticated authority, dependencies, and Tenant/resource scope.

Project mutations remain project-scoped: `ProjectPhase.Change` is the existing sensitive permission for the governed Phase Gate workflow and must continue to require server-side gate validation; `ProjectStatus.Change` authorizes separately configured status transitions and depends on `Project.View`. Phase and Status changes remain separate from Health. Active Deny and Tenant/Organization/Project boundaries remain enforced by the ordinary effective-permission and resource-scope checks.

The minimal `ProjectLifecycle.OptionsLookup` policy allows `Project.Create` or `Project.Edit` to retrieve only the active options needed by the requested project operation. It does not grant catalog listing or management. Permission catalog declarations, named API policies, frontend constants, and focused contract tests live in `PermissionCatalog.cs`, `Program.cs`, `permissions.ts`, and `ProjectLifecyclePermissionContractTests.cs`; persisted grants and authenticated runtime behavior are verified separately.
