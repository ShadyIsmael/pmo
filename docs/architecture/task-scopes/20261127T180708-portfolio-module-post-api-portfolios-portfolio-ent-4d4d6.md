# UPI Task Context Manifest

Task: **Portfolio module: POST /api/portfolios, Portfolio entity, PortfolioService, portfolio create React sheet. Require owning org, choose creator eligible org automatically only if exactly one. Program creation inherits Portfolio scope. Project creation resolves direct Portfolio or Program hierarchy, retains Project.Create contextual lookups, and assigns Pre-Initiation.**
Primary module: **portfolios** (task-explicit-alias)
Architecture snapshot: `20261127T141352-618e703`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Security/PortfolioProgramStatusScopeTests.cs` | 1-180 | 2616 | filename-match:portfolio, filename-match:program, filename-match:scope, module-mentioned, primary-module, symbol-match:PortfolioProgramStatusScopeTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/PortfolioProgramAuthorizationTests.cs` | 1-285 | 3309 | filename-match:portfolio, filename-match:program, module-mentioned, primary-module, symbol-match:PortfolioProgramAuthorizationTests |
| `apps/web-react/src/test/portfolio-sheet.test.tsx` | 43-47 | 112 | filename-match:portfolio, filename-match:portfolios, filename-match:sheet, module-mentioned, primary-module, symbol-match:renderSheet |
| `apps/api/src/NEXUS.Modules.Portfolio/Application/IPortfolioService.cs` | 1-14 | 293 | filename-match:portfolio, filename-match:portfolios, module-mentioned, primary-module, symbol-match:IPortfolioService |
| `apps/api/src/NEXUS.Modules.Portfolio/Domain/PortfolioSnapshot.cs` | 1-18 | 204 | filename-match:portfolio, filename-match:portfolios, module-mentioned, primary-module, symbol-match:PortfolioSnapshot |
| `packages/api-client/src/portfolios.ts` | 4-14 | 105 | filename-match:portfolio, filename-match:portfolios, module-mentioned, primary-module, symbol-match:createPortfoliosApi |
| `apps/api/src/NEXUS.Api/PortfolioManagement/IPortfolioManagementService.cs` | 1-9 | 132 | filename-match:portfolio, module-mentioned, primary-module, symbol-match:IPortfolioManagementService |
| `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementController.cs` | 1-44 | 659 | filename-match:portfolio, module-mentioned, primary-module, symbol-match:PortfolioManagementController |
| `apps/api/src/NEXUS.Api/PortfolioManagement/PortfolioManagementDtos.cs` | 3-19, 21-45, 47-62, 64-77, 79-95 | 1306 | filename-match:portfolio, module-mentioned, primary-module, symbol-match:PortfolioManagementListItemDto |
| `apps/api/src/NEXUS.Modules.Portfolio/Application/PortfolioDtos.cs` | 1-75 | 822 | filename-match:portfolio, module-mentioned, primary-module, symbol-match:PortfolioProjectDto |

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
- `apps/api/src/NEXUS.SharedKernel/IUserOrganizationScopeLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IUserReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemAssigneeLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IWorkItemReferenceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/PagedResult.cs` — REFERENCES_TYPE (inferred-token-match; PagedResult)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9558** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
