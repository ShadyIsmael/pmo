# Tenant Admin Protected Permission Editing Recovery

Date: 2026-11-15

## Root Cause

Tenant Admin permission editing was blocked in `RoleAdministrationService.UpdateAsync` by an unconditional rejection of every protected Tenant Admin role mutation:

- `ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)` returned `The protected Tenant Admin role is immutable through role administration.`
- `CanManageRolePermissions` also excluded all protected Tenant Admin roles.
- `CanDelegateOrdinaryRolePermissions` also excluded all protected Tenant Admin roles.

This treated the protected role identity and its optional permission grants as equally immutable, even though the approved policy permits authorized optional operational permission changes while preserving the mandatory Protected Core.

## Backend Changes

File: `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs`

1. Removed the blanket protected-role rejection from `UpdateAsync`, allowing permission-grant mutations while identity fields remain guarded by `CanEditRoleDetails` (still false for protected Tenant Admin).
2. Allowed protected Tenant Admin roles in `CanManageRolePermissions`.
3. Allowed protected Tenant Admin roles in `CanDelegateOrdinaryRolePermissions`.
4. Preserved mandatory Protected Core by validating that every key in `ProtectedTenantAdminPolicy.MandatoryPermissionKeys` remains present; missing keys are rejected.
5. Rejected reserved `Platform`, `CrossTenant`, and `SystemOwner` permission grants for the protected Tenant Admin role.
6. Refined bootstrap permission handling so protected Tenant Admin may receive only keys allowed by `BootstrapPermissionDelegationPolicy.CanAddToProtectedTenantAdminRole`; other bootstrap keys remain blocked.
7. Bypassed the self-escalation restriction specifically for the protected Tenant Admin role so authorized operational permissions can be added, while reserved/platform grants remain blocked.
8. Retained last-effective-tenant-admin protection, concurrency handling, tenant mutation locking, and audit persistence.

## Frontend Changes

No source change was required in `apps/web-react/src/features/administration/RolesPage.tsx`; the existing UI already:

- Enables Edit Permissions when `canManagePermissions` is true.
- Disables identity fields when `canEditDetails` is false.
- Locks mandatory permissions for protected Tenant Admin.
- Auto-includes mandatory permissions in `toSaveInput`.
- Blocks bootstrap permissions except approved protected-admin bootstrap keys.

Backend `canManagePermissions` now reports true for authorized actors, so the existing UI becomes functional.

## Security Invariants Preserved

- Tenant Admin identity, `RoleKey`, `SystemRoleKey`, and system classification remain immutable.
- Delete and deactivation remain unavailable for protected Tenant Admin.
- Mandatory Protected Core cannot be removed or denied.
- Reserved/platform/cross-tenant/system-owner permissions remain unavailable.
- Tenant isolation and actor authorization remain enforced.
- Last-effective-Tenant-Admin protection remains active.
- Changes remain atomic and audited.

## Portfolio / Program Lookup Findings

The Program creation eligible-portfolio lookup is intentionally scoped to native Portfolio Manager assignments and `Program.CreateAcrossPortfolios`. Empty results can occur when the actor is not a native Portfolio Manager or lacks cross-portfolio creation authority. This is a scope/assignment behavior rather than a direct RBAC editing defect. Project creation portfolio/program lookups are governed by `WorkflowLookupPolicyRegistration` and remain unchanged. No unrelated scope expansion was performed.

## Build Verification

- Backend identity module build: success, 0 errors.
- Backend API build: success, 0 errors.
- Backend API test project build: success, 0 errors (pre-existing analyzer warnings only).
- Frontend TypeScript build (`tsc -b`): success, 0 errors.
- Frontend Vite production build: failed in this environment due to an esbuild temporary-file cleanup `Access is denied` error after 3005 modules were transformed. This is an environment/AV-level issue, not a source compile error. TypeScript compilation passed.

## Deferred Verification

Runtime permission editing, Portfolio/Program lookup behavior, and live API checks remain deferred to Product Owner UAT per the task's build-only acceptance.