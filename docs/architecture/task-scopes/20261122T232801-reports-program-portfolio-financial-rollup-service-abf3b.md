# UPI Task Context Manifest

Task: **reports ProgramPortfolioFinancialRollupService pending reservation**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261122T232742-8e7ead7`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` | 1-58 | 1076 | filename-match:financial, primary-module, symbol-match:FinancialPermissionPolicyRegistration |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` | 8-8 | 18 | filename-match:pending, primary-module, symbol-match:ProjectAnnualBudgetPendingApprovalDto |
| `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` | 1-201 | 2999 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationController |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/ProjectFinancialBalanceCalculator.cs` | 1-22 | 322 | filename-match:financial, primary-module, symbol-match:ProjectFinancialBalanceCalculator |
| `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs` | 10-24 | 161 | filename-match:reservation, primary-module, symbol-match:PlannedPaymentLifetimeReservation |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/BudgetService.cs` | 163-170 | 140 | primary-module, symbol-match:CanAccessProjectOrPortfolio |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostManagementDtos.cs` | 98-101 | 35 | primary-module, symbol-match:DeleteFinancialRecordDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/CostSummaryDtos.cs` | 8-25, 31-50, 56-60 | 571 | primary-module, symbol-match:CurrencyFinancialSummaryDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` | 265-265 | 16 | primary-module, symbol-match:PendingPaymentRequestApprovalDto |
| `apps/web-react/src/features/finance/api/finance.ts` | 148-162, 164-180, 182-185, 251-258 | 407 | primary-module, symbol-match:CurrencyFinancialSummary |

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
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/web-react/src/features/projects/ProjectPaymentRequests.tsx` — REFERENCES_TYPE (inferred-token-match; ApprovalMode)
- `packages/shared-types/src/types/cost.ts` — REFERENCES_TYPE (inferred-token-match; Budget)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budget-closings)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-foundation)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-foundation)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-foundation)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-foundation)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectFinancialActivityController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/financial-foundation)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditFieldRedactor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditSources.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)

## Budget

- Initial selected source: **5761** tokens (method varies per file)
- Requested planning budget: **10000** tokens
- Other-context allowance: **4000** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
