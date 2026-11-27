# UPI Task Context Manifest

Task: **login fails for admin@nexus.com with password P@ssw0rd or Pssw0rd, identify root cause**
Primary module: **administration** (task-explicit-alias)
Architecture snapshot: `20261125T190942-cd9ddbb`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` | 1-123 | 1622 | filename-match:admin, filename-match:password, primary-module, symbol-match:DevelopmentAdminPasswordRecoveryService |
| `apps/api/src/NEXUS.Api/Development/DevelopmentAdminRecoveryConsole.cs` | 1-156 | 1706 | filename-match:admin, primary-module, symbol-match:DevelopmentAdminRecoveryConsole |
| `apps/web-react/src/features/administration/AdministrationHub.tsx` | 1-77 | 2175 | filename-match:admin, primary-module, symbol-match:AdministrationArea |
| `apps/web-react/src/features/administration/api/admin.ts` | 5-5, 6-6, 7-7, 8-8, 9-9 | 211 | filename-match:admin, primary-module, symbol-match:AdminUserDto |
| `packages/api-client/src/admin.ts` | 4-14 | 113 | filename-match:admin, primary-module, symbol-match:createAdminApi |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Application/CurrentUserDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/EffectivePermissionResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthorizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IRoleAdministrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserEffectivePermissionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserRepository.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserSessionValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs` — REFERENCES_TYPE (inferred-token-match; LocalPasswordPolicyOptions)
- `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LoginCommand.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LoginResponseDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RefreshTokenRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RegisterCommand.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RoleCategories.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` — REFERENCES_TYPE (inferred-token-match; SeededDevelopmentAdmin)
- `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/UserAdminDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/UserLookupDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/UserPermissionDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; AuthenticationSource)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **5827** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
