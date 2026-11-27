# UPI Task Context Manifest

Task: **Fix ProjectReportingService and ProjectStatusReportService outstanding planned payment values to include Planned Payment Installments and confirmed settlement balances**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261122T234254-6f2367f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/projects/api/planned-payments.ts` | 4-17, 19-40, 42-53, 55-63, 65-70 | 476 | filename-match:payment, filename-match:planned, module-mentioned, primary-module, symbol-match:PlannedPaymentInstallmentDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusReportingSemantics.cs` | 1-49 | 454 | filename-match:report, filename-match:reporting, filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportingCategories |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/PaymentObligationProjectLookupController.cs` | 12-25 | 214 | filename-match:payment, module-mentioned, primary-module, symbol-match:PaymentObligationProjectLookupController |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/PaymentObligationProjectLookupDto.cs` | 4-4 | 17 | filename-match:payment, module-mentioned, primary-module, symbol-match:PaymentObligationProjectLookupDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectReportingDtos.cs` | 3-11, 13-19, 21-37, 39-51, 53-57 | 674 | filename-match:report, filename-match:reporting, module-mentioned, primary-module, symbol-match:SaveProjectReportingConfigurationDto |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStatusReportDtos.cs` | 6-12, 14-19, 21-26, 28-42, 44-89 | 1173 | filename-match:report, filename-match:status, module-mentioned, primary-module, symbol-match:ProjectResourceUtilizationStatuses |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IPaymentObligationProjectLookupService.cs` | 5-8 | 54 | filename-match:payment, module-mentioned, primary-module, symbol-match:IPaymentObligationProjectLookupService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectReportingService.cs` | 1-13 | 314 | filename-match:report, filename-match:reporting, module-mentioned, primary-module, symbol-match:IProjectReportingService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs` | 1-17 | 484 | filename-match:report, filename-match:status, module-mentioned, primary-module, symbol-match:IProjectStatusReportService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/PaymentObligationProjectLookupService.cs` | 1-21 | 235 | filename-match:payment, module-mentioned, primary-module, symbol-match:PaymentObligationProjectLookupService |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportComparison.cs` | 1-132 | 2194 | filename-match:report, filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusReportComparison |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectReportingConfiguration.cs` | 1-38 | 493 | filename-match:report, filename-match:reporting, module-mentioned, primary-module, symbol-match:ProjectReportingConfiguration |
| `apps/web-react/src/features/projects/lib/project-reporting-configuration.ts` | 1-18 | 139 | filename-match:report, filename-match:reporting, module-mentioned, primary-module |
| `apps/web-react/src/test/project-status-reports-parity.test.tsx` | 114-126 | 118 | filename-match:report, filename-match:status, module-mentioned, primary-module, symbol-match:renderStatusReports |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectStatusTransitionRule.cs` | 6-13 | 86 | filename-match:status, module-mentioned, primary-module, symbol-match:ProjectStatusTransitionRule |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budget-closings)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/approval-configuration)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/lines)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-foundation)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — REFERENCES_TYPE (inferred-token-match; PaymentObligation)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/BaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/CostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectBaselineVersionDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectChangeRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectHandoverRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectOverviewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPeopleDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectSnapshotDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectStakeholderDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderInvitationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/StakeholderProjectViewDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimeEntryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/TimerDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IMilestoneService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectBaselineService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectChangeRequestService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Application)

## Budget

- Initial selected source: **7125** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
