# UPI Task Context Manifest

Task: **Review existing ProgramPortfolioFinancialRollupService and ProgramPortfolioFinancialRollupDtos, project portfolio hierarchy queries, FinancialReportingDtos, CostUtilizationExportController, and export service for Sprint 5 Program/Portfolio Cash Flow API and Excel/PDF integration; identify safe new-file ownership and tests.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/FinancialReportingDtos.cs` | 1-77 | 1054 | filename-match:dtos, filename-match:financial, filename-match:reporting, primary-module, symbol-match:ProjectFinancialReportingDto |
| `apps/web-react/src/features/finance/FinancialReportingExportButtons.tsx` | 1-52 | 621 | filename-match:export, filename-match:financial, filename-match:reporting, primary-module, symbol-match:FinancialReportingExportFormat |
| `apps/web-react/src/features/finance/financial-reporting-export.ts` | 1-444 | 6278 | filename-match:export, filename-match:financial, filename-match:reporting, primary-module, symbol-match:FinancialReportingExportFormat |
| `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` | 1-65 | 709 | filename-match:controller, filename-match:financial, filename-match:reporting, primary-module, symbol-match:ProjectFinancialReportingController |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:dtos, filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |
| `apps/web-react/src/test/cost-utilization-export.test.ts` | 83-89 | 104 | filename-match:cost, filename-match:export, filename-match:utilization, primary-module, symbol-match:ExcelJS |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` | 8-25, 31-50, 56-60 | 571 | filename-match:cost, filename-match:dtos, primary-module, symbol-match:CurrencyFinancialSummaryDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/BudgetRevisionDtos.cs` | 48-52 | 47 | filename-match:dtos, primary-module, symbol-match:ReviewBudgetRevisionDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` | 98-98 | 17 | filename-match:dtos, primary-module, symbol-match:PlannedPaymentReplacementPreviewDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDtos.cs` | 19-19, 28-28, 46-56 | 136 | filename-match:dtos, primary-module, symbol-match:SubmitProjectAnnualBudgetReviewDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budget-closings)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-reporting)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-reporting)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-reporting)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-reporting)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-reporting)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-reporting)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/AnnualBudgetRequestReservationCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetAuthority.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostSummaryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDraftApprovalConfigurationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetLineDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancePermissionGuard.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinanceWorkflowIntegration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialAuditEntryFactory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialReportingCalculator.cs` — REFERENCES_TYPE (inferred-token-match; FinancialReportingCalculator)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialReportingCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/IBudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/IBudgetService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostSummaryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ICurrentProjectManagerLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9553** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
