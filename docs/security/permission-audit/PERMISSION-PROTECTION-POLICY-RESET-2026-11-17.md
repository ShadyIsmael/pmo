# Permission Protection Policy Reset and Documents Authorization

**Date:** 2026-11-17  
**Scope:** Permission grantability, Role scope, and Documents authorization  
**Database:** Not read or written in this task; no migration, reset, or seed was run.

## Current policy

`Security:PermissionProtection:ProtectedPermissionKeys` is now the sole explicit permission-protection list. It defaults to an empty array in `appsettings.json`, so every active canonical permission is grantable unless the Product Owner adds its exact key. Startup validation rejects duplicate, unknown, inactive, or non-canonical keys. No protected keys have been configured because none were approved.

The policy controls grantability only. A grant still requires an authenticated actor with `Roles.ConfigurePermissions` or the relevant user-permission administration authority. Tenant ownership, Organization ownership, permission dependencies, scope, resource authorization, and Active Deny remain separate checks. Platform, CrossTenant, and SystemOwner permissions remain outside Tenant- and Organization-owned Role scope.

The last-admin safeguard is role-identity based: an active user must remain assigned to the canonical Tenant Admin System Role. It does not require an unapproved hardcoded set of permission keys. Removing permissions from the Admin Role does not remove or deny the protected role identity, and Active Deny continues to apply normally.

## Removed legacy rules

The prior implementation contained these protection or delegation rules. They are no longer used as policy:

1. **Dynamic protected Tenant Admin baseline.** `ProtectedTenantAdminPolicy.MandatoryPermissionKeys` treated every active Tenant-scoped permission as mandatory, then added 14 hardcoded Portfolio, Program, and Handover keys: `Portfolio.View/Create/Edit/Archive/Reactivate`, `Program.View/Create/Edit/Archive/Reactivate`, and `Handover.View/Manage/Approve/AdminInitiate`. Admin users inherited these keys even when the Role did not grant them, and Active Deny was ignored for the mandatory set. The runtime union and deny bypass are removed.
2. **Administration/baseline classification.** `TenantAdministrationPermissionKeys` was derived from Tenant-scoped entries in the Administration module plus entries carrying `IsTenantAdminBaseline`. The catalog explicitly marked these 18 non-Administration keys: `DocumentCategory.View/Create/Edit/ActivateDeactivate`; `Governance.AssignmentRecovery.View/Initiate/Manage`; `ProjectLifecyclePhase.View/Create/Edit/ActivateDeactivate`; `ProjectLifecycleStatus.View/Create/Edit/ActivateDeactivate`; and `Workflow.View/Manage/Publish`. The per-entry baseline field and its policy use are removed.
3. **Bootstrap non-delegable allowlist.** `BootstrapPermissionDelegationPolicy` allowed first-grant delegation only for 21 listed keys: `ProjectPlan.Create`, `Planning.View/Edit`; `ProjectAnnualBudget.View/Create/EditDraft/ConfigureDraftApproval/SubmitForReview/Approve/ReviseApproved/ManageApprovalConfiguration/Delete/Withdraw`; `ProjectAnnualBudgetClosing.View/Close/CloseApprove/CarryForward`; `ProjectFinancialFoundation.View`; `CostUtilization.Export`; `ProgramFinancialRollup.View`; and `PortfolioFinancialRollup.View`. It required `Roles.ConfigurePermissions` and `TenantAdministration.ManageScope`, honored Active Deny, and let the actor delegate these keys without holding them. Its separate protected-Admin exception covered 13 Annual Budget/Closing keys, excluding `ProjectAnnualBudget.ConfigureDraftApproval`. The allowlist and exception are removed.
4. **Role editor restriction by module and Role type.** Administration-module permissions could be delegated only when the actor already held each permission; ordinary custom-role delegation used a separate rule and first-grant exceptions. The module-based grant restriction is removed. Custom Role category is no longer used to decide permission grantability.
5. **Catalog/UI lock metadata.** `PermissionCatalogEntry.IsAssignable` and `IsTenantAdminBaseline`, `IsMandatoryForSystemAdmin` response metadata, and the protected Admin permission locks/labels in Roles and User Permissions screens no longer define grantability. The audited catalog had no `IsAssignable: false` entries, but the field was an independent hard-policy switch and has been removed from the catalog definition. Compatibility `IsAssignable` values now reflect active status; actual protection comes from the central policy.
6. **Seed-time protected Tenant Admin reconciliation.** The previous seed path added the catalog-derived mandatory baseline and the supplemental `ProjectSettings.View` / `ProjectSettings.Edit` keys to an existing protected Tenant Admin Role, archived Deny overrides for baseline permissions, and audited those changes. That dynamic append and Deny cleanup are removed. Existing Role IDs, memberships, grants, and overrides were not changed by this task; no seed ran.
7. **Last-admin permission trio.** `ProtectedTenantAdminPolicy` previously called an Admin "effective" only when its permissions included `Roles.ConfigurePermissions`, `Users.AssignRoles`, and `Users.SetStatus`. This was a hardcoded permission eligibility list. The safeguard now checks for an active assignment to the canonical Tenant Admin Role, independently of its current grants; no permission is protected by this check.

