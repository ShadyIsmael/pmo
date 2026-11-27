# UPI Task Context Manifest

Task: **Focused Sprint 5 QA coverage: planned payment installment schedules, approved unpaid payment request balances and partial settlements, refunds/corrections, cash flow date buckets, ProjectFinancialFoundation reporting, ProgramPortfolioFinancialRollup aggregation, CostUtilization export authorization, currency and forecast variance tests.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` | 1-2416 | 44896 | filename-match:flow, filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestWorkflow |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestApprovalNotificationPublisher.cs` — REFERENCES_TYPE (inferred-token-match; IProjectPaymentRequestApprovalNotificationPublisher)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestFundingOptions.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestLineFunding.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestMyWork.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestSettlementAdjustments.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentReversalWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectTeamApproverEligibility.cs` — REFERENCES_TYPE (inferred-token-match; ProjectTeamApproverEligibility)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalAction)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs` — REFERENCES_TYPE (inferred-token-match; ActualPaidSettlementCalculator)
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
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/FinancialReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` — REFERENCES_TYPE (inferred-token-match; CancelPaymentRequestDto)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **44896** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
