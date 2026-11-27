# Permission and Authorization Model

**Current source contract:** 2026-11-08. This document describes the current code-defined permission architecture. The existing audit evidence and working matrix remain under `docs/security/permission-audit/`; runtime and persisted database state are reported separately from source counts.

## Permission catalog

`apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs` is the authoritative catalog. It defines stable keys, Module filter classification, Screen/feature groups, View/Action level, English and Arabic labels/descriptions, sensitivity, scope, assignability, action dependencies, and the protected tenant Admin baseline flag. Roles & Permissions displays Screen/feature groups with action rows; Module is used for filtering and search. API DTOs project this metadata to the editor; grouping metadata is not stored as new database schema. Retired aliases are not kept in runtime compatibility metadata.

The source catalog currently contains 174 unique active keys (46 View and 128 Action). These are code counts, not persisted Permission rows, role grants, user assignments, or runtime effective access. Retired aliases are not accepted as effective permissions.

## Effective permissions and dependencies

Effective keys are resolved from assigned role grants and direct user overrides by the identity authorization service. Deny overrides normally remove a key. For a user assigned the trusted protected tenant System Admin role, the active mandatory Admin baseline is applied after overrides, so a Deny cannot remove a required baseline permission; a conflicting persisted Deny is logged and does not reduce the effective baseline. The resulting set is passed through `PermissionCatalog.EnforceDependencies`: an Action with a missing required permission is removed from the effective set. New role configurations are validated by the role-administration service and the editor selects dependencies only in its unsaved draft.

Approval-only access is a narrow exception to ordinary same-screen View dependencies. `ChangeRequest.Approve`, `ProjectAnnualBudget.Approve`, `ProjectPaymentRequest.Approve`, and `ProjectPaymentReversal.Approve` authorize an assigned workflow decision, not broad list access. `ProjectFinancialCeiling.ApproveChange` additionally depends on `ChangeRequest.Approve` and applies only to the final approval step of a CR that changes the lifetime ceiling. Each workflow API must verify the exact pending assignment, eligible reviewer, scope, current workflow state, separation of duties, and audit requirements.

## Capability, management scope, and workflow assignment

Authorization combines three independent questions:

1. **Capability:** does the actor hold the required permission key?
2. **Management/data scope:** does the actor have authority over this Tenant, Organization, Portfolio, Program, Project, or resource?
3. **Workflow assignment:** is the actor the eligible reviewer/approver for this exact workflow step?

A permission key does not create resource assignments or widen scope. An assigned reviewer can receive one narrowly filtered record through an approval-specific endpoint; that does not grant access to the normal list or unrelated financial records. Normal read and write APIs continue to apply tenant and resource scope.

### Project creation contextual hierarchy lookups

| Project action | Permission | Data returned and scope |
|---|---|---|
| Select a Portfolio or Program while creating a Project | `Project.Create` alone is sufficient; the existing contextual lookup policies also retain their source View and Project Edit paths | Dedicated Project lookup endpoints return only minimal selection fields for the authenticated Tenant and Organizations authorized by the user's management scope and Organization-owned role memberships. A Program lookup can be narrowed by the selected Portfolio. |
| Browse the Portfolio module | `Portfolio.View` | Existing Portfolio list/detail APIs and module routes remain independently protected and scoped. `Project.Create` does not grant module browsing. |
| Browse the Program module | `Program.View` | Existing Program list/detail APIs and module routes remain independently protected and scoped. `Project.Create` does not grant module browsing. |
| Create the Project with the selected hierarchy | `Project.Create` | The server verifies the selected Portfolio/Program exists in the Tenant, that a Program belongs to the supplied Portfolio, and that the resulting owning Organization is active and within the actor's authorized Organization scope. A Project may be assigned directly to a Portfolio with no Program; the owning Organization's default Pre-Initiation phase is assigned automatically. |

These contextual lookups do not add permission keys or grant Portfolio/Program module access. General module reads continue to use their independent View permissions.

