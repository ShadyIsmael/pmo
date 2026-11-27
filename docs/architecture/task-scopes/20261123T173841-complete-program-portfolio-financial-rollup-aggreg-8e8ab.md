# UPI Task Context Manifest

Task: **Complete Program Portfolio financial rollup aggregation BuildContributions ReadFinancialRowsAsync AggregateCurrency BuildHierarchyGroup integrate FinancialReportingCalculator fiscal-year annual budget installments legacy obligation settlement executed refund correction lifetime budget currency grouping data-quality**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T173650-8468532`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialReportingCalculator.cs` | 1-318 | 4520 | filename-match:calculator, filename-match:financial, filename-match:reporting, primary-module, symbol-match:FinancialReportingCalculator |
| `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` | 1-65 | 709 | filename-match:financial, filename-match:reporting, primary-module, symbol-match:ProjectFinancialReportingController |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/AnnualBudgetRequestReservationCalculator.cs` | 1-19 | 226 | filename-match:annual, filename-match:budget, filename-match:calculator, primary-module, symbol-match:AnnualBudgetRequestReservation |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/FinancialReportingDtos.cs` | 1-77 | 1054 | filename-match:financial, filename-match:reporting, primary-module, symbol-match:ProjectFinancialReportingDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/ProjectFinancialBalanceCalculator.cs` | 1-22 | 322 | filename-match:calculator, filename-match:financial, primary-module, symbol-match:ProjectFinancialBalanceCalculator |
| `apps/api/tests/NEXUS.Api.Tests/CostManagement/P004FinancialDataIntegrityTests.cs` | 1-80 | 1343 | filename-match:data, filename-match:financial, primary-module, symbol-match:P004FinancialDataIntegrityTests |
| `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` | 1-58 | 1076 | filename-match:financial, primary-module, symbol-match:FinancialPermissionPolicyRegistration |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalDtos.cs` | 3-3, 17-17, 49-49 | 59 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetAssignedApprovalStepDto |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` | 8-8 | 18 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetPendingApprovalDto |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` | 26-26, 829-834, 908-912, 1117-1122, 1320-1320 | 271 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetWorkflow |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — REFERENCES_TYPE (inferred-token-match; PaymentSettlement)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetClosing.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetClosing)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ResourceManagement/Domain/Resource.cs` — REFERENCES_TYPE (inferred-token-match; Resource)
- `apps/web-react/src/features/projects/ProjectAnnualBudgetLines.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudget)
- `apps/web-react/src/features/projects/ProjectAnnualBudgetWorkspace.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudget)
- `apps/web-react/src/features/projects/ProjectAnnualBudgetYearEndClosing.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetClosing)
- `apps/web-react/src/features/projects/api/project-annual-budgets.ts` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudget)
- `apps/web-react/src/features/projects/api/project-financials.ts` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetClosing)
- `apps/web-react/src/features/projects/components/PlannedPaymentSection.tsx` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequest)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `packages/shared-types/src/types/resource.ts` — REFERENCES_TYPE (inferred-token-match; Resource)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.Modules.WorkflowEngine/Domain/WorkflowState.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowState)
- `apps/web-react/src/features/projects/ProjectPaymentRequests.tsx` — REFERENCES_TYPE (inferred-token-match; ApprovalMode)
- `apps/web-react/src/features/workflow/api/workflows.ts` — REFERENCES_TYPE (inferred-token-match; WorkflowState)
- `apps/web-react/src/features/workflow/api/workflowsFull.ts` — REFERENCES_TYPE (inferred-token-match; WorkflowState)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Currency)
- `packages/shared-types/src/types/workflow.ts` — REFERENCES_TYPE (inferred-token-match; WorkflowState)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `packages/shared-types/src/types/cost.ts` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — REFERENCES_TYPE (inferred-token-match; IProjectAnnualBudgetApprovalNotificationPublisher)
- `apps/api/src/NEXUS.Api/Finance/ProjectTeamApproverEligibility.cs` — REFERENCES_TYPE (inferred-token-match; ProjectTeamApproverEligibility)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalAction)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9598** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
