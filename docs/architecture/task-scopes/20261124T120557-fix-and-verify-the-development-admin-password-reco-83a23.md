# UPI Task Context Manifest

Task: **Fix and verify the Development Admin password recovery console with a secure PowerShell Read-Host workflow, including quoted and special-character input, confirmation, password policy, canonical Admin checks, development-only protection, and safe recovery execution.**
Primary module: **workflow** (task-explicit-alias)
Architecture snapshot: `20261124T120021-db43106`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowExecutionContracts.cs` | 7-7, 17-17, 30-30, 59-59, 67-67 | 70 | filename-match:execution, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowTransitionRequest |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowExecutionService.cs` | 1-337 | 4296 | filename-match:execution, filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowExecutionService |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/ProjectWorkflowController.cs` | 1-32 | 491 | filename-match:workflow, module-mentioned, primary-module, symbol-match:ProjectWorkflowController |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Api/WorkflowEntityController.cs` | 1-206 | 2407 | filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowEntityController |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/ProjectWorkflowContracts.cs` | 1-44 | 414 | filename-match:workflow, module-mentioned, primary-module, symbol-match:ProjectWorkflowConfigurationDto |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowContracts.cs` | 7-7, 16-16, 21-21, 30-45, 47-51 | 380 | filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowExecutionContext |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowManagementContracts.cs` | 1-30 | 262 | filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowFactMetadata |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Contracts/WorkflowManagementDtos.cs` | 5-5, 19-19, 35-35, 50-50, 64-64 | 72 | filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowDefinitionListItemDto |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/WorkflowConfigurationRegistry.cs` | 1-40 | 552 | filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowConfigurationRegistry |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/ProjectWorkflowConfiguration.cs` | 1-32 | 406 | filename-match:workflow, module-mentioned, primary-module, symbol-match:ProjectWorkflowModes |
| `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDefinition.cs` | 1-17 | 230 | filename-match:workflow, module-mentioned, primary-module, symbol-match:WorkflowDefinition |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/workflows)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; service:IProjectWorkflowConfigurationService.SaveAsync at line 21)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — REFERENCES_TYPE (inferred-token-match; IProjectWorkflowConfigurationService)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/ProjectWorkflowConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowInstanceService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowManagementService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowTransitionQueryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Application.Services)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/api/src/NEXUS.SharedKernel/Result.cs` — REFERENCES_TYPE (inferred-token-match; Result)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — REFERENCES_TYPE (inferred-token-match; Role)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDefinitionVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowDomain.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — REFERENCES_TYPE (inferred-token-match; ProjectWorkflowConfiguration)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — REFERENCES_TYPE (inferred-token-match; IWorkflowInstanceService)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinanceWorkflowIntegration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.WorkflowEngine.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinanceWorkflowIntegration.cs` — IMPLEMENTS_INTERFACE (source-class-declaration; FinanceWorkflowConsumerValidator declares IWorkflowDefinitionConsumerValidator)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinanceWorkflowIntegration.cs` — REFERENCES_TYPE (inferred-token-match; IWorkflowDefinitionConsumerValidator)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9580** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
