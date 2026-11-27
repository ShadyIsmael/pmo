# UPI Task Context Manifest

Task: **Identify Development API authentication login route and authenticated current-user endpoint for programmatic verification of membership and permissions**
Primary module: **identity** (task-explicit-alias)
Architecture snapshot: `20261127T141352-618e703`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Identity/Application/CurrentUserDto.cs` | 5-18 | 183 | filename-match:current, filename-match:user, primary-module, symbol-match:CurrentUserDto |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserEffectivePermissionService.cs` | 1-7 | 108 | filename-match:permissions, filename-match:user, primary-module, symbol-match:IUserEffectivePermissionService |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserEffectivePermissionService.cs` | 1-108 | 1579 | filename-match:permissions, filename-match:user, primary-module, symbol-match:UserEffectivePermissionService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` | 1-14 | 303 | filename-match:authentication, primary-module, symbol-match:IAuthenticationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` | 1-14 | 328 | filename-match:user, primary-module, symbol-match:IUserAdministrationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserRepository.cs` | 1-19 | 383 | filename-match:user, primary-module, symbol-match:IUserRepository |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserSessionValidator.cs` | 1-6 | 62 | filename-match:user, primary-module, symbol-match:IUserSessionValidator |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginCommand.cs` | 3-3 | 24 | filename-match:login, primary-module, symbol-match:LoginCommand |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginResponseDto.cs` | 5-13 | 108 | filename-match:login, primary-module, symbol-match:LoginResponseDto |
| `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` | 8-13 | 78 | filename-match:development, primary-module, symbol-match:SeededDevelopmentAdmin |
| `apps/api/src/NEXUS.Modules.Identity/Application/UserAdminDto.cs` | 1-51 | 521 | filename-match:user, primary-module, symbol-match:UserAdminDto |
| `apps/api/src/NEXUS.Modules.Identity/Application/UserLookupDto.cs` | 1-15 | 178 | filename-match:user, primary-module, symbol-match:WorkflowUserLookupDto |
| `apps/api/src/NEXUS.Modules.Identity/Application/UserPermissionDto.cs` | 1-33 | 336 | filename-match:user, primary-module, symbol-match:UserPermissionAssignmentDto |
| `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` | 1-29 | 357 | filename-match:user, primary-module, symbol-match:User |
| `apps/api/src/NEXUS.Modules.Identity/Domain/UserOrganization.cs` | 5-9 | 37 | filename-match:user, primary-module, symbol-match:UserOrganization |
| `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` | 12-19 | 98 | filename-match:user, primary-module, symbol-match:UserPermissionOverride |
| `apps/api/src/NEXUS.Modules.Identity/Domain/UserRole.cs` | 5-11 | 58 | filename-match:user, primary-module, symbol-match:UserRole |
| `apps/api/src/NEXUS.Modules.Identity/Domain/UserSession.cs` | 1-16 | 156 | filename-match:user, primary-module, symbol-match:UserSession |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/DevelopmentAdminTemporaryPasswordBypass.cs` | 1-20 | 225 | filename-match:development, primary-module, symbol-match:DevelopmentAdminTemporaryPasswordBypass |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserReferenceLookup.cs` | 1-45 | 617 | filename-match:user, primary-module, symbol-match:UserReferenceLookup |
| `apps/api/src/NEXUS.Api/Account/AccountDtos.cs` | 29-29, 31-31, 32-32, 34-34 | 116 | primary-module, symbol-match:AccountTenantMembershipDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Location.cs` — REFERENCES_TYPE (inferred-token-match; Location)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/web-react/src/api/account.ts` — REFERENCES_TYPE (inferred-token-match; ChangeAccountPasswordRequest)
- `apps/web-react/src/api/client.ts` — REFERENCES_TYPE (inferred-token-match; AuthenticationSource)
- `apps/web-react/src/lib/permissions.ts` — REFERENCES_TYPE (inferred-token-match; Role)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; Role)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Location)
- `packages/shared-types/src/types/project.ts` — REFERENCES_TYPE (inferred-token-match; ProjectStatus)
- `apps/web-react/src/features/administration/api/admin.ts` — REFERENCES_TYPE (inferred-token-match; RoleDto)
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
- `apps/api/src/NEXUS.Api/Account/AccountController.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — REFERENCES_TYPE (inferred-token-match; UserSession)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **5855** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
