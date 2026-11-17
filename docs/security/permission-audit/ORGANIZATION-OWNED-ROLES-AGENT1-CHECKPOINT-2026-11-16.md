# Organization-Owned Roles — Agent 1 Checkpoint

**Date:** 2026-11-16  
**Status:** Migration applied and approved ownership reconciliation verified on Development; successful login and broader regression closure remain open.

## Decisions and current implementation

- Operational Roles have one immutable `OrganizationId`; Role names may repeat across Organizations, while Role IDs and permission grants remain independent.
- Protected Tenant Admin and Organization Admin remain tenant-scoped System Role identities. Organization Admin authority continues through the approved organization-scoped settings grants; no per-Organization Tenant Admin role is introduced.
- `GET /api/roles/assignable` returns assignable Role IDs with Organization ID/name and ownership `scope`. Assignment authorization resolves Role IDs, validates tenant and active Organization, checks permission delegation in each owning Organization, and rejects protected or unresolved Roles.
- User Create/Edit DTOs in the current shared tree use `roleIds`. Responses expose Role ID and Organization metadata; `organizations` is derived from active Role assignments. Direct Organization assignment is not used by the current User Administration service.
- On Create, absent or empty `roleIds` creates a user without Role assignments. On Update, absent/null preserves current assignments and an empty array requests removal. Removing the last Role in an Organization checks the current protected-membership guard and returns structured HTTP 409 when blocked.
- Effective permission resolution keeps Organization-owned Role grants within their owning Organization; Active Deny remains authoritative. Organization membership by itself grants no permission.
- Roles & Permissions supports a required Organization selector on create, immutable ownership on edit, Organization filtering/display, independent permission editing, and English/Arabic labels. The displayed ownership label follows the API `scope` field.

## Migration and legacy data

Created `20261116093145_AddOrganizationOwnedRoles` with a same-Tenant composite Organization FK, active unique name index on `(TenantId, OrganizationId, Name)`, and a filtered tenant/name index for ownerless rows. RoleKey/SystemRoleKey identity uniqueness remains tenant-wide.

The migration retains legacy Role IDs, grants, UserRole rows, audit rows, and `UserOrganizations` data. It does not infer owners. The approved Portfolio Manager and Project Manager Roles are reconciled to NEXUS Group within the migration transaction. Five unassigned operational Roles remain `IsLegacyOrganizationOwnershipUnresolved`; no default Organization mapping is applied to them. The `Down` migration refuses to discard ownership or unresolved-state metadata. `UserOrganizations` was not dropped.

### Database gate status

| Check | Result |
|---|---|
| EF model/snapshot | Synchronized: **Yes** |
| Live target identity | Verified Development `Shady / NexusPmo`, DB_ID **5**, Windows Authentication, API Development endpoint matched |
| Migration history | **30/30 applied**, **0 pending**; only pre-apply pending migration was `20261116093145_AddOrganizationOwnedRoles` |
| Fresh reviewed SQL / SHA-256 | `artifacts/ef-review/dev-pending-20261116T121205Z-3812ee8e.sql`; `CC49E01AFC03473FDC2C10827909038DCC95C62AC1E2234425D3D90D7ABC6E8C` |
| Apply report | `artifacts/ef-review/dev-db-migrations-apply-20261116T121415Z-ccad8141.json` |
| Final status report | `artifacts/ef-review/dev-db-migrations-status-20261116T121941Z-8189009f.json` |
| Migration applied | **Yes**, through the approved Development wrapper after explicit authorization |
| Physical schema | Verified; **0** objects missing from the last-applied model and **0** missing from current repository model |
| Role data comparison | Target Role IDs unchanged; active UserRole rows and RolePermission links preserved; two approved Roles now owned by NEXUS Group; five unassigned operational Roles remain unresolved |
| Seed / reset | Not run |
| Backup | Not requested (optional for Development) |
| Persistence/security tests | Focused login/session and role-isolation run passed **20/20**; broader filtered suite **43/91 passed, 48 failed** |
| Successful live login | Not verified; no approved credential was available in environment or user-secrets store |

