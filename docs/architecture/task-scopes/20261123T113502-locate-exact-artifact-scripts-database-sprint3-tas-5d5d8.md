# UPI Task Context Manifest

Task: **Locate exact artifact scripts/database/Sprint3Task05BRefundExecutionGovernance.sql generated for migration 20261123090738_Sprint3Task05BRefundExecutionGovernance; locate docs/finance/SPRINT-3-CONSOLIDATED-CLOSURE-REPORT.md.**
Primary module: **governance** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/test/governance-recovery-navigation.test.tsx` | 19-48 | 327 | filename-match:governance, module-mentioned, primary-module, symbol-match:renderGovernance |
| `apps/api/tests/NEXUS.Api.Tests/Integration/AssignmentRecoveryServiceTests.cs` | 465-487 | 398 | module-mentioned, primary-module, symbol-match:MissingGovernanceFilter_IncludesAnInactiveDirectManagerEvenWhenHistoryMatches |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryController.cs` — REFERENCES_TYPE (inferred-token-match; AssignmentRecoveryController)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Governance)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryDtos.cs` — REFERENCES_TYPE (inferred-token-match; CreateRecoveryAssignmentDto)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Governance)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryService.cs` — REFERENCES_TYPE (inferred-token-match; AssignmentRecoveryException)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Governance)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — REFERENCES_TYPE (inferred-token-match; Statuses)
- `apps/api/src/NEXUS.Modules.Identity/Application/CurrentUserDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/EffectivePermissionResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthorizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IRoleAdministrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserEffectivePermissionService.cs` — REFERENCES_TYPE (inferred-token-match; IUserEffectivePermissionService)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserEffectivePermissionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserRepository.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserSessionValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LocalPasswordPolicyOptions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LoginCommand.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/LoginResponseDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/ProtectedTenantAdminPolicy.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RefreshTokenRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RegisterCommand.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RoleAdministrationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/RoleCategories.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/SeededDevelopmentAdmin.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **725** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