The authenticated tenant boundary is derived from trusted identity/context. `ManagementScopeResolver` derives Portfolio, Program, Project, and Organization scope from native assignments and explicit tenant-scoped administrative authority. Organization scope is not a substitute for native manager assignment on a record. Cross-Portfolio Program creation requires `Program.Create` plus the explicit `Program.CreateAcrossPortfolios` capability and an eligible Portfolio inside the actor's authorized Tenant/Organization scope. It does not create a lasting manager assignment.

Platform administration is separate from Tenant administration. Tenant-wide management scope requires trusted, explicitly scoped authority (`TenantAdministration.ManageScope`) and remains within that Tenant. Neither the `admin` nor `pmo.admin` role name grants platform or tenant-wide authority by itself; there is no automatic equivalence between those role names.

Organization settings have their own per-user, per-Organization grants. Platform Settings use platform policies and are not assignable through Tenant Roles & Permissions.

## Financial authorization and CR-only ceiling changes

Annual Budgets, Payment Requests, Settlements/Reversals, and Fiscal Year Closing have separate canonical screen and action keys. There are no active `finances.read` or `finances.write` aliases or broad OR fallbacks. Each operation authorizes against its definitive action key; related source rollups require every source View permission explicitly.

`ProjectFinance.View` is the canonical **Project Finance → Open Finance** permission for the Project Details Financial Tab and its direct route aliases. It controls tab visibility and entry. Budget, Cost, Commitment, Payment Obligation, and financial action APIs continue to require their own permissions and enforce their existing Tenant/Project scope. `ProjectFinancialFoundation.View` remains a distinct read permission for the financial foundation/settings data flow; `ProjectSettings.View` applies to that settings flow and does not gate opening the Financial Tab.

Project Financial Overview has its own read-only `ProjectFinancialFoundation.View` key at Project scope and appears under the **Project Financial Management** module in Roles & Permissions. Cost Utilization report reads use `Resource.View`; actual cost-rate values remain redacted unless `Resource.ViewCostRate` is effective. Export data is provided by `GET /api/resource-analytics/cost-utilization/export-data` and requires both `Resource.View` and the separate sensitive `CostUtilization.Export` key. Program and Portfolio financial rollups each require the matching parent read (`Program.View` or `Portfolio.View`) plus their separate read key (`ProgramFinancialRollup.View` at Program scope or `PortfolioFinancialRollup.View` at Portfolio scope). Both rollup services filter to the authenticated Tenant and authorized descendant Project scope. These read and export keys do not grant workflow actions and are not added to the protected Admin baseline.

The approved Project lifetime financial ceiling may change only through an approved Project Change Request. Project Edit rejects a changed ceiling, and ordinary Project mapping does not write it. Creating or submitting a CR does not apply its proposed ceiling. Only final approval of a CR containing a ceiling change can apply it; the final assigned reviewer must hold both `ChangeRequest.Approve` and `ProjectFinancialCeiling.ApproveChange`. The workflow rechecks Tenant/Project scope, assignment, separation of duties, that the recorded prior ceiling is still current, financial commitments and annual allocations, and writes an audit event in the approval transaction. No independent ceiling approval path exists.

Persistent Project approval defaults use `ProjectAnnualBudget.ManageApprovalConfiguration`. `ProjectApproval.Override` is separate and applies to one submission subject to the workflow's reason, reviewer eligibility, mandatory stages, and audit controls; it must not update Project Settings.

Project Settings has its own Project-scoped `ProjectSettings.View` and `ProjectSettings.Edit` permissions, grouped under the Project Settings screen. Edit depends on View. The View key opens the Header Side Sheet but does not replace each section's View key. Workflow settings writes require both Project Settings keys and an authorized mutable Project context. Financial approval configuration also retains its section View and `ProjectAnnualBudget.ManageApprovalConfiguration` checks. These Project-scoped keys are outside the dynamic Tenant Admin baseline; the identity seed adds them to the stable `tenant.admin` role. Direct user Deny overrides remain effective, and the Admin grant does not bypass Tenant, Project, section, or read-only checks.

### Sprint 2 Planned Payment foundation

