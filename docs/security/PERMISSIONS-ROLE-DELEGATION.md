# Role Permission Delegation Policy

**Status (2026-11-02):** Protected Admin and last-administrator safeguards are implemented; focused permission/security tests passed (38/38), seed tests passed (10/10), and Portfolio/Program UI tests passed (12/12). `Shady/NexusPmo` was inspected; a verified backup preceded the narrowly audited Admin reconciliation.

## Server-side rules

The role editor is a convenience layer. `RoleAdministrationService` is authoritative for every create, detail update, permission configuration, and delete operation.

| Operation | Required capability | Additional server checks |
|---|---|---|
| View roles and role details | `Roles.View`, `users.read`, or verified `TenantAdministration.ManageScope` | Active actor and role are in the authenticated Tenant. Role administrators see only roles whose active grants fit their effective delegation set. Permission counts and detail projections include only permission rows from that Tenant, even if a malformed cross-Tenant join row exists. |
| Create a role | `Roles.Create` | Tenant ownership; if permission IDs are supplied, also require `Roles.ConfigurePermissions` or verified `TenantAdministration.ManageScope`; validate every requested active catalog key against actor authority. |
| Edit role details | `Roles.EditDetails` | Tenant/role ownership; reject protected system-role identity changes. |
| Configure role permissions | `Roles.ConfigurePermissions` or verified `TenantAdministration.ManageScope` | Tenant/role ownership; active keys only for new grants; enforce declared dependencies; reject keys outside actor's effective permissions; audit before/after IDs. Tenant scope does not grant create, details-edit, delete, user-role assignment, direct overrides, or Platform authority. |
| Delete a role | `Roles.Delete` | No `Roles.Manage` or `users.write` fallback; reject system roles and roles with active user assignments. |

`users.write` is not a role-administration authorization path. The inactive `Roles.Manage` compatibility permission may authorize creation, role-detail editing, or permission configuration only where retained by the current service; it never authorizes deletion. `TenantAdministration.ManageScope` is resolved from the active actor's effective tenant permission set, including declared permission dependencies; no role name proves authority.

Before the canonical role-operation split, `RoleAdministrationService` used `users.write` as a blanket service gate. Existing persisted roles are not automatically backfilled when the service switches to `Roles.View`, `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, and `Roles.Delete`. If a verified Development inventory finds an existing administrator role relying on the legacy gate, reconcile only the approved canonical grants through an audited recovery; do not restore a live `users.write` bypass.

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

- Permission changes are bounded by the actor's effective permission set and any explicit Tenant/role ownership constraints. A role cannot receive a permission the actor is not authorized to delegate.
- An actor cannot edit their own assigned role membership through this role-permission endpoint or use a grant operation to create an effective permission escalation path.
- A Tenant Administrator can configure only grants already present in their own effective permissions. The role service rechecks effective tenant-administrator capabilities after applying role changes, direct overrides, and catalog dependencies. A serializable transaction and per-tenant SQL Server application lock prevent concurrent changes from removing the last effective tenant administrator.
- Actions with missing dependencies are rejected for new role configurations. Runtime resolution removes invalid Actions from the effective set; it does not silently grant a missing View.
- Inactive compatibility keys are not offered as new grants. Existing persisted references are retained until a reviewed migration.
- User role assignment and direct user Allow/Deny overrides remain separate capabilities (`Users.AssignRoles`, `UserPermissions.Manage`) and require their own server checks and audits.

## Preservation and audit

New protected Admin roles receive the dynamic mandatory baseline. Ordinary startup seeding does not rewrite existing RolePermission rows. An explicit Development-only reconciliation adds missing mandatory Admin grants, soft-deletes only conflicting mandatory Deny overrides, and audits changes; unrelated role grants, user-role links, and direct overrides are preserved. The 2026-11-02 verified Development reconciliation added three grants to the seeded Admin and left the 65 `pmo.admin` grants unchanged; details and backup verification are in the [implementation report](PERMISSIONS-IMPLEMENTATION-REPORT.md). No migration was created or applied. Never fan out a broad legacy grant into approval, settlement, reversal, closing, or financial-ceiling permissions.

Role permission changes audit actor, Tenant, role, and before/after permission keys. The Admin reconciliation wrote a Tenant-scoped compatibility-grant audit record with the before/after sets; focused tests cover grant and rejection audit behavior. Portfolio and Program initial manager assignment requires `Handover.AdminInitiate`; CRUD permission keys alone do not allow assignment. Browser runtime UAT remains open; see the implementation report.

## Role category eligibility (2026-11-05)

Create and update now validate the custom role category against the closed set Tenant, Organization, Management, Operation, and Business. System is reserved and cannot be selected for a custom role; system-role categories are protected. Each permission has explicit multi-category eligibility metadata, and the service rejects new grants outside the selected category in addition to its existing active-catalog, dependency, actor-delegation, tenant, and protected-Admin checks.

Existing grants are grandfathered only while their category is unchanged, allowing explicit removal without an automatic revocation. A category change returns a conflict with the remaining incompatible permission keys and does not mutate the role. Existing system-role categories and grants are not normalized by ordinary role updates or reseeding. See [Role Category Eligibility and Migration Notes](ROLE-CATEGORY-ELIGIBILITY-AND-MIGRATION.md) for the source-only conflict inventory and reviewed migration approach.
