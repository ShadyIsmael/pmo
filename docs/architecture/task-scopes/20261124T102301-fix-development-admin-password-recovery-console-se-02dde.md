# UPI Task Context Manifest

Task: **Fix Development Admin Password Recovery Console secure input, focused validation, and PowerShell recovery procedure**
Primary module: **administration** (task-explicit-alias)
Architecture snapshot: `20261124T091539-11ef236`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` | 1-123 | 1622 | filename-match:admin, filename-match:development, filename-match:password, filename-match:recovery, primary-module, symbol-match:DevelopmentAdminPasswordRecoveryService |
| `apps/api/src/NEXUS.Api/Development/DevelopmentAdminRecoveryConsole.cs` | 1-99 | 1172 | filename-match:admin, filename-match:console, filename-match:development, filename-match:recovery, primary-module, symbol-match:DevelopmentAdminRecoveryConsole |
| `apps/web-react/src/features/administration/api/assignmentRecovery.ts` | 1-186 | 2320 | filename-match:recovery, primary-module, symbol-match:RecoveryEntityType |
| `apps/web-react/src/features/administration/AdministrationHub.tsx` | 1-77 | 2175 | filename-match:admin, primary-module, symbol-match:AdministrationArea |
| `apps/web-react/src/features/administration/api/admin.ts` | 5-5, 6-6, 7-7, 8-8, 9-9 | 211 | filename-match:admin, primary-module, symbol-match:AdminUserDto |
| `packages/api-client/src/admin.ts` | 4-14 | 113 | filename-match:admin, primary-module, symbol-match:createAdminApi |
| `apps/web-react/src/features/administration/DocumentCategoriesPage.tsx` | 25-27 | 58 | primary-module, symbol-match:SaveDocumentCategoryInput |
| `apps/web-react/src/features/administration/RolesPage.tsx` | 25-27, 81-92 | 213 | primary-module, symbol-match:SaveRoleInput |
| `apps/web-react/src/features/administration/WorkItemStatusesPage.tsx` | 23-31 | 116 | primary-module, symbol-match:SaveWorkItemStatusInput |
| `apps/web-react/src/features/administration/api/directoryIntegration.ts` | 12-16 | 104 | primary-module, symbol-match:SaveDirectoryConnectionInput |

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

- Initial selected source: **8104** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