The Planned Payment foundation reuses the existing canonical `PaymentObligation.*` keys; no new permission key, catalog row, role grant, or System Role is introduced. The keys already describe the same planned-payment-obligation capability and are already registered as API policies and exposed in Roles & Permissions with EN/AR labels. Reuse avoids an ungranted parallel key while keeping the existing Active Deny, dependency, scope, and role-administration behavior intact.

| Operation | Required keys | Additional server checks |
| --- | --- | --- |
| Open the Planned Payment area in the Financial Tab | `ProjectFinance.View` for the tab and `PaymentObligation.View` for the section | Authenticated user; Management Tenant/Project scope; Active Deny removes access. |
| Retrieve budget, list, or detail | `PaymentObligation.View` | Project row must exist in the authenticated Tenant and current project scope. |
| Register a Planned Payment | `PaymentObligation.Create` | Current Project Manager; active same-Tenant actor; writable management context; project-row lock; lifetime-budget availability; configured matching currency. |
| Amend an unlocked Planned Payment | `PaymentObligation.Edit` | Current Project Manager; row version; project-row lock; no first Payment Request; lifetime-budget availability; matching configured currency. |
| First Payment Request locks the plan | `ProjectPaymentRequest.Create` on the request path | Linked installment belongs to the same Tenant/Project; no overcommitment; lock and audit are committed in the existing request transaction. No Planned Payment-specific approval permission is added. |

The seeded `tenant.project-manager` role is not automatically granted any additional key. An actor with Project Manager assignment but without the canonical grant can read the Financial Tab shell but cannot load or write Planned Payments; the UI hides the section/action and direct API calls fail closed. Approval remains authorization only: it never creates a lifetime reservation or increases Actual Paid.

### Sprint 2 Payment Request and Budget Line integration

Creating a Payment Request requires `ProjectPaymentRequest.Create`; changing a draft or returned request requires `ProjectPaymentRequest.EditDraft`; submission requires `ProjectPaymentRequest.Submit` and the requester must still be the current Project Manager. Approval requires `ProjectPaymentRequest.Approve`, an exact active assignment in the current immutable cycle, current Project Team eligibility, effective permission, tenant/project scope, and a requester/approver separation check. The assigned approval inbox grants no general Payment Request list access.

Payment Requests require one same-Project Planned Payment installment, one approved/open Annual Budget for the selected fiscal year, and exactly one Budget Line belonging to that budget. Installment, Annual Budget, and Budget Line limits are revalidated under the workflow transaction; approver visibility and line selection never grant authority. A new Project-scoped `ProjectAnnualBudget.ManageLines` key configures lines on approved budgets. Draft lines can use `ProjectAnnualBudget.EditDraft` or `ProjectAnnualBudget.ManageLines`; approved lines require `ProjectAnnualBudget.ManageLines`. This key depends on `ProjectAnnualBudget.View`, is outside the protected Tenant Admin baseline, and is not granted automatically to existing roles. Active Deny and Management Tenant/Project scope continue to apply.

| Operation | Required keys | Additional server checks |
| --- | --- | --- |
| Read Annual Budget Lines | `ProjectAnnualBudget.View` | Budget belongs to the authorized Tenant and Project; amounts and active reservations come from persisted records. |
| Create/update/delete a line on an open Draft budget | `ProjectAnnualBudget.EditDraft` or `ProjectAnnualBudget.ManageLines` | Writable Project scope; no pending budget review/revision; line total cannot exceed the budget; referenced lines cannot be mutated or deleted. |
| Create/update/delete a line on an open Approved budget | `ProjectAnnualBudget.ManageLines` | Same Tenant/Project and explicit current effective grant; no pending budget review/revision; line total cannot exceed the budget; referenced lines remain immutable. |
| Create a Payment Request draft | `ProjectPaymentRequest.Create` | Current Project Manager, positive amount, same-Project installment, matching currency and available installment amount; first request locks the plan. |
| Submit or resubmit | `ProjectPaymentRequest.Submit` | Requester remains current Project Manager; approved/open selected budget and its single line; current eligible configured approvers; Annual Budget, line, and installment availability rechecked atomically. Lifetime reservation is not increased. |
| Decide an approval | `ProjectPaymentRequest.Approve` | Exact pending assignment, eligible active user, effective permission, current cycle, no self-approval, full amount only; final approval does not increase Actual Paid. |

