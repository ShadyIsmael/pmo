# UPI Task Context Manifest

Task: **Login 401 & Invalid Session Recovery**
Primary module: **identity** (task-explicit-alias)
Architecture snapshot: `20261123T230520-4fdc8b8`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserSessionValidator.cs` | 1-6 | 62 | filename-match:session, primary-module, symbol-match:IUserSessionValidator |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginCommand.cs` | 3-3 | 24 | filename-match:login, primary-module, symbol-match:LoginCommand |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginResponseDto.cs` | 5-13 | 108 | filename-match:login, primary-module, symbol-match:LoginResponseDto |
| `apps/api/src/NEXUS.Modules.Identity/Domain/UserSession.cs` | 1-16 | 156 | filename-match:session, primary-module, symbol-match:UserSession |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserSessionValidator.cs` | 1-32 | 345 | filename-match:session, primary-module, symbol-match:UserSessionValidator |
| `apps/web-react/src/test/auth-session-isolation.test.tsx` | 259-268 | 120 | filename-match:session, primary-module, symbol-match:SessionControls |
| `apps/api/src/NEXUS.Api/Account/AccountController.cs` | 40-45, 48-55, 58-64 | 441 | primary-module, symbol-match:GetSessions |
| `apps/api/src/NEXUS.Api/Account/AccountDtos.cs` | 52-52, 73-73 | 32 | primary-module, symbol-match:AccountSessionDto |
| `apps/api/src/NEXUS.Api/Account/AccountService.cs` | 279-285 | 202 | primary-module, symbol-match:GetSessionsAsync |
| `apps/api/src/NEXUS.Api/Account/IAccountService.cs` | 10-10, 11-11, 12-12 | 164 | primary-module, symbol-match:GetSessionsAsync |
| `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` | 7-7, 11-11 | 67 | primary-module, symbol-match:LoginAsync |
| `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` | 69-69 | 42 | primary-module, symbol-match:InvalidOperationException |
| `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` | 27-27, 35-35 | 72 | primary-module, symbol-match:InvalidOperationException |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs` | 730-730 | 38 | primary-module, symbol-match:InvalidOperationException |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/TenantAdminMutationGuard.cs` | 116-116 | 41 | primary-module, symbol-match:InvalidOperationException |
| `apps/web-react/src/api/account.ts` | 29-36, 95-98, 100-103 | 187 | primary-module, symbol-match:AccountSession |
| `apps/web-react/src/auth/AuthProvider.tsx` | 63-70, 76-105 | 420 | primary-module, symbol-match:expireSession |
| `apps/web-react/src/features/account/AccountPages.tsx` | 191-197 | 117 | primary-module, symbol-match:revokeOtherSessions |
| `apps/web-react/src/test/p0-07-auth-session-contract.test.ts` | 1-109 | 1778 | filename-match:session, primary-module |
| `packages/shared-types/src/types/identity.ts` | 39-43, 45-48 | 50 | primary-module, symbol-match:LoginRequest |
| `apps/api/tests/NEXUS.Api.Tests/Integration/UserPermissionAdministrationTests.cs` | 398-427, 430-464, 467-477, 480-493, 1893-1908 | 1400 | primary-module, symbol-match:Middleware_rejects_revoked_access_token_session |
| `apps/api/tests/NEXUS.Api.Tests/Security/AuthCookieContractTests.cs` | 44-72, 75-93, 154-185, 188-205, 208-232 | 2141 | primary-module, symbol-match:Login_sets_a_secure_httponly_host_cookie_and_redacts_refresh_secret_from_json |
| `apps/api/tests/NEXUS.Api.Tests/Unit/AccountControllerTests.cs` | 15-30, 33-42, 64-73 | 586 | primary-module, symbol-match:Get_is_bound_to_the_authenticated_user_tenant_and_session_claims |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; AuthenticationSource)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserOrganization.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserRole.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Location.cs` — REFERENCES_TYPE (inferred-token-match; Location)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)
- `apps/web-react/src/api/client.ts` — REFERENCES_TYPE (inferred-token-match; AuthenticationSource)
- `apps/web-react/src/lib/permissions.ts` — REFERENCES_TYPE (inferred-token-match; Role)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Location)
- `packages/shared-types/src/types/project.ts` — REFERENCES_TYPE (inferred-token-match; ProjectStatus)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditFieldRedactor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditSources.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; _audit:IAuditService.AppendAsync at line 262)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — REFERENCES_TYPE (inferred-token-match; IAuditService)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — REFERENCES_TYPE (inferred-token-match; User)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **8593** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
