# UPI Task Context Manifest

Task: **DevelopmentAdminRecoveryConsole.cs DevelopmentAdminPasswordRecoveryService.cs Program.cs Recover-DevelopmentAdminPassword.ps1 and focused password recovery tests: diagnose hidden password input, accept secure PowerShell Read-Host input through stdin, preserve password confirmation and Identity policy, Development Shady NexusPmo canonical Admin validation.**
Primary module: **identity** (task-explicit-alias)
Architecture snapshot: `20261124T120021-db43106`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DevelopmentAdminTemporaryPasswordBypass.cs` | 1-20 | 225 | filename-match:admin, filename-match:development, filename-match:password, module-mentioned, primary-module, symbol-match:DevelopmentAdminTemporaryPasswordBypass |
| `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` | 8-13 | 78 | filename-match:admin, filename-match:development, module-mentioned, primary-module, symbol-match:SeededDevelopmentAdmin |
| `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs` | 1-16 | 182 | filename-match:password, filename-match:policy, module-mentioned, primary-module, symbol-match:LocalPasswordPolicyOptions |
| `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` | 1-77 | 920 | filename-match:admin, filename-match:policy, module-mentioned, primary-module, symbol-match:ProtectedTenantAdminPolicy |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/InitialPasswordEnforcementMiddleware.cs` | 1-142 | 1821 | filename-match:password, module-mentioned, primary-module, symbol-match:InitialPasswordEnforcementMiddleware |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/TemporaryPasswordGenerator.cs` | 1-36 | 376 | filename-match:password, module-mentioned, primary-module, symbol-match:TemporaryPasswordGenerator |
| `apps/web-react/src/features/account/ChangeInitialPasswordPage.tsx` | 1-107 | 1948 | filename-match:password, module-mentioned, primary-module, symbol-match:ChangeInitialPasswordPage |
| `apps/api/tests/NEXUS.Api.Tests/Security/ProtectedTenantAdminIdentityTests.cs` | 1-65 | 647 | filename-match:admin, filename-match:identity, module-mentioned, primary-module, symbol-match:ProtectedTenantAdminIdentityTests |
| `apps/api/src/NEXUS.Modules.Identity/Application/IRoleAdministrationService.cs` | 1-13 | 280 | filename-match:admin, module-mentioned, primary-module, symbol-match:IRoleAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` | 1-14 | 328 | filename-match:admin, module-mentioned, primary-module, symbol-match:IUserAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/UserAdminDto.cs` | 5-19, 46-51 | 249 | filename-match:admin, module-mentioned, primary-module, symbol-match:UserAdminDto |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/TenantAdminMutationGuard.cs` | 1-118 | 1303 | filename-match:admin, module-mentioned, primary-module, symbol-match:TenantAdminMutationGuard |
| `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationDtos.cs` | 1-56 | 677 | filename-match:admin, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Modules.Identity/NEXUS.Modules.Identity.csproj` | 1-18 | 245 | filename-match:identity, filename-match:nexus, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Api/Account/AccountController.cs` | 31-37, 66-75 | 307 | module-mentioned, primary-module, symbol-match:ChangePassword |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Account/AccountDtos.cs` — REFERENCES_TYPE (inferred-token-match; ChangeAccountPasswordRequest)
- `apps/api/src/NEXUS.Api/Account/IAccountService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; accountService:IAccountService.RevokeOtherSessionsAsync at line 61)
- `apps/api/src/NEXUS.Api/Account/IAccountService.cs` — REFERENCES_TYPE (inferred-token-match; IAccountService)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/web-react/src/api/account.ts` — REFERENCES_TYPE (inferred-token-match; ChangeAccountPasswordRequest)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
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
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — REFERENCES_TYPE (inferred-token-match; LocalPasswordPolicyOptions)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — REFERENCES_TYPE (inferred-token-match; SeededDevelopmentAdmin)
- `apps/api/src/NEXUS.Api/Documents/TemplateDeletionApproverResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9586** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
