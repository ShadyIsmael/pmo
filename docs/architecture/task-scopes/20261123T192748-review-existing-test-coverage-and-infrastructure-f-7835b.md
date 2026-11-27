# UPI Task Context Manifest

Task: **Review existing test coverage and infrastructure for PlannedPaymentFoundationTests, CostUtilizationExportAuthorizationTests, CostUtilizationCalendarTests, financial reporting exports, ProgramPortfolioFinancialRollupServiceTests; identify focused gaps for Sprint 5 project cash flow and forecast calculations, authorization, currency, hierarchy deduplication, and localization.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Security/CostUtilizationExportAuthorizationTests.cs` | 1-115 | 1739 | filename-match:authorization, filename-match:cost, filename-match:export, filename-match:utilization, primary-module, symbol-match:CostUtilizationExportAuthorizationTests |
| `apps/web-react/src/features/finance/FinancialReportingExportButtons.tsx` | 1-52 | 621 | filename-match:export, filename-match:financial, filename-match:reporting, primary-module, symbol-match:FinancialReportingExportFormat |
| `apps/web-react/src/features/finance/financial-reporting-export.ts` | 1-444 | 6278 | filename-match:export, filename-match:financial, filename-match:reporting, primary-module, symbol-match:FinancialReportingExportFormat |
| `apps/web-react/src/test/cost-utilization-calendar.test.ts` | 1-72 | 912 | filename-match:calendar, filename-match:cost, filename-match:utilization, primary-module |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, filename-match:foundation, primary-module, symbol-match:ProjectFinancialFoundationDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Api/Authorization/ApiAuthorizationDefaults.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalsController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — REFERENCES_TYPE (inferred-token-match; FinanceApprovalModes)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestAssignedApprovalsController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestFundingOptions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestLineFunding.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestMyWork.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestSettlementAdjustments.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentReversalWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9566** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
