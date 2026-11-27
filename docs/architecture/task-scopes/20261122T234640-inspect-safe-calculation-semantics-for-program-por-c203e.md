# UPI Task Context Manifest

Task: **Inspect safe calculation semantics for ProgramPortfolioFinancialRollup pending and approved Annual Budget reservations across transfers/reversals, and project outstanding/due amounts from PlannedPaymentInstallment and linked PaymentRequest settlements without double-counting legacy PaymentObligation or Quick One-off Plan+Request.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261122T234254-6f2367f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/test/quick-one-off-payment-request.test.tsx` | 55-78 | 218 | filename-match:off, filename-match:one, filename-match:payment, filename-match:quick, filename-match:request, primary-module, symbol-match:renderRequests |
| `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs` | 9-29 | 228 | filename-match:installment, filename-match:payment, filename-match:plan, filename-match:planned, primary-module, symbol-match:PlannedPaymentInstallment |
| `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` | 1-124 | 1648 | filename-match:payment, filename-match:plan, filename-match:planned, primary-module, symbol-match:PlannedPaymentController |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestApprovalNotificationPublisher.cs` | 1-107 | 1698 | filename-match:payment, filename-match:request, primary-module, symbol-match:IProjectPaymentRequestApprovalNotificationPublisher |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestAssignedApprovalsController.cs` | 1-43 | 522 | filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestAssignedApprovalsController |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestLineFunding.cs` | 1-88 | 1422 | filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` | 6-6, 20-20, 33-33, 38-38, 47-47 | 79 | filename-match:payment, filename-match:request, primary-module, symbol-match:CreatePaymentRequestDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` | 3-3, 9-9, 14-14, 21-21, 27-27 | 77 | filename-match:payment, filename-match:plan, filename-match:planned, primary-module, symbol-match:CreatePlannedPaymentInstallmentDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budget-closings)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/approval-configuration)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/lines)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetAuthority.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostSummaryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/BudgetRevisionDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDraftApprovalConfigurationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetLineDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancePermissionGuard.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinanceWorkflowIntegration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialAuditEntryFactory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/IBudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/IBudgetService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostSummaryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICurrentProjectManagerLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/IPaymentObligationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)

## Budget

- Initial selected source: **5892** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
