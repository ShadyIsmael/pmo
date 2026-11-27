# UPI Task Context Manifest

Task: **Inspect ProjectPaymentReversalWorkflow and ProjectPaymentRequestDetail.ReversesRequestId to map approved Request Reversal Amount by original Payment Request for Sprint 5 Committed Unpaid calculation; confirm status and persisted DbSet paths.**
Primary module: **workflow** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/WorkflowDefinitionValidator.cs` | 1-151 | 2258 | filename-match:id, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowDefinitionValidator |
| `apps/web-react/src/features/workflow/WorkflowDetailPage.tsx` | 1-102 | 1580 | filename-match:detail, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowDetailPage |
| `apps/api/tests/NEXUS.Api.Tests/Unit/WorkflowDefinitionValidatorTests.cs` | 1-96 | 1218 | filename-match:id, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowDefinitionValidatorTests |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/ProjectWorkflowController.cs` | 1-32 | 491 | filename-match:workflow, module-mentioned, primary-module, symbol-match:ProjectWorkflowController |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/ProjectWorkflowContracts.cs` | 1-44 | 414 | filename-match:workflow, module-mentioned, primary-module, symbol-match:ProjectWorkflowConfigurationDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowExecutionContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowManagementContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; service:IProjectWorkflowConfigurationService.SaveAsync at line 21)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — REFERENCES_TYPE (inferred-token-match; ProjectWorkflowConfigurationDto)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowExecutionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowInstanceService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowManagementService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowTransitionQueryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/api/src/NEXUS.SharedKernel/Result.cs` — REFERENCES_TYPE (inferred-token-match; Result)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/ProjectWorkflowConfiguration.cs` — REFERENCES_TYPE (inferred-token-match; ProjectWorkflowModes)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/ProjectWorkflowConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **5961** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
