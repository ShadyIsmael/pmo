# UPI Task Context Manifest

Task: **Cash Flow Calendar in Project Financial Workspace: reuse Cost Utilization Calendar, monthly daily buckets, due dates, filters, date navigation, payment details, English Arabic RTL rendering and loading empty error states.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/test/cost-utilization-calendar.test.ts` | 5-35 | 243 | filename-match:calendar, filename-match:cost, filename-match:utilization, primary-module, symbol-match:daily |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/PaymentDueNotificationScheduler.cs` | 1-44 | 475 | filename-match:due, filename-match:payment, primary-module, symbol-match:PaymentDueNotificationScheduler |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/PaymentDueNotificationService.cs` | 1-116 | 1640 | filename-match:due, filename-match:payment, primary-module, symbol-match:PaymentDueNotificationService |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/PaymentRequestWorkflowPolicy.cs` | 1-185 | 2481 | filename-match:flow, filename-match:payment, primary-module, symbol-match:PaymentRequestWorkflowPolicy |
| `apps/api/tests/NEXUS.Api.Tests/Security/CostUtilizationExportAuthorizationTests.cs` | 1-115 | 1739 | filename-match:cost, filename-match:utilization, primary-module, symbol-match:CostUtilizationExportAuthorizationTests |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` | 26-26, 506-515, 2238-2238 | 241 | filename-match:flow, primary-module, symbol-match:ProjectAnnualBudgetWorkflow |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — REFERENCES_TYPE (inferred-token-match; IProjectAnnualBudgetApprovalNotificationPublisher)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalDtos.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetAssignedApprovalDetailDto)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetPendingApprovalDto)
- `apps/api/src/NEXUS.Api/Finance/ProjectTeamApproverEligibility.cs` — REFERENCES_TYPE (inferred-token-match; ProjectTeamApproverEligibility)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalAction)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditFieldRedactor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditSources.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; audit:IAuditService.AppendAsync at line 1289)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — REFERENCES_TYPE (inferred-token-match; IAuditService)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/AnnualBudgetRequestReservationCalculator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetAuthority.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetRevisionService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/CostSummaryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/BudgetRevisionDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **6819** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
