# UPI Task Context Manifest

Task: **Fix Development Admin recovery backup SQL result type conversion in SeedDataProvider; add focused regression test; prepare authorized Development-only full reset and official minimal seed PowerShell execution script with identity checks and post-reset verification**
Primary module: **identity** (task-explicit-alias)
Architecture snapshot: `20261124T143831-c08b56f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` | 8-13 | 78 | filename-match:admin, filename-match:development, filename-match:seed, module-mentioned, primary-module, symbol-match:SeededDevelopmentAdmin |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DevelopmentAdminTemporaryPasswordBypass.cs` | 1-20 | 225 | filename-match:admin, filename-match:development, module-mentioned, primary-module, symbol-match:DevelopmentAdminTemporaryPasswordBypass |
| `apps/api/tests/NEXUS.Api.Tests/Security/ProtectedTenantAdminIdentityTests.cs` | 1-65 | 647 | filename-match:admin, filename-match:identity, module-mentioned, primary-module, symbol-match:ProtectedTenantAdminIdentityTests |
| `apps/api/src/NEXUS.Modules.Identity/Application/IRoleAdministrationService.cs` | 1-13 | 280 | filename-match:admin, module-mentioned, primary-module, symbol-match:IRoleAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` | 1-14 | 328 | filename-match:admin, module-mentioned, primary-module, symbol-match:IUserAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` | 1-77 | 920 | filename-match:admin, module-mentioned, primary-module, symbol-match:ProtectedTenantAdminPolicy |
| `apps/api/src/NEXUS.Modules.Identity/Application/UserAdminDto.cs` | 5-19, 46-51 | 249 | filename-match:admin, module-mentioned, primary-module, symbol-match:UserAdminDto |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/TenantAdminMutationGuard.cs` | 1-118 | 1303 | filename-match:admin, module-mentioned, primary-module, symbol-match:TenantAdminMutationGuard |
| `apps/api/src/NEXUS.Modules.Identity/ModuleRegistration/IdentityModuleRegistration.cs` | 1-42 | 630 | filename-match:identity, module-mentioned, primary-module, symbol-match:IdentityModuleRegistration |
| `apps/web-react/src/auth/AuthProvider.tsx` | 1-201 | 2722 | filename-match:provider, module-mentioned, primary-module, symbol-match:AuthProvider |
| `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationDtos.cs` | 1-56 | 677 | filename-match:admin, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Modules.Identity/NEXUS.Modules.Identity.csproj` | 1-18 | 245 | filename-match:identity, module-mentioned, primary-module |
| `packages/shared-types/src/types/identity.ts` | 1-65 | 335 | filename-match:identity, module-mentioned, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Unit/DirectoryIdentityMatcherTests.cs` | 1-46 | 514 | filename-match:identity, module-mentioned, primary-module, symbol-match:DirectoryIdentityMatcherTests |
| `apps/api/src/NEXUS.Api/Account/AccountController.cs` | 66-75 | 152 | module-mentioned, primary-module, symbol-match:TryGetIdentity |
| `apps/api/src/NEXUS.Modules.Identity/Api/AuthController.cs` | 150-153, 163-168, 179-179, 206-210 | 244 | module-mentioned, primary-module, symbol-match:Unauthorized |
| `apps/api/src/NEXUS.Modules.Identity/Api/RolesController.cs` | 71-71 | 28 | module-mentioned, primary-module, symbol-match:CreatedAtAction |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Account/AccountDtos.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Api/Account/IAccountService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; accountService:IAccountService.RevokeOtherSessionsAsync at line 61)
- `apps/api/src/NEXUS.Api/Account/IAccountService.cs` — REFERENCES_TYPE (inferred-token-match; IAccountService)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/web-react/src/api/account.ts` — REFERENCES_TYPE (inferred-token-match; ChangeAccountPasswordRequest)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.Modules.Identity/Application/CurrentUserDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/EffectivePermissionResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; _authenticationService:IAuthenticationService.SwitchRoleAsync at line 208)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` — REFERENCES_TYPE (inferred-token-match; IAuthenticationService)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthorizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserEffectivePermissionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserRepository.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserSessionValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs` — REFERENCES_TYPE (inferred-token-match; LocalPasswordPolicyOptions)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Documents/TemplateDeletionApproverResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Api/Documents/TemplateDeletionApproverResolver.cs` — REFERENCES_TYPE (inferred-token-match; Permission)
- `apps/api/src/NEXUS.Api/Documents/TemplateDeletionWorkflowService.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — REFERENCES_TYPE (inferred-token-match; User)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9577** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
