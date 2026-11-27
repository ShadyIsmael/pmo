# UPI Task Context Manifest

Task: **Fix Development Admin recovery preflight failure: trace Recover-DevelopmentAdminPassword.ps1 child process command, exit/error handling, launchSettings environment, no-build output and Program.cs recovery entry point; preserve Development target checks and secure redirected password console.**
Primary module: **programs** (task-explicit-alias)
Architecture snapshot: `20261124T120021-db43106`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/ProgramManagement/IProgramManagementService.cs` | 1-18 | 150 | filename-match:program, primary-module, symbol-match:IProgramManagementService |
| `apps/api/src/NEXUS.Api/ProgramManagement/ProgramManagementController.cs` | 1-41 | 605 | filename-match:program, primary-module, symbol-match:ProgramManagementController |
| `apps/api/src/NEXUS.Api/ProgramManagement/ProgramManagementDtos.cs` | 3-18, 20-39, 41-54, 56-65, 67-76 | 982 | filename-match:program, primary-module, symbol-match:ProgramManagementListItemDto |
| `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioCashFlowAggregation.cs` | 1-410 | 5184 | filename-match:program, primary-module, symbol-match:ProgramPortfolioCashFlowAggregation |
| `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioCashFlowController.cs` | 1-99 | 1140 | filename-match:program, primary-module, symbol-match:ProgramPortfolioCashFlowController |
| `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioCashFlowDtos.cs` | 8-8, 21-21, 30-30, 45-45, 53-53 | 87 | filename-match:program, primary-module, symbol-match:ProgramPortfolioCashFlowProjectMembershipDto |
| `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioFinancialRollupDtos.cs` | 97-97 | 17 | filename-match:program, primary-module, symbol-match:ProgramPortfolioFinancialRollupDto |
| `apps/web-react/src/test/program-overview-parity.test.tsx` | 120-122 | 94 | filename-match:program, primary-module, symbol-match:renderProgram |

## Dependency awareness (not automatically read)

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
- `apps/api/src/NEXUS.SharedKernel/PagedResult.cs` — REFERENCES_TYPE (inferred-token-match; PagedResult)
- `apps/api/src/NEXUS.SharedKernel/PagedResult.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **8259** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
