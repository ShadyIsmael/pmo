# UPI Task Context Manifest

Task: **Apply pending Sprint 3 Task 05B refund governance EF migration to verified Development database Shady NexusPmo; verify EF migration history, model synchronization, refund schema; update existing Sprint 3 consolidated closure report.**
Primary module: **governance** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryDtos.cs` | 105-108, 110-123 | 234 | module-mentioned, path-match:governance, primary-module, symbol-match:AssignmentRecoveryHistoryDto |
| `apps/web-react/src/test/governance-recovery-navigation.test.tsx` | 19-48 | 327 | filename-match:governance, module-mentioned, primary-module, symbol-match:renderGovernance |
| `apps/api/tests/NEXUS.Api.Tests/Integration/AssignmentRecoveryServiceTests.cs` | 257-279, 282-318, 321-375, 465-487 | 2025 | module-mentioned, primary-module, symbol-match:InitialAssignmentConflict_WhenActiveManagerHistoryNeedsReconciliation_UsesStableBusinessCode |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryController.cs` — REFERENCES_TYPE (inferred-token-match; AssignProjectSponsorDto)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Governance)
- `apps/api/src/NEXUS.Api/Governance/AssignmentRecoveryService.cs` — REFERENCES_TYPE (inferred-token-match; AssignmentRecoveryCandidateDto)
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

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **2586** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
