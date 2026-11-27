# UPI Task Context Manifest

Task: **ProjectFinancialWorkspace planned payments and cash flow calendar React UI**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.CostManagement/Application/IPlannedPaymentService.cs` | 1-71 | 752 | filename-match:payments, filename-match:planned, primary-module, symbol-match:IPlannedPaymentService |
| `apps/web-react/src/test/planned-payments-project-id.test.tsx` | 40-51 | 110 | filename-match:payments, filename-match:planned, primary-module, symbol-match:renderPayments |
| `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` | 1-58 | 1076 | filename-match:financial, primary-module, symbol-match:FinancialPermissionPolicyRegistration |
| `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` | 1-124 | 1648 | filename-match:planned, primary-module, symbol-match:PlannedPaymentController |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` | 26-26, 506-515 | 202 | filename-match:flow, primary-module, symbol-match:ProjectAnnualBudgetWorkflow |
| `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` | 1-65 | 709 | filename-match:financial, primary-module, symbol-match:ProjectFinancialReportingController |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` | 27-27 | 18 | filename-match:flow, primary-module, symbol-match:ProjectPaymentRequestWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/FinancialReportingDtos.cs` | 1-77 | 1054 | filename-match:financial, primary-module, symbol-match:ProjectFinancialReportingDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` | 3-3, 9-9, 14-14, 21-21, 27-27 | 77 | filename-match:planned, primary-module, symbol-match:CreatePlannedPaymentInstallmentDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/ProjectFinancialBalanceCalculator.cs` | 1-22 | 322 | filename-match:financial, primary-module, symbol-match:ProjectFinancialBalanceCalculator |

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
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budget-closings)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/approval-configuration)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/AnnualBudgetRequestReservationCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetAuthority.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostSummaryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/BudgetRevisionDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **5984** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
