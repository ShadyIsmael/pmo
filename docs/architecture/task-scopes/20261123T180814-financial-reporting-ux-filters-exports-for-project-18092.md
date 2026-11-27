# UPI Task Context Manifest

Task: **financial reporting UX filters exports for project program portfolio financial dashboards excel pdf export charts currency fiscal year localization**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T180116-88343bd`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` | 1-65 | 709 | filename-match:financial, filename-match:reporting, primary-module, symbol-match:ProjectFinancialReportingController |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/FinancialReportingDtos.cs` | 1-77 | 1054 | filename-match:financial, filename-match:reporting, primary-module, symbol-match:ProjectFinancialReportingDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialReportingCalculator.cs` | 1-318 | 4520 | filename-match:financial, filename-match:reporting, primary-module, symbol-match:FinancialReportingCalculator |
| `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` | 1-58 | 1076 | filename-match:financial, primary-module, symbol-match:FinancialPermissionPolicyRegistration |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialAuditEntryFactory.cs` | 1-177 | 2041 | filename-match:financial, primary-module, symbol-match:FinancialAuditEntryFactory |
| `apps/web-react/src/test/cost-utilization-export.test.ts` | 83-89 | 104 | filename-match:export, primary-module, symbol-match:ExcelJS |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` | 2600-2600 | 33 | primary-module, symbol-match:NormalizeCurrency |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` | 98-101 | 35 | primary-module, symbol-match:DeleteFinancialRecordDto |

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

- Initial selected source: **9588** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
