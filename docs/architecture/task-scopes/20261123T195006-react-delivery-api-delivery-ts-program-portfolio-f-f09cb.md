# UPI Task Context Manifest

Task: **React delivery/api/delivery.ts ProgramPortfolioFinancialRollup API request helpers and types; create sibling cash-flow API client preserving current API and query patterns.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T194651-001f232`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Unit/PaymentRequestWorkflowPolicyTests.cs` | 1-185 | 2246 | filename-match:flow, filename-match:request, filename-match:ts, primary-module, symbol-match:PaymentRequestWorkflowPolicyTests |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` | 27-27, 257-263, 509-512, 1392-1392, 1399-1422 | 651 | filename-match:flow, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectCashFlowDtos.cs` | 6-10, 13-18, 21-27, 29-55, 57-63 | 746 | filename-match:cash, filename-match:flow, primary-module, symbol-match:CashFlowGranularity |
| `apps/api/tests/NEXUS.Api.Tests/CostManagement/FinancialDecimalPrecisionMappingTests.cs` | 1-98 | 1218 | filename-match:financial, filename-match:ts, primary-module, symbol-match:FinancialDecimalPrecisionMappingTests |
| `apps/web-react/src/test/financial-rollup-view.test.tsx` | 40-59, 61-73 | 1119 | filename-match:financial, filename-match:rollup, primary-module, symbol-match:makeRollup |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |

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

- Initial selected source: **5996** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
