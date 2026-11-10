# Role Permission Delegation Policy

**Status (2026-11-02):** Protected Admin and last-administrator safeguards are implemented; focused permission/security tests passed (38/38), seed tests passed (10/10), and Portfolio/Program UI tests passed (12/12). `Shady/NexusPmo` was inspected; a verified backup preceded the narrowly audited Admin reconciliation.

## Server-side rules

The role editor is a convenience layer. `RoleAdministrationService` is authoritative for every create, detail update, permission configuration, and delete operation.

| Operation | Required capability | Additional server checks |
|---|---|---|
| View roles and role details | `Roles.View` | Active actor and role are in the authenticated Tenant. The actor may inspect a role with ordinary grants beyond the permissions currently effective for the actor; viewing does not permit changing those grants. Permission counts and details include only active permission rows from that Tenant. |
| Create a role | `Roles.Create` | Tenant ownership; if the create request also configures permissions, it additionally requires `Roles.ConfigurePermissions`. Validate each permission using the target-role rules below; reject cross-Tenant IDs and protected/system-managed role mutations. |
| Edit role details | `Roles.EditDetails` | Tenant/role ownership; reject protected system-role identity changes. |
| Configure role permissions | `Roles.ConfigurePermissions` | Tenant and role ownership; active, assignable canonical keys only; enforce dependencies, Active Deny, scope, protected-role and platform boundaries. For user-managed custom roles, ordinary non-Administration permissions do not require actor possession. Administration-module permissions remain actor-authorized only. If the target role is assigned to the actor, additions must not expand the permissions currently effective for the actor beyond the current set. Audit before/after keys. |
| Delete a role | `Roles.Delete` | No `Roles.Manage` or `users.write` fallback; reject system roles and roles with active user assignments. |

Each operation requires its exact `Roles.*` capability shown above. `users.read`, `users.write`, inactive `Roles.Manage`, and `TenantAdministration.ManageScope` do not substitute for those capabilities. `TenantAdministration.ManageScope` is resolved from the active actor's effective tenant permission set, including declared permission dependencies; it is an additional prerequisite only for the separately defined `BootstrapPermissionDelegationPolicy` path below. No role name proves authority.

