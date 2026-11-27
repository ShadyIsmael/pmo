# UPI Task Context Manifest

Task: **Sprint 5 financial cash flow management: discover current planned payments, installments, payment requests, settlements, adjustments, cash flow and cost utilization calendar, financial reports, program and portfolio rollups and exports; define contracts and task dependencies for foundation, calendar, forecast, hierarchy rollups, tests, authorization and localization.**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` | 1-201 | 2999 | filename-match:financial, filename-match:foundation, primary-module, symbol-match:ProjectFinancialFoundationController |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` | 27-27, 257-263, 509-512, 1399-1422, 1704-1720 | 875 | filename-match:flow, filename-match:payment, primary-module, symbol-match:ProjectPaymentRequestWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, filename-match:foundation, primary-module, symbol-match:ProjectFinancialFoundationDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/IPlannedPaymentService.cs` | 1-71 | 752 | filename-match:payment, filename-match:payments, filename-match:planned, primary-module, symbol-match:IPlannedPaymentService |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/PaymentRequestWorkflowPolicy.cs` | 1-185 | 2481 | filename-match:flow, filename-match:payment, primary-module, symbol-match:PaymentRequestWorkflowPolicy |
| `apps/web-react/src/test/cost-utilization-calendar.test.ts` | 1-72 | 912 | filename-match:calendar, filename-match:cost, filename-match:utilization, primary-module |
| `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` | 1-58 | 1076 | filename-match:financial, path-match:authorization, primary-module, symbol-match:FinancialPermissionPolicyRegistration |
| `apps/web-react/src/test/planned-payments-project-id.test.tsx` | 40-51 | 110 | filename-match:payment, filename-match:payments, filename-match:planned, primary-module, symbol-match:renderPayments |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` | 26-26, 506-515, 2238-2238 | 241 | filename-match:flow, primary-module, symbol-match:ProjectAnnualBudgetWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` | 3-3, 9-9, 14-14, 21-21, 27-27 | 77 | filename-match:payment, filename-match:planned, primary-module, symbol-match:CreatePlannedPaymentInstallmentDto |

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

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9539** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
