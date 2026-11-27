# UPI Task Context Manifest

Task: **Program and Portfolio hierarchy aggregate view: projects linked directly to a Portfolio and projects linked through its Programs must contribute once. Identify the existing rollup service and its focused test.**
Primary module: **portfolios** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Security/PortfolioProgramStatusScopeTests.cs` | 1-180 | 2616 | filename-match:portfolio, filename-match:program, filename-match:programs, primary-module, symbol-match:PortfolioProgramStatusScopeTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/PortfolioProgramAuthorizationTests.cs` | 1-285 | 3309 | filename-match:portfolio, filename-match:program, primary-module, symbol-match:PortfolioProgramAuthorizationTests |
| `apps/api/src/NEXUS.Api/PortfolioManagement/IPortfolioManagementService.cs` | 1-9 | 132 | filename-match:portfolio, primary-module, symbol-match:IPortfolioManagementService |
| `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementController.cs` | 1-44 | 659 | filename-match:portfolio, primary-module, symbol-match:PortfolioManagementController |
| `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementDtos.cs` | 3-19, 21-45, 47-62, 64-77, 79-95 | 1306 | filename-match:portfolio, primary-module, symbol-match:PortfolioManagementListItemDto |
| `apps/api/src/NEXUS.Modules.Portfolio/Api/PortfolioController.cs` | 1-48 | 932 | filename-match:portfolio, primary-module, symbol-match:PortfolioController |
| `apps/api/src/NEXUS.Modules.Portfolio/Application/IPortfolioService.cs` | 1-14 | 293 | filename-match:portfolio, primary-module, symbol-match:IPortfolioService |
| `apps/api/src/NEXUS.Modules.Portfolio/Domain/PortfolioBudget.cs` | 1-15 | 143 | filename-match:portfolio, primary-module, symbol-match:PortfolioBudget |
| `apps/api/src/NEXUS.Modules.Portfolio/Domain/PortfolioHealth.cs` | 1-15 | 147 | filename-match:portfolio, primary-module, symbol-match:PortfolioHealth |

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

- Initial selected source: **9537** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
