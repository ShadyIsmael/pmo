# UPI Task Context Manifest

Task: **Map approved project Payment Request reversal/release persisted fields and workflow into the Cash Flow approved unpaid release fact; identify exact query path and distinguish returned/rejected/cancelled versus approved reversal amounts.**
Primary module: **workflow** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowTransitionQueryService.cs` | 1-138 | 1616 | filename-match:flow, filename-match:query, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowTransitionQueryService |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/ProjectWorkflowController.cs` | 1-32 | 491 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:ProjectWorkflowController |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/WorkflowEntityController.cs` | 1-206 | 2407 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowEntityController |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowExecutionContracts.cs` | 7-7, 17-17, 30-30, 59-59, 67-67 | 70 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowTransitionRequest |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowManagementDtos.cs` | 5-5, 19-19, 35-35, 50-50, 64-64 | 72 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowDefinitionListItemDto |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowTransitionAction.cs` | 1-13 | 132 | filename-match:flow, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowTransitionAction |

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
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/ProjectWorkflowContracts.cs` — REFERENCES_TYPE (inferred-token-match; SaveProjectWorkflowConfigurationRequest)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/ProjectWorkflowContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowManagementContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Contracts)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; service:IProjectWorkflowConfigurationService.SaveAsync at line 21)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — REFERENCES_TYPE (inferred-token-match; IProjectWorkflowConfigurationService)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowExecutionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowInstanceService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowManagementService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/api/src/NEXUS.SharedKernel/Result.cs` — REFERENCES_TYPE (inferred-token-match; Result)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowContracts.cs` — REFERENCES_TYPE (inferred-token-match; IWorkflowEntityAdapterRegistry)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — REFERENCES_TYPE (inferred-token-match; IWorkflowInstanceService)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinanceWorkflowIntegration.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowDomainStateMismatch)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **4788** tokens (method varies per file)
- Requested planning budget: **8000** tokens
- Other-context allowance: **3200** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
