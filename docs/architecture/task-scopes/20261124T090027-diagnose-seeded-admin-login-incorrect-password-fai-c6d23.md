# UPI Task Context Manifest

Task: **Diagnose seeded Admin login incorrect-password failure; inspect Admin seeding idempotency and authentication checks; prepare safe Development-only Identity password reset procedure for Product Owner local execution**
Primary module: **identity** (task-explicit-alias)
Architecture snapshot: `20261124T085908-1fc6a7e`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DevelopmentAdminTemporaryPasswordBypass.cs` | 1-20 | 225 | filename-match:admin, filename-match:development, filename-match:password, module-mentioned, primary-module, symbol-match:DevelopmentAdminTemporaryPasswordBypass |
| `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` | 8-13 | 78 | filename-match:admin, filename-match:development, filename-match:seeded, module-mentioned, primary-module, symbol-match:SeededDevelopmentAdmin |
| `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs` | 1-16 | 182 | filename-match:local, filename-match:password, module-mentioned, primary-module, symbol-match:LocalPasswordPolicyOptions |
| `apps/api/tests/NEXUS.Api.Tests/Security/ProtectedTenantAdminIdentityTests.cs` | 1-65 | 647 | filename-match:admin, filename-match:identity, module-mentioned, primary-module, symbol-match:ProtectedTenantAdminIdentityTests |
| `apps/api/src/NEXUS.Modules.Identity/Application/IRoleAdministrationService.cs` | 1-13 | 280 | filename-match:admin, module-mentioned, primary-module, symbol-match:IRoleAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` | 1-14 | 328 | filename-match:admin, module-mentioned, primary-module, symbol-match:IUserAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` | 1-77 | 920 | filename-match:admin, module-mentioned, primary-module, symbol-match:ProtectedTenantAdminPolicy |
| `apps/api/src/NEXUS.Modules.Identity/Application/UserAdminDto.cs` | 5-19, 46-51 | 249 | filename-match:admin, module-mentioned, primary-module, symbol-match:UserAdminDto |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/InitialPasswordEnforcementMiddleware.cs` | 1-142 | 1821 | filename-match:password, module-mentioned, primary-module, symbol-match:InitialPasswordEnforcementMiddleware |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/TemporaryPasswordGenerator.cs` | 1-36 | 376 | filename-match:password, module-mentioned, primary-module, symbol-match:TemporaryPasswordGenerator |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/TenantAdminMutationGuard.cs` | 1-118 | 1303 | filename-match:admin, module-mentioned, primary-module, symbol-match:TenantAdminMutationGuard |
| `apps/web-react/src/features/account/ChangeInitialPasswordPage.tsx` | 1-107 | 1948 | filename-match:password, module-mentioned, primary-module, symbol-match:ChangeInitialPasswordPage |
| `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationDtos.cs` | 1-56 | 677 | filename-match:admin, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` | 1-14 | 303 | filename-match:authentication, module-mentioned, primary-module, symbol-match:IAuthenticationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginCommand.cs` | 3-3 | 24 | filename-match:login, module-mentioned, primary-module, symbol-match:LoginCommand |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginResponseDto.cs` | 1-17 | 189 | filename-match:login, module-mentioned, primary-module, symbol-match:LoginResponseDto |
| `packages/shared-types/src/types/identity.ts` | 39-43, 45-48 | 50 | filename-match:identity, module-mentioned, primary-module, symbol-match:LoginRequest |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Application/CurrentUserDto.cs` — REFERENCES_TYPE (inferred-token-match; CurrentUserDto)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAggregateRoot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IApprovalDelegationEligibilityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAuditable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ICostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IMeetingPublicIdLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IOrganizationLifecycleHandler.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectAudienceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectFinancialLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectOrganizationLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectPlanMilestoneLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectPlanProgressLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectResourcePlanningContextLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IResourceReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ISoftDeletable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ITenantRegistryStatusValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IUserReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemAssigneeLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/PagedResult.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9600** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
