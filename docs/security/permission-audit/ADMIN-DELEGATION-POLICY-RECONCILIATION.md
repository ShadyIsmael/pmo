# Admin Delegation Policy Reconciliation

Generated: 2026-11-14 20:26:21 +02:00
Status: IMPLEMENTED (source) — focused verification evidence recorded separately.

## Before (observed/current policy behavior)
- **User role assignment/delegation:** required actor to possess each target permission, or permission had to be in narrow bootstrap-delegable set and assignment had to be to a different user on a non-system custom role. Possession was treated as primary delegation authority.
- **Role configuration:** ordinary scoped permissions for custom roles could be configured through role-permission configuration flows without requiring actor possession for non-restricted permissions (subject to Roles.ConfigurePermissions and existing checks), while restricted/administration/baseline permissions required actor effective access; bootstrap-delegable keys had specific constraints.
- **Direct overrides:** actor could only grant/deny permissions within their own effective access (ctorPermissions.Contains(key) required).
- **Resulting inconsistency:** protected Tenant Admin could be blocked from creating/assigning users to eligible ordinary roles containing unheld operational permissions, producing POST /api/users 403.

## After (reconciled common delegation policy intent)
- **Delegation authority vs operational possession:** separated. Protected Tenant Admin (canonical 	enant.admin system role identity) may delegate eligible ordinary permissions/roles within tenant without holding those operational permissions; this does not grant Admin operational access.
- **Eligible ordinary delegation:** active, assignable, role-assignable scoped (Tenant/Organization/Portfolio/Program/Project), non-restricted (not administration-module and not tenant-admin-baseline), not Platform/CrossTenant/SystemOwner.
- **Protected Tenant Admin:** can assign eligible ordinary custom roles and delegate eligible ordinary direct overrides without possession, subject to tenant/scope validation, no self-escalation, and no system-role escalation path via unheld permissions.
- **Ordinary delegated administrators:** require explicit effective administrative/delegation authority (e.g. Users.AssignRoles for assignment operations plus role-configuration/bootstrap authority for unheld ordinary delegation as applicable). Cannot delegate restricted/admin/baseline or reserved permissions without effective access; cannot self-escalate.
- **Role configuration vs role assignment:** role create/edit permission resolution remains governed by RoleAdministrationService (operation-specific Roles.Create/EditDetails/ConfigurePermissions/Delete, dependency checks, protected Admin baseline rules, actor-retains-role-management safeguards). User role assignment now validates full target role grants against common delegation authority so preconfigured roles cannot bypass assignment policy.
- **Direct overrides:** aligned to same eligible-ordinary delegation boundary for others; self overrides still forbidden; protected Admin mandatory denies and inactive/reserved rules unchanged.

## Preserved safeguards
- Canonical protected Admin identity by SystemRoleKey, not display name.
- Protected role/core, last-effective-admin mutation guard, tenant serialization/revalidation.
- Reserved scope exclusion, tenant isolation, active Deny precedence, dependency enforcement.
- No automatic grant of operational permissions to Admin; no blanket bypass or role-name authorization.
- Existing valid bootstrap delegation paths for scoped role admins retained.

## Known/accepted distinctions
- Users.Create/Users.AssignRoles remain required operation gates; they do not imply unlimited delegation beyond policy.
- Protected Admin delegation of unheld permissions is limited to eligible ordinary custom roles (not system roles) in assignment path to avoid protected/system role takeover.
- Direct override delegation for non-protected ordinary admins is tied to Roles.ConfigurePermissions authority for eligible ordinary permissions; restricted/admin permissions still require actor effective possession.
