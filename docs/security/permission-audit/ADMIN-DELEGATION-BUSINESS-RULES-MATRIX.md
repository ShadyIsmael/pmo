# Admin Delegation Business Rules Matrix

Generated: 2026-11-14 20:19:37 +02:00
Status: IMPLEMENTED - analysis captured from source inspection and approved task decisions.

## Business operating model (Tenant Admin delegation)
- The protected tenant administrator is the existing user holding the canonical 	enant.admin system role (Role.SystemRoleKey == tenant.admin, IsSystem == true, same tenant). This is a business responsibility, not a new role or permission key.
- Delegation authority is independent from operational permission possession. Tenant Admin may create users, assign eligible ordinary roles, and delegate eligible ordinary permissions within tenant scope without personally holding those operational permissions.
- Delegation does not grant the administrator operational access, approval eligibility, or resource ownership.
- Ordinary delegated administrators may act only within explicit effective administrative/delegation authority and approved target scope.
- Protected roles/core, reserved scopes (Platform/CrossTenant/SystemOwner), tenant isolation, direct Active Deny precedence, anti-escalation, and last effective Admin safeguards remain mandatory.

## Workflow matrix

| Workflow | Required actor authority (entry) | Delegation/assignment rule | Tenant/scope validation | Expected result | Notes / safeguards |
|---|---|---|---|---|---|
| Create user without roles | Users.ManageAccounts; Users.SetStatus if inactive | No role delegation evaluated | Actor identity/tenant bound from authenticated principal; tenant mutation guard | Allowed for authorized actor in tenant | No partial persistence on failure; audit as implemented |
| Create user with existing ordinary role(s) | Users.ManageAccounts, Users.AssignRoles, Users.SetStatus if inactive | Each target role permission must be delegable by actor under common policy; possession not required for eligible ordinary permissions | Role and permission rows must belong to same tenant and be active/catalog-valid; reserved scopes rejected | Allowed when role is eligible ordinary role and actor has delegation authority | No self-escalation for actor-as-target N/A on create; no cross-tenant role ids |
| Edit user account fields | Users.ManageAccounts for profile fields | No delegation | Same tenant user | Allowed | Independent from role changes |
| Set user status | Users.SetStatus | No delegation | Same tenant; cannot change own status | Allowed/denied per authority | Last-admin protection revalidated |
| Assign/remove role(s) on existing user | Users.AssignRoles; account/status perms as applicable for other fields | Common delegation policy over full target role permission set; rejects actor self-escalation via unheld permissions | Same tenant role/user/permission; reserved scopes rejected; protected/system role restrictions enforced | Allowed for eligible ordinary roles within authority | Revalidated in mutation transaction; last-admin guard |
| Assign multiple roles | Users.AssignRoles | All permissions across all selected roles must pass delegation checks | Same as single assignment | Allowed only if every role/permission is delegable | Atomic role replacement |
| Configure role grants (create/edit custom role permissions) | Roles.Create / Roles.EditDetails / Roles.ConfigurePermissions as applicable | RoleAdministrationService resolves permissions with delegation/assignment constraints; administrative/restricted permissions require actor effective access; ordinary scoped permissions delegable for custom roles per existing policy | Tenant-scoped catalog rows; dependencies enforced; protected Admin baseline/system-role rules enforced | Allowed for eligible permission configuration | Distinct from role assignment: changes role definition |
| Assign existing configured role to user | Users.AssignRoles | Must validate target role's current grants are delegable by actor (cannot bypass by preconfigured role) | Same tenant | Allowed/denied by common assignment policy | Prevents indirect escalation chain |
| Direct permission overrides (Allow/Deny) | UserPermissions.Manage | Actor must have effective access or eligible delegation authority for unheld ordinary permission; cannot change own overrides | Same tenant user/permission; protected Admin mandatory denies blocked | Allowed for eligible ordinary scoped permissions; restricted/admin permissions require effective access | Direct Deny precedence preserved by resolver |
| Revoke/remove delegation (role removal or override removal/inherit) | Matching assign/manage authority | Removal of grants/overrides permitted within authority | Tenant-scoped | Allowed; effective permissions recomputed | Last-admin protection on mutations affecting protected admin effectiveness |

## Self-escalation, circular/indirect escalation, deadlock analysis
- **Self-escalation:** blocked when assigning a role or granting direct override would add a permission the actor does not effectively hold to the actor's own account (	argetUserId == actorUserId in role assignment; explicit self-edit rejection in user update/direct overrides).
- **Circular delegation:** prevented because authority is checked against actor's current effective permissions/delegation authority at mutation time inside tenant mutation guard; actors cannot grant themselves unheld permissions and then re-enter with new authority in same operation.
- **Indirect escalation via role chains:** prevented by validating the full permission set of each selected existing role during assignment, not only role metadata. A custom role containing restricted/admin or reserved permissions cannot be assigned without satisfying those specific rules.
- **Deadlock / last-admin loss:** tenant admin mutation guard and post-mutation effective-admin checks reject changes that would remove last effective tenant administrator; protected Admin mandatory permissions cannot be removed/denied.
- **Concurrency:** actor authority and role/user state are re-read/revalidated in transactional mutation paths; DbUpdate concurrency conflicts handled as conflict/no-success outcomes.

## Validated expectation
- **Admin can delegate ordinary permissions without possessing them:** YES for eligible, assignable, tenant/org/portfolio/program/project-scoped, non-restricted, non-reserved permissions when acting as protected 	enant.admin within same tenant and not escalating own access.
- **Ordinary administrators:** may delegate eligible ordinary permissions on custom roles/overrides only with explicit delegation authority (Roles.ConfigurePermissions path and/or approved bootstrap paths) and never for restricted administration/baseline permissions or reserved scopes.

## Risks reviewed before implementation
- Over-broad delegation of restricted administration permissions: mitigated by treating administration-module and tenant-admin-baseline permissions as restricted (require actor effective access).
- Protected/system role misuse: protected Admin identity checked by canonical system role key; assignment of system roles with unheld permissions denied for non-possessed restricted cases and restricted by role-assignment policy.
- Cross-tenant/reserved scope leakage: all role/permission/user rows must match tenant; Platform/CrossTenant/SystemOwner excluded.
