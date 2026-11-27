# UPI Task Context Manifest

Task: **Cost Utilization Calendar and existing Project Financial Workspace UI for daily costs**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/test/cost-utilization-calendar.test.ts` | 5-35 | 243 | filename-match:calendar, filename-match:cost, filename-match:utilization, primary-module, symbol-match:daily |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/CostService.cs` | 1-167 | 3644 | filename-match:cost, filename-match:costs, primary-module, symbol-match:CostService |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` | 8-25, 31-50, 56-60 | 571 | filename-match:cost, filename-match:costs, primary-module, symbol-match:CurrencyFinancialSummaryDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostService.cs` | 1-13 | 230 | filename-match:cost, filename-match:costs, primary-module, symbol-match:ICostService |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/ICostSummaryService.cs` | 11-14 | 38 | filename-match:cost, filename-match:costs, primary-module, symbol-match:ICostSummaryService |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Audit/Application/AuditFieldRedactor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditSources.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; _auditService:IAuditService.AppendAsync at line 163)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — REFERENCES_TYPE (inferred-token-match; IAuditService)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/BudgetRevisionDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — REFERENCES_TYPE (inferred-token-match; CostEntryDto)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/FinancialReportingDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDraftApprovalConfigurationDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectAnnualBudgetLineDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Application.DTOs)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancePermissionGuard.cs` — REFERENCES_TYPE (inferred-token-match; FinancePermissionGuard)
- `apps/api/src/NEXUS.Modules.CostManagement/Application/FinancialAuditEntryFactory.cs` — REFERENCES_TYPE (inferred-token-match; FinancialAuditEntryFactory)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — REFERENCES_TYPE (inferred-token-match; CostEntry)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **4742** tokens (method varies per file)
- Requested planning budget: **8000** tokens
- Other-context allowance: **3200** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