## Build evidence

- `NEXUS.Modules.Identity` build: passed, 0 warnings / 0 errors after shared contract and protected-role scope hardening.
- `NEXUS.Persistence.DesignTime` build: passed, 0 warnings / 0 errors.
- `NEXUS.EFDatabaseGate` build: passed, 0 warnings / 0 errors.
- Offline EF gate `model -NoBuild`: passed; model synchronized, 30 repository migrations; database connection not opened.
- `NEXUS.Api` build: passed, 0 warnings / 0 errors.
- `apps/web-react` `npm run build`: passed. Existing Rollup annotation, mixed import, and large-chunk warnings remain.

The focused login/session and same-name Organization-role isolation regressions pass. A broader security/persistence filter remains red with 48 failures; those results must be reviewed before full acceptance. Successful login against the live Development database remains unverified because no approved credential was available. No password reset or seed was performed.

## Independent reviews and open findings

- Domain/model review: approved the owner FK, uniqueness split, protected-role exception, and conservative legacy handling. It identified and this checkpoint fixed protected identity scope checks, domain prevention of assigning a System Role identity to an owned/unresolved Role, and guarded migration rollback.
- Roles UX review: verified Organization selection/display/filtering, immutable ownership, permission editor isolation, EN/AR coverage, and protected Admin UI controls. Scope labels now use the explicit API `scope` field.
- RBAC contract review: confirmed the current DTOs/service have adopted stable Role IDs, per-Organization authorization, derived Organization projections, and structured dependency conflicts.
- Independent security review: the earlier stale Update User authorization read was corrected in the final integration by moving it under the tenant mutation guard. No migration changes were made to User Administration authorization or audit behavior.
- QA review: focused same-name Organization-role isolation and login/session tests pass after the regression fixture was aligned to explicitly project-scoped permissions. The broader suite remains red; protected membership dependencies, Active Deny, concurrency, and full persistence behavior still need complete passing coverage.

## Files changed by Agent 1 in this checkpoint

- Identity domain and shared contracts: `Role.cs`, `ProtectedTenantAdminPolicy.cs`, `RoleAdministrationDtos.cs`, `IRoleAssignmentAuthorizationService.cs`, `RoleAssignmentAuthorizationPolicy.cs`, `RoleAssignmentContract.cs`, `RoleAssignmentAuthorizationService.cs`, `RoleAdministrationService.cs`, `RolesController.cs`, `IdentityModuleRegistration.cs`, `IUserEffectivePermissionService.cs`, `UserEffectivePermissionService.cs`, and `UserPermissionClaimsMiddleware.cs`.
- Shared organization contracts/adapters: `IUserOrganizationDirectory.cs`, `IUserOrganizationMembershipIntegrity.cs`, `IUserOrganizationSettingsPermissionLookup.cs`, the corresponding Organization module adapters, and `OrganizationModuleRegistration.cs`.
- Persistence: `RoleConfiguration.cs`, `SeedDataProvider.cs`, `AppDbContextModelSnapshot.cs`, and the `AddOrganizationOwnedRoles` migration and designer.
- Frontend Roles screen and API types: `RolesPage.tsx`, `api/admin.ts`, and the English/Arabic messages.
- Handover contract: `ORGANIZATION-OWNED-ROLE-CONTRACT.md`.

Agent 2-owned User Administration source files are present in the shared worktree but were not edited by Agent 1.

## Remaining gates

1. Verify one successful live Development login using an existing approved credential. No credential was found in the local Development environment or user-secrets store; password recovery and seed remain out of scope.
2. Review and resolve the 48 failures in the broader filtered security/persistence run before claiming full regression acceptance.
3. Complete full SQL Server persistence and runtime authorization/UAT checks; the applied migration and schema gate do not substitute for those application-path checks.
4. Add Organization-scoped workflow role checks where unscoped `ClaimTypes.Role` guards previously relied on Organization Roles; current behavior is intentionally fail-closed and may deny those workflows.

**Migration delivery is complete on Development. Full application acceptance remains open.**