The user preferences and in-app notification entry identify the assigned project approval step; delivery is idempotent by Tenant, request, cycle, assignment, and recipient. No new financial role or automatic grant is introduced.

## Project Plan creation and content editing

`ProjectPlan.Create` creates a new Plan aggregate through `POST /api/projects/{projectId}/plans`; it is Project-scoped, depends on `Planning.View`, and is eligible for Management custom roles. `Planning.Edit` authorizes changes to existing Plan contents, including activities, WBS, dependencies, assignments, calendars, and importing into an existing Plan; it is Project-scoped, depends on `Planning.View`, and is eligible for Management and Operation custom roles. The standalone empty-Plan creation form requires `ProjectPlan.Create` only. Plan content controls and write APIs require `Planning.Edit`.

MPP import writes plan contents: creating an import review session or importing into an existing Plan requires `Planning.Edit`, while creating a Plan from MPP requires both `ProjectPlan.Create` and `Planning.Edit`. Creating new Resource master records from imported rows additionally requires `Resource.Create`; mapping or ignoring Resources does not. The commit service rechecks this permission before creating any Resource so a permission revoked after review cannot be used at commit. Plan creation and content editing remain distinct operations; the catalog has no separate Plan metadata edit permission because no Plan metadata edit API exists. None of these keys is added to the protected Tenant Admin baseline or automatically granted to existing roles.

## Role delegation

