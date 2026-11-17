# RBAC Delegation and Security Verification

**Date:** 2026-11-15  
**Status:** Source reviewed; React production build and scoped UI tests passed; .NET authorization tests did not execute. Runtime/API and Development data remain unverified.

## Contract implemented in source

- Delegation authority is checked separately from operational permission possession.
- A canonical protected Tenant Admin may assign eligible ordinary roles, including Project Manager, without holding every operational permission on the target role.
- Delegating operational access does not copy that operational access to the actor or make the actor an eligible workflow approver.
- Assignment and role/direct-override changes validate tenant ownership, canonical role identity, active catalog membership, assignability, scope, dependencies, restricted/Core/reserved permissions, and anti-self-escalation.
- User create/update role changes run under the Tenant Admin mutation guard, revalidate authority, keep the membership change/audit within the same persistence unit, and retain last-effective-Admin protection.
- Effective permissions retain Active Deny precedence for ordinary permissions and reapply mandatory Protected Core for the exact canonical protected Tenant Admin identity. Organization Admin does not receive this Core.
- Direct Allow/Deny/Inherit validates the complete resulting effective permission set, including dependencies; resetting a saved override also rechecks current delegation authority.
- Protected Tenant Admin identity requires Tenant scope and matching `IsSystem`, `RoleKey`, and `SystemRoleKey`; role name/category alone is not authority.
- Tenant Admin and Organization Admin identities are excluded from ordinary role assignment; Organization Admin appointment remains denied pending a separate approved policy.

Primary source references:

- `apps/api/src/NEXUS.Modules.Identity/Application/RoleAssignmentAuthorizationPolicy.cs`
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserAdministrationService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserEffectivePermissionService.cs`
- `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs`
- `apps/api/src/NEXUS.Modules.Identity/Api/RolesController.cs` and `UsersController.cs`
- `apps/web-react/src/features/administration/AccessPages.tsx`

## P0 Project Manager onboarding

Expected sequence:

1. Protected Tenant Admin has `Users.ManageAccounts` and `Users.AssignRoles`.
2. Role choice uses the server-authorized assignable-role list, not `Roles.View` or a broad role list.
3. Admin creates a user and assigns canonical Project Manager (`tenant.project-manager`, Management category, Non-System).
4. Assignment policy confirms every Project Manager permission is eligible and scoped but does not require actor possession of ordinary operational permissions.
5. Target user receives membership and target effective permissions; Admin effective permissions remain unchanged.
6. Audit records the user/membership change. Project assignment remains a separate Handover/manager-assignment workflow.

The controller source requires `Users.ManageAccounts`; the service additionally requires `Users.AssignRoles` when roles are supplied. `AdminDelegationAuthorityRecoveryTests.DEL03A` contains the positive fixture with a Project Manager operational permission absent from Admin and asserts Admin does not gain it. The fixture is not an authenticated Development API test.

The repeated `POST /api/users -> 403` remains **unattributed**. Candidate rejection points include controller policy, mutation-actor binding, missing `Users.AssignRoles`, role lookup, catalog/scope/dependency restrictions, or a protected identity mismatch. None was tied to an actual request trace. The focused .NET/API test build did not reach discovery because of the IKVM reference-generation boundary; no .NET tests ran. Do not say the historical incident is resolved until the Development API returns a correlated successful response and persisted membership/effective-permission evidence.

## Security test matrix

| Security property | Positive coverage source | Negative/corner coverage source | Evidence state |
|---|---|---|---|
| Delegation without operational possession | DEL03A: protected Tenant Admin assigns PM with unheld `Project.Edit`. | Wrong tenant, self escalation, invalid/reserved role/permission, restricted admin permission, dependency failure. | Test sources exist; .NET suite did not reach discovery, so no cases executed. See `RBAC-TEST-EXECUTION-RESULTS.md`. |
| Delegation does not elevate actor | DEL03A asserts Admin remains without `Project.Edit`. | Self-override and attempt to gain effective permission via role edit/direct override. | Test sources exist; execution pending. |
| Direct Allow/Deny/Inherit | DEL04A/DEL04E exercise eligible direct Allow and atomic dependency-complete updates. | DEL04B/DEL04C/DEL04D/DEL04F cover self-escalation, Protected Core, missing dependency, and latent inherited Action. | Test sources exist; execution pending. |
| Active Deny and fresh state | Effective service reloads membership/overrides; Organization profile listing rechecks fresh permission. | Revoked membership, Active Deny on Org View/Manage, stale grant. | Test sources exist; execution pending. |
| Protected Core and last Admin | Canonical Admin policy and guarded mutation path. | Reserved identity spoof, deny Core, remove last effective admin. | Test sources exist; execution pending; persisted Development state unknown. |
| Role scope and Tenant isolation | Same-Tenant canonical identity and scoped role checks. | Cross-Tenant role/user/scope, invalid organization/project scope. | Test sources exist; execution pending. |
| System identity integrity | `RoleKey` separate from System/Protected/category/assignability flags. | Only `tenant.admin` and `organization.admin` accepted; malformed NULL identity is now rejected by explicit SQL check guards. | New SQLite constraint regression test source; execution pending. |
| Atomicity and concurrency | Save/audit under guarded persistence transaction. | Stale rowversion, concurrent role change, partial multi-role or multi-override failure. | Source contains protections; concurrency/race results not established by current verification. |

## Known gaps and constraints

- Protected Tenant Admin's full HTTP `POST /api/users` flow and real persistence are unverified.
- Actual seeded PM permissions, memberships, and Admin effective access are unknown until the Development database is verified.
- Organization Admin appointment/removal/protection/recovery is unresolved and deliberately fail-closed.
- Active Deny, last-admin continuity, races, and direct-override semantics require the executable test results and final target-specific checks before release.
- Authorization failures currently have safe source-level rejection paths; trace-correlated deployed response wording/status remains unverified.

