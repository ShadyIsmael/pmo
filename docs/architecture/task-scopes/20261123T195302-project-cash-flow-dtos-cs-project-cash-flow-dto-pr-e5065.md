# UPI Task Context Manifest

Task: **ProjectCashFlowDtos.cs ProjectCashFlowDto ProjectCashFlowCurrencyGroupDto CashFlowBucketDto CashFlowUndatedTotalsDto CashFlowLineDto CashFlowQualityIssueDto CashFlowDataQualityStatus; input and response contract for Program Portfolio aggregation.**
Primary module: **portfolios** (task-explicit-alias)
Architecture snapshot: `20261123T195019-9716fd7`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementDtos.cs` | 3-19, 21-45, 47-62, 64-77, 79-95 | 1306 | filename-match:dto, filename-match:dtos, filename-match:portfolio, primary-module, symbol-match:PortfolioManagementListItemDto |
| `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioDtos.cs` | 1-75 | 822 | filename-match:dto, filename-match:dtos, filename-match:portfolio, primary-module, symbol-match:PortfolioProjectDto |
| `apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramDtos.cs` | 1-63 | 644 | filename-match:dto, filename-match:dtos, filename-match:program, primary-module, symbol-match:ProgramDto |
| `apps/api/src/NEXUS.Modules.Portfolio/Application/ManagerHandoverDtos.cs` | 1-92 | 1029 | filename-match:dto, filename-match:dtos, primary-module |
| `apps/api/src/NEXUS.Api/PortfolioManagement/IPortfolioManagementService.cs` | 1-9 | 132 | filename-match:portfolio, primary-module, symbol-match:IPortfolioManagementService |
| `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementController.cs` | 1-44 | 659 | filename-match:portfolio, primary-module, symbol-match:PortfolioManagementController |
| `apps/api/src/NEXUS.Modules.Portfolio/Domain/ManagerHandoverStatus.cs` | 1-9 | 80 | filename-match:status, primary-module, symbol-match:ManagerHandoverStatus |
| `packages/api-client/src/portfolios.ts` | 4-14 | 105 | filename-match:portfolio, primary-module, symbol-match:createPortfoliosApi |

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

- Initial selected source: **4777** tokens (method varies per file)
- Requested planning budget: **8000** tokens
- Other-context allowance: **3200** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