Role administration distinguishes `Roles.View`, `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, and `Roles.Delete`; each operation requires its exact capability. `TenantAdministration.ManageScope` does not substitute for those capabilities and is an additional prerequisite only for the separate bootstrap delegation policy. The retired `users.write` key is not accepted by any role policy or service. `RoleAdministrationService` binds actor and tenant arguments to the authenticated principal and checks role-operation authority and tenant/role ownership. For user-managed custom roles, ordinary active, assignable, scoped non-Administration permissions may be granted or revoked regardless of actor possession or target category. Administration-module permissions remain actor-authorized only. A role assigned to the actor cannot be changed to add effective permissions beyond the actor's current set. System roles cannot be deleted or renamed, and roles with user assignments cannot be deleted through the current service.

Catalog `IsAssignable` metadata does not itself authorize a grant. For a user-managed custom role, an authorized `Roles.ConfigurePermissions` holder can configure any active, assignable, scoped non-Administration key without holding it; the server still checks dependencies, Active Deny, tenant/resource scope, protected-role rules, and self-escalation. Administration-module keys remain actor-authorized only. The earlier actor-possession ceiling is superseded for ordinary custom-role permissions. The 21-key `BootstrapPermissionDelegationPolicy` list is not a ceiling for ordinary grants. The protected tenant Admin exception remains limited to its exact 13 Annual Budget/Closing keys; `ProjectAnnualBudget.ConfigureDraftApproval` is excluded, and other system-managed roles do not receive bootstrap-only grants. No grant is automatic.

The separate bootstrap and protected-Admin paths remain explicit and do not add grants automatically or change the mandatory Admin baseline. Other role-administration safeguards, including the last-administrator check and platform/global boundaries, remain enforced.

See `docs/security/PERMISSIONS-ROLE-DELEGATION.md` for the implementation rule and `PERMISSIONS-TWO-LEVEL-UX.md` for the editor behavior.

## Protected tenant Admin baseline

The mandatory baseline dynamically includes all active Tenant-scoped catalog entries in the `administration` module or with `IsTenantAdminBaseline` set, plus the approved tenant Portfolio (`View/Create/Edit/Archive/Reactivate`), Program (`View/Create/Edit/Archive/Reactivate`), and Handover (`View/Manage/Approve/AdminInitiate`) keys. It excludes Organization-scoped `Program.CreateAcrossPortfolios` and all Platform, CrossTenant, and SystemOwner permissions. Portfolio and Program initial manager assignment requires `Handover.AdminInitiate`. Protected tenant Admin identity is the immutable, set-once `Role.SystemRoleKey = tenant.admin`, assigned only by trusted seed/provisioning code to a system role in its Tenant. Runtime authorization does not use the role display name or `CreatedBy` to identify the protected role. Role permission replacement cannot omit mandatory keys; protected System roles cannot be deleted. A serializable tenant guard with a SQL Server application lock prevents concurrent access changes from removing the last effective tenant administrator.

## Compatibility, seeds, and persistence

The source catalog currently has 174 active entries. Retired aliases such as `finances.write`, `projects.read`, `Roles.Manage`, and `ReferenceData.Manage` are historical examples only; they are absent from active authorization code and cannot authorize requests or be selected as role grants. Workflow permission checks use fresh effective permission claims; API source does not treat `pmo.admin`, `admin`, or `SystemAdministrator` role-name membership as an authorization substitute.

New tenant Admin roles receive the catalog-derived mandatory baseline from that Tenant's own permission rows. Tenant provisioning does not copy grants from another Tenant. Repeated identity seeding adds missing mandatory grants to the keyed protected Admin role without changing other roles or removing unrelated Admin grants. The source-defined current baseline contains 48 keys. The 2026-11-02 P0 delivery adds a stable system-role identity through an EF migration; its one-time backfill assigns `tenant.admin` to uniquely identified, active tenant system roles with the exact legacy Admin seed name, and runtime checks use only the stable key. The requested Development rebuild is a clean EF reset followed by the deterministic application seed; it does not patch legacy permission rows or fan out historical broad grants. Every Development/UAT operation still requires exact environment/target checks, migration readiness, and required schema validation; backups are optional, including for the explicit Product Owner-authorized Development `Shady/NexusPmo` reset. The reset remains destructive and requires that authorization, all EF migrations, the full seed, and post-seed verification. Production backup and recovery requirements remain mandatory.

Catalog grouping metadata remains code-backed and requires no new database columns. The separate SystemRoleKey model change is persisted by its EF migration. Before Development/UAT schema changes, verify the environment and database identity, migration history, model snapshot, and reviewed migration SQL; a backup is optional and is not a prerequisite. A database reset is permitted only through the narrowly scoped Product Owner-authorized `Shady/NexusPmo` reset procedure in the [EF Core database workflow](../development/ef-core-database-workflow.md); it must still apply all EF migrations and run the full seed. Production backup and recovery requirements remain unchanged.

## Source and delivery references

- Code catalog: `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`
- Role configuration: `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationService.cs`
- Management scope: `apps/api/src/NEXUS.Persistence/Management/ManagementScopeResolver.cs`
- Final matrix: `docs/security/PERMISSIONS-FINAL-TARGET-MATRIX.csv`
- API mapping: `docs/security/PERMISSIONS-CANONICAL-API-MAPPING.md`
- Migration constraints: `docs/security/PERMISSIONS-MIGRATION-STRATEGY.md`
- Implementation and verification status: `docs/security/PERMISSIONS-IMPLEMENTATION-REPORT.md`

## Role Category as descriptive metadata; grant eligibility suspended — 2026-11-09

Role categories remain descriptive composition metadata for selectable custom roles (Tenant, Organization, Management, Operation, and Business). System remains reserved for protected system roles. Category eligibility no longer filters or rejects ordinary Tenant-, Organization-, or Project-scoped permission grants. The Roles & Permissions catalog DTO may retain AllowedCategories for compatibility, but it returns all selectable custom-role categories and is not an authorization boundary.

Permission assignment requires `Roles.ConfigurePermissions` and remains bounded by active and assignable catalog status, declared dependencies, tenant/role ownership, resource scope, Active Deny, protected-role rules, and platform/global boundaries. Ordinary non-Administration permissions on user-managed custom roles are not capped by actor possession or Role Category. `TenantAdministration.ManageScope` is an additional prerequisite only for the separate bootstrap delegation policy. A role category change does not force removal of grants. No grants are added automatically.

Category may still be used by independent business eligibility rules. Management-category manager eligibility and System/Management Annual Budget reviewer eligibility remain separate domain rules and are not permission grant restrictions. Do not reintroduce category-based grant eligibility until the Product Owner explicitly approves it.