Before the canonical role-operation split, `RoleAdministrationService` used `users.write` as a blanket service gate. Existing persisted roles are not automatically backfilled when the service switches to `Roles.View`, `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, and `Roles.Delete`. If a verified Development inventory finds an existing administrator role relying on the legacy gate, reconcile only the approved canonical grants through an audited recovery; do not restore a live `users.write` bypass.

## Ordinary custom-role delegation and protected-role bootstrap

For a same-Tenant user-managed custom role, an administrator authorized through `Roles.ConfigurePermissions` may grant or revoke any active, assignable, scoped non-Administration permission, even when the actor does not hold that permission. Role Category does not filter the grant. The previous actor-possession ceiling is superseded for these ordinary permissions. Administration-module permissions remain actor-authorized only. The separate 21-key `BootstrapPermissionDelegationPolicy` set is `ProjectPlan.Create`, `Planning.Edit`, `Planning.View`, `ProjectAnnualBudget.View`, `ProjectAnnualBudget.Create`, `ProjectAnnualBudget.EditDraft`, `ProjectAnnualBudget.ConfigureDraftApproval`, `ProjectAnnualBudget.SubmitForReview`, `ProjectAnnualBudget.Approve`, `ProjectAnnualBudget.ReviseApproved`, `ProjectAnnualBudget.ManageApprovalConfiguration`, `ProjectAnnualBudget.Delete`, `ProjectAnnualBudget.Withdraw`, `ProjectAnnualBudgetClosing.View`, `ProjectAnnualBudget.Close`, `ProjectAnnualBudget.CloseApprove`, `ProjectAnnualBudget.CarryForward`, `ProjectFinancialFoundation.View`, `CostUtilization.Export`, `ProgramFinancialRollup.View`, and `PortfolioFinancialRollup.View`. That separately defined bootstrap path is not the ceiling for ordinary custom-role grants. When this special path applies, it still requires effective `Roles.ConfigurePermissions` and `TenantAdministration.ManageScope` in the authenticated Tenant, respects Active Deny, dependencies and scope, and grants nothing automatically.

The protected tenant Admin has a separate 13-key `ProtectedAdminAnnualBudgetPermissionKeys` exception: `ProjectAnnualBudget.View`, `ProjectAnnualBudget.Create`, `ProjectAnnualBudget.EditDraft`, `ProjectAnnualBudget.SubmitForReview`, `ProjectAnnualBudget.Approve`, `ProjectAnnualBudget.ReviseApproved`, `ProjectAnnualBudget.ManageApprovalConfiguration`, `ProjectAnnualBudget.Delete`, `ProjectAnnualBudget.Withdraw`, `ProjectAnnualBudgetClosing.View`, `ProjectAnnualBudget.Close`, `ProjectAnnualBudget.CloseApprove`, and `ProjectAnnualBudget.CarryForward`. It excludes `ProjectAnnualBudget.ConfigureDraftApproval` and the four additional finance keys. Bootstrap-only grants cannot be added to other system-managed roles. The protected Admin mandatory Administration baseline remains locked; last-administrator protection remains active, and Deny remains effective for non-baseline finance permissions. The separate user-role assignment fallback still targets another user and a same-Tenant non-system recipient role. Ordinary system-role assignment remains available when the actor already holds every permission in that role.

## User administration operations

| Operation | Required capability | Enforcement |
|---|---|---|
| Create account or edit profile fields | `Users.ManageAccounts` | `UsersController` policy and `UserAdministrationService`; legacy `users.write` remains a temporary profile/read compatibility path only. |
| Activate/deactivate account | `Users.SetStatus` | Exact key required by `UserAdministrationService`; no `users.write` or `Users.ManageAccounts` alias. Self-status changes are rejected and status transitions are audited. |
| Assign/remove user roles | `Users.AssignRoles` | Exact key required for role changes; update endpoint can be used by role-only actors, while service prevents profile/status edits and applies delegation checks. |
| Manage direct per-user permission overrides | `UserPermissions.Manage` | Exact key required; target tenant is checked and the actor cannot grant a permission beyond their effective authority. |
| Reset a local user's password | `users.reset-password` | Separate password reset endpoint and policy. |

The shared user update endpoint accepts account, status, and role fields, but the service authorizes each changed field group independently. Frontend controls mirror those boundaries. The protected System Admin receives every active tenant-scoped Administration permission plus the approved tenant Portfolio, Program, and Handover operations. Mandatory grants cannot be removed or denied through the API or Roles UI.

## No self-escalation

- Permission changes are bounded by role-administration authority, Tenant/role ownership, active and assignable keys, target-role eligibility, declared dependencies, Active Deny, and resource scope. The actor effective permission set is not the grant ceiling for ordinary non-Administration permissions on user-managed custom roles.
- No self-escalation: a role assigned to the actor cannot be changed to add an effective permission beyond the permissions currently effective for the actor. The actor also cannot alter their own role membership through role-assignment operations.
- `TenantAdministration.ManageScope` does not replace any exact role-operation capability. It is additionally required only on the separately defined bootstrap path. The service rechecks effective administrator capabilities after changes and uses the serializable tenant transaction/application lock to prevent removal of the last effective tenant administrator. This does not restore the actor-possession ceiling for ordinary custom-role grants.
- Actions with missing dependencies are rejected for new role configurations. Runtime resolution removes invalid Actions from the effective set; it does not silently grant a missing View.
- Inactive compatibility keys are not offered as new grants. Existing persisted references are retained until a reviewed migration.
- User role assignment and direct user Allow/Deny overrides remain separate capabilities (`Users.AssignRoles`, `UserPermissions.Manage`) and require their own server checks and audits.

## Preservation and audit

New protected Admin roles receive the dynamic mandatory baseline. Ordinary startup seeding does not rewrite existing RolePermission rows. An explicit Development-only reconciliation adds missing mandatory Admin grants, soft-deletes only conflicting mandatory Deny overrides, and audits changes; unrelated role grants, user-role links, and direct overrides are preserved. The 2026-11-02 verified Development reconciliation added three grants to the seeded Admin and left the 65 `pmo.admin` grants unchanged; details and backup verification are in the [implementation report](PERMISSIONS-IMPLEMENTATION-REPORT.md). No migration was created or applied. Never fan out a broad legacy grant into approval, settlement, reversal, closing, or financial-ceiling permissions.

Role permission changes audit actor, Tenant, role, and before/after permission keys. The Admin reconciliation wrote a Tenant-scoped compatibility-grant audit record with the before/after sets; focused tests cover grant and rejection audit behavior. Portfolio and Program initial manager assignment requires `Handover.AdminInitiate`; CRUD permission keys alone do not allow assignment. Browser runtime UAT remains open; see the implementation report.

## Temporary Role Category grant-eligibility suspension (2026-11-09)

Role Category remains descriptive metadata for custom roles. It does not restrict ordinary custom-role permission grants. The permission-possession ceiling is removed only for active, assignable, scoped non-Administration permissions assigned to user-managed custom roles; Administration-module permissions remain actor-authorized only. Category-neutral grantability does not bypass the actor `Roles.ConfigurePermissions` authority, tenant/role ownership, Active Deny, dependencies, resource scope, protected/system-managed role rules, or no-self-escalation. The exact protected-Admin subset remains separate; no grant is automatic.

The API may continue returning AllowedCategories for compatibility with existing clients; it lists every selectable custom-role category. This field is not an authorization boundary. A category change does not require removal of grants solely because their former eligibility metadata differed.

The updated grant policy preserves server authorization boundaries: tenant and role ownership, `Roles.ConfigurePermissions` authority, Active Deny, declared dependencies, resource scope, protected Admin permissions, protected/system-managed role rules, last-administrator protection, and platform/global privilege boundaries remain enforced. Actor possession is no longer a prerequisite for ordinary non-Administration grants to user-managed custom roles; Administration-module permissions remain actor-authorized only. The separate bootstrap rules and protected Admin subset remain distinct. No permission is granted automatically.

Some domain-specific eligibility rules may still use Role Category independently of ordinary permission grants, including Management-category manager assignment and System/Management Annual Budget approver eligibility. They are not grant-filter rules and were not changed by this suspension.

Do not restore category-based permission grant restrictions until the Product Owner explicitly approves their reintroduction. Historical seed conflict counts and migration notes below do not require remediation while the suspension is active.
