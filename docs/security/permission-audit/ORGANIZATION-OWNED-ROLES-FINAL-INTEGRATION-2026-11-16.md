# Organization-Owned Roles — Final Integration Handover

**Date:** 2026-11-16  
**Status:** Migration and ownership reconciliation applied; final acceptance remains open for successful Development login and broader regression closure  
**Repository:** `C:\Projects\pmo`

## Summary

The shared implementation builds, and the React production build succeeds. Create User already acquires the tenant mutation guard before validating Role IDs; Update User was corrected to acquire that guard before reading the actor's effective permissions and deriving mutation capabilities. Organization membership in User Administration is derived from assigned Organization-owned Roles. Global role-name claims now expose only the canonical Tenant Admin identity, preventing unscoped workflow role checks from treating an Organization Role or unresolved legacy Role as globally valid.

The authorized Development target was verified as `Shady / NexusPmo`, database ID 5. Migration `20261116093145_AddOrganizationOwnedRoles` was regenerated through the EF Database Gate, reviewed, and applied with the Product Owner's two-Role ownership reconciliation in the migration transaction. The final read-only gate confirms 30/30 migrations, synchronized model, and zero missing schema objects. A pre-apply snapshot comparison confirms that all Role IDs, active UserRole assignments, and RolePermission links were preserved; the two approved Roles are owned by NEXUS Group and the five unassigned operational Roles remain unresolved. The API build passes. A focused 20-test login/session and role-isolation run passes; the broader filtered security/persistence run passes 43 of 91 and fails 48. Successful live login remains unverified because no approved Development login credential was available; no password reset or seed was performed.

## Files changed for this integration

- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserAdministrationService.cs` — moved Update User authorization reads under the existing tenant mutation guard; Create User already uses that guard and authoritative Role IDs.
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserEffectivePermissionService.cs` — restricted global `ClaimTypes.Role` names to the canonical Tenant Admin identity.
- `apps/api/src/NEXUS.Modules.Organization/Application/UserOrganizationMembershipIntegrity.cs` — combines protected settings dependencies with active responsibility dependencies.
- `apps/api/src/NEXUS.SharedKernel/IUserOrganizationResponsibilityLookup.cs` — shared responsibility lookup contract.
- `apps/api/src/NEXUS.Api/Identity/UserOrganizationResponsibilityLookup.cs` — resolves active portfolio, program, and project management/sponsor responsibilities to Organizations.
- `apps/api/src/NEXUS.Api/Program.cs` — registers the responsibility lookup.
- `apps/api/tests/NEXUS.Api.Tests/Integration/UserPermissionAdministrationTests.cs` — added scoped Role-name, global-role-claim, Role-ID, derived-membership, and direct-membership non-authority coverage; updated test mappings and Role DTO assertions.
- `apps/api/tests/NEXUS.Api.Tests/Security/AdminDelegationAuthorityRecoveryTests.cs` — migrated Create/Edit user test inputs to Role IDs and Organization scope.
- `apps/api/tests/NEXUS.Api.Tests/Security/FinancialApprovalDelegationBoundaryTests.cs` — migrated user test inputs and fixtures to Role IDs and Organization-owned roles.
- `apps/api/tests/NEXUS.Api.Tests/Integration/LocalPasswordLifecycleTests.cs` — updated the Create User DTO and service construction.
- `apps/api/tests/NEXUS.Api.Tests/Integration/ProgramCreationServiceTests.cs` and `apps/api/tests/NEXUS.Api.Tests/Security/PortfolioProgramAuthorizationTests.cs` — updated test fakes for the organization-scoped effective-permission contract.

The worktree already contained extensive concurrent changes before this integration. They were preserved; this list is limited to files relevant to this handover.

## Integration and security findings

- Create User validates selected Role IDs, tenant ownership, Organization availability, permission delegation, and persists the user, Role assignments, and audit entry within the tenant mutation guard transaction.
- Update User now calculates actor permissions and account/Role/status capabilities after acquiring the same guard, closing the stale-authorization interval identified by the independent concurrency review.
- Role assignment resolves by Role ID and rejects cross-tenant, inactive-Organization, protected administrator, and ownerless/unresolved legacy Roles. Effective permission evaluation filters Organization-owned grants to the requested Organization; unowned ordinary Roles grant no effective access.
- User Administration response Organizations are derived from assigned Organization-owned Roles. Source review found no application service reading the persisted `UserOrganizations` rows to authorize users or produce this DTO. The table/model remains in the repository; it is not an authority source in the reviewed User Administration path. The direct-membership regression is included in the filtered suite; the broader suite is not clean overall.
- Removing a user's final Role in an Organization checks `OrganizationSettingsGrant` plus active portfolio/program/project manager and owner/sponsor responsibilities. The responsibility query compiles as part of the API build, but it was not runtime-tested against SQL Server.
- Global `ClaimTypes.Role` claims are now limited to canonical Tenant Admin. This closes the unscoped Organization-role name leak reported by the independent reviewer. Existing workflow guards that rely on Organization Role names will need an Organization-scoped role check to preserve those workflows safely; that follow-up was not implemented here.
- The domain's `AssignOrganization` method rejects changing a Role's owner after assignment and prevents assigning an owner to an unresolved legacy Role. The applied database constraint/FK and post-migration ownership state were verified; service-level runtime enforcement was not separately exercised against SQL Server.
- Tenant Admin and Organization Admin identities remain distinguished by canonical Role/SystemRole keys. Effective Organization Admin configuration grants remain Organization-scoped. Runtime behavior was not tested in this environment.

