# UPI Task Context Manifest

Task: **Fix React authentication initialization deadlock: AuthProvider restorationPromise knownInvalidSession guards loading transitions refresh failure network errors concurrent requests React Strict Mode and successful login redirect**
Primary module: **identity** (task-explicit-alias)
Architecture snapshot: `20261126T120055-8909287`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` | 1-14 | 303 | filename-match:auth, filename-match:authentication, primary-module, symbol-match:IAuthenticationService |
| `apps/web-react/src/auth/AuthProvider.tsx` | 1-218 | 2976 | filename-match:auth, filename-match:provider, primary-module, symbol-match:AuthStatus |
| `apps/web-react/src/auth/RequireFreshPermission.tsx` | 1-62 | 583 | filename-match:refresh, path-match:auth, primary-module, symbol-match:RequireFreshPermissionProps |
| `apps/api/tests/NEXUS.Api.Tests/Unit/AuthenticationPipelineTests.cs` | 1-324 | 3976 | filename-match:auth, filename-match:authentication, primary-module, symbol-match:AuthenticationPipelineTests |
| `apps/web-react/src/test/auth-session-isolation.test.tsx` | 215-225, 259-268 | 238 | filename-match:auth, filename-match:session, primary-module, symbol-match:AuthorizationProbe |
| `apps/api/src/NEXUS.Modules.Identity/Application/IAuthorizationService.cs` | 1-8 | 126 | filename-match:auth, primary-module, symbol-match:IAuthorizationService |
| `apps/api/src/NEXUS.Modules.Identity/Application/IUserSessionValidator.cs` | 1-6 | 62 | filename-match:session, primary-module, symbol-match:IUserSessionValidator |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginCommand.cs` | 3-3 | 24 | filename-match:login, primary-module, symbol-match:LoginCommand |
| `apps/api/src/NEXUS.Modules.Identity/Application/LoginResponseDto.cs` | 5-13 | 108 | filename-match:login, primary-module, symbol-match:LoginResponseDto |
| `apps/api/src/NEXUS.Modules.Identity/Application/RefreshTokenRequest.cs` | 3-3 | 19 | filename-match:refresh, primary-module, symbol-match:RefreshTokenRequest |
| `apps/api/src/NEXUS.Modules.Identity/Domain/UserSession.cs` | 1-16 | 156 | filename-match:session, primary-module, symbol-match:UserSession |
| `apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserSessionValidator.cs` | 1-32 | 345 | filename-match:session, primary-module, symbol-match:UserSessionValidator |
| `apps/web-react/src/auth/RequireAuth.tsx` | 1-39 | 514 | filename-match:auth, primary-module, symbol-match:RequireAuth |
| `apps/web-react/src/auth/RequirePermission.tsx` | 18-20 | 24 | path-match:auth, primary-module, symbol-match:RedirectToHome |
| `apps/api/src/NEXUS.Api/Account/AccountDtos.cs` | 52-52, 73-73 | 32 | primary-module, symbol-match:AccountSessionDto |
| `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` | 69-69 | 42 | primary-module, symbol-match:InvalidOperationException |
| `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` | 27-27, 35-35 | 72 | primary-module, symbol-match:InvalidOperationException |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; AuthenticationSource)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserOrganization.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserRole.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
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

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9600** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
