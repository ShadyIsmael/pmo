# UPI Task Context Manifest

Task: **Report financial rollup reporting module pending and approved payment request reservation annual budget availability**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261122T232605-c8ee5f0`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` | 8-8 | 18 | filename-match:annual, filename-match:budget, filename-match:pending, primary-module, symbol-match:ProjectAnnualBudgetPendingApprovalDto |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` | 1-148 | 2175 | filename-match:annual, filename-match:budget, primary-module, symbol-match:IProjectAnnualBudgetApprovalNotificationPublisher |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalDtos.cs` | 3-3, 17-17, 49-49 | 59 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetAssignedApprovalStepDto |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalsController.cs` | 1-59 | 710 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetAssignedApprovalsController |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` | 1-99 | 1510 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetClosingController |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` | 1-63 | 794 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetDraftApprovalConfigurationController |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` | 26-26, 2456-2456, 2602-2602, 2607-2607, 2615-2629 | 308 | filename-match:annual, filename-match:budget, primary-module, symbol-match:ProjectAnnualBudgetWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` | 6-6, 20-20, 33-33, 38-38, 47-47 | 79 | filename-match:payment, filename-match:request, primary-module, symbol-match:CreatePaymentRequestDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDtos.cs` | 3-3, 8-8, 13-13, 15-15, 17-17 | 104 | filename-match:annual, filename-match:budget, primary-module, symbol-match:CreateProjectAnnualBudgetDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetLineDtos.cs` | 3-3, 5-5, 7-7, 9-9 | 99 | filename-match:annual, filename-match:budget, primary-module, symbol-match:CreateProjectAnnualBudgetLineDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalAction)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/BudgetRevisionDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDraftApprovalConfigurationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` — REFERENCES_TYPE (inferred-token-match; PaymentRequestApproverAssignment)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlementAdjustment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPayment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetApprovalConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)

## Budget

- Initial selected source: **5856** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