`AdminRoleSeedPermissionKeys` remains an explicit initial grant set for a newly created Tenant Admin Role. It is not consulted by the protection policy, does not repair existing grants, and was not executed. Seed-time starter grants are separate from runtime protection and authorization. The canonical Tenant Admin role identity remains protected; its permission grants do not.

The selectable custom Role categories remain Tenant, Organization, Management, Operation, and Business. They constrain Role creation metadata only. Tenant/Organization permission scope eligibility remains separately enforced to prevent Platform or cross-tenant expansion.

## Documents authorization root cause and correction

Documents endpoints require exact catalog permission keys (`Document.View`, `Document.Create`, `Document.Edit`, `Document.Archive`, and `Document.TemplateManage`) and retain their existing management-context and document ownership checks.

Two authorization hydration defects caused assigned permissions to be missing from request claims:

- `UserPermissionClaimsMiddleware` rebuilt claims before Management Context middleware, but did not resolve an Organization from a requested Project. Organization-owned Role grants therefore were not activated for project-scoped Documents requests.
- `UserEffectivePermissionService` treated a null `OrganizationId` as an unresolved legacy Role. For ordinary Tenant-owned Roles, null means Tenant ownership, so their explicitly granted permissions were incorrectly omitted.

The middleware now resolves an explicit Organization ID or the same-Tenant owning Organization for a request Project ID (`projectId`, `forProjectId`, route value, or `X-Nexus-Project-Id`). It rejects invalid, conflicting, unresolved, or cross-Organization context values. Effective permissions are then evaluated for that exact Organization. Ordinary null-Organization Roles are treated as Tenant-owned; unresolved legacy Roles remain inactive. Organization-owned Role grants activate only for their owner Organization. No global permission union was added, and Documents' management-scope/resource checks remain in place.

## Files changed for this work

- `apps/api/src/NEXUS.Modules.Identity/Application/PermissionProtectionPolicy.cs` - central policy and configuration validation.
- `apps/api/src/NEXUS.Modules.Identity/Application/OrganizationRolePermissionPolicy.cs` — Organization Role scope eligibility.
- `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`, `ProtectedTenantAdminPolicy.cs`, `RoleAdministrationDtos.cs`, `UserPermissionDto.cs`, `EffectivePermissionResolver.cs`, and `IUserEffectivePermissionService.cs` — removed legacy protection fields/unions and retained scoped authorization contracts.
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/BootstrapPermissionDelegationPolicy.cs` — removed.
- `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs`, `UserAdministrationService.cs`, `UserEffectivePermissionService.cs`, `UserPermissionClaimsMiddleware.cs`, and `TenantAdminMutationGuard.cs` - central grantability, scope hydration, and role-identity-based last-admin integrity.
- `apps/api/src/NEXUS.Modules.Identity/ModuleRegistration/IdentityModuleRegistration.cs` and `apps/api/src/NEXUS.Api/appsettings.json` — bind and validate the empty-by-default protection list.
- `apps/api/src/NEXUS.Api/Program.cs` — project Organization lookup is registered for permission hydration.
- `apps/api/tests/NEXUS.Api.Tests/Security/EffectivePermissionResolverTests.cs`, `PermissionCatalogArchitectureTests.cs`, `ProtectedTenantAdminIdentityTests.cs`, `ProjectAnnualBudgetRoleCategoryTests.cs`, and `apps/api/tests/NEXUS.Api.Tests/Integration/UserPermissionAdministrationTests.cs` - default grantability, explicit protected keys, Deny behavior, Tenant/Organization scope, Documents hydration, and admin integrity regression coverage.
- `apps/web-react/src/features/administration/RolesPage.tsx`, `AccessPages.tsx`, `apps/web-react/src/i18n/messages/en.json`, `ar.json`, and `apps/web-react/src/test/access-security-pages.test.tsx` — removed hardcoded UI locks and updated permission assignment behavior/tests.
- `docs/architecture/permission-model.md` — current policy addendum.

This repository also has concurrent working-tree changes in other areas. They were preserved and are not attributed to this audit.

## Verification and remaining limits

- Identity and Integrations project builds: passed after the final source edits, 0 warnings and 0 errors.
- API host isolated-output build: passed with 0 errors; it emitted 352 third-party IKVM compatibility warnings.
- Documents and Persistence project builds: passed in the current working tree, 0 warnings and 0 errors for each project.
- Focused API test project compilation: passed with analyzer/IKVM warnings and no compile errors.
- Focused API regression execution: not completed. VSTest and direct `vstest` attempts aborted before executing any test cases because the testhost did not connect within 90 seconds; an earlier isolated-output attempt also failed to connect after 180 seconds. No API regression case is reported as passed.
- React focused Roles & Permissions tests: 3 passed, 36 skipped.
- Full `access-security-pages.test.tsx`: not completed; the broad file still has unrelated/stale fixture failures and stalled after large DOM output.
- No API runtime/UAT or database verification was run. No database write, migration, reset, or seed was performed.