## Independent reviews

Focused IAM, concurrency, legacy reconciliation, test-coverage, and independent workflow/security reviews reported:

- A stale Update User permission calculation before the mutation guard — corrected in this integration.
- Global Role-name claims could allow an Organization or unresolved Role to satisfy an unscoped workflow guard — closed fail-safe by restricting global claims to Tenant Admin; scoped workflow guard follow-up remains.
- The available filtered suite now executes. Focused login/session and same-name Organization-role isolation cases pass 20/20 after correcting the isolation test to use explicitly project-scoped permissions. The broader filtered security/persistence run has 48 failures; those remain open for review and are not represented as passing.
- The live Role/UserRole/RolePermission inventory was reviewed before apply. The approved Portfolio Manager and Project Manager Roles each had one active assignment and 41/45 active grants; five unassigned operational Roles were left unresolved. The transaction added ownership only to the approved Role IDs and preserved existing assignment/grant links.

## Build and test results

| Check | Result |
|---|---|
| `NEXUS.Modules.Identity` build | Passed — 0 warnings, 0 errors |
| `NEXUS.Persistence` build | Passed — 0 warnings, 0 errors |
| `NEXUS.Api` build | Passed — 0 warnings, 0 errors |
| Focused login/session and Organization-role isolation | Passed — 20/20 after test-scope fixture correction |
| Broader filtered security/persistence suite | Failed — 43 passed, 48 failed, 91 total; see test output for authorization/fixture and permission expectation failures |
| EF Model synchronization | Passed — 30 repository migrations, model/snapshot synchronized |
| Live Development migration/schema gate | Passed — 30 applied, 0 pending, 0 missing schema objects |
| Successful login against live Development | Not verified — no approved credential was available; no password reset or seed was performed |

The focused suite's 20/20 result covers the auth pipeline, local password/session lifecycle, and same-name Organization-owned Role isolation. The broader run remains red; no full security/concurrency/Active Deny or SQL Server persistence pass is claimed.

## Migration and database evidence

- Repository migration: `20261116093145_AddOrganizationOwnedRoles`.
- Fresh pre-apply SQL: `artifacts/ef-review/dev-pending-20261116T121205Z-3812ee8e.sql`.
- Reviewed SQL SHA-256: `CC49E01AFC03473FDC2C10827909038DCC95C62AC1E2234425D3D90D7ABC6E8C`.
- Apply report: `artifacts/ef-review/dev-db-migrations-apply-20261116T121415Z-ccad8141.json`.
- Final read-only status report: `artifacts/ef-review/dev-db-migrations-status-20261116T121941Z-8189009f.json`.
- Target identity verified by the approved wrappers: Development `Shady / NexusPmo`, database ID 5, Windows Authentication, API Development endpoint matched.
- Migration history: 30 repository migrations, 30 applied, 0 pending; the only pre-apply pending migration was `20261116093145_AddOrganizationOwnedRoles`.
- Physical schema: verified against the current model; 0 objects missing from the last-applied model and 0 objects missing from the current repository model.
- Migration applied through `scripts/dev/db-apply-migrations.ps1` after Product Owner authorization, exact SQL/hash review, and target verification. Ownership reconciliation was inside the migration transaction. No manual SQL was used.
- Pre-apply data check: NEXUS Group is active and shares a Tenant with both approved active Roles. Portfolio Manager had 1 active UserRole and 41 active grants; Project Manager had 1 active UserRole and 45 active grants. Protected Tenant Admin/Organization Admin identities remained unchanged.
- Pre/post data comparison: Role IDs unchanged; both Role IDs and all 2 active UserRole links / 86 RolePermission link IDs remained unchanged. Portfolio Manager and Project Manager now reference NEXUS Group, `IsLegacyOrganizationOwnershipUnresolved = false`; five unassigned operational Roles remain unresolved.
- Seed/reset: not run. Backup: not requested (Development policy makes it optional).

### Live login follow-up

The local Development environment and user-secrets store had no approved login credential. A successful login against the verified target therefore remains unverified. Supply an existing credential through the approved secure local channel and repeat `POST /api/auth/login` against the API configured for this exact Development database. Do not reset a password or seed an account as part of this migration task.

## Remaining blockers

1. Verify a successful live Development login using an existing approved credential; no credential was available in the local environment, and password recovery/seed were not authorized.
2. Review and resolve the 48 failures in the broader filtered security/persistence suite before claiming full regression acceptance.
3. Complete full SQL Server persistence and runtime authorization/UAT checks; the current database gate verifies schema and migration history, not application-path persistence behavior.
4. Add Organization-scoped workflow role checks where `ClaimTypes.Role` name guards previously depended on Organization Roles; current global-claim behavior is intentionally fail-closed and may deny those workflows.

**Acceptance decision:** Migration and data reconciliation are complete on the authorized Development target. Full acceptance remains open because successful live login and broader security/persistence regression closure are not verified.
