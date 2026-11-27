# UPI Task Context Manifest

Task: **PaymentObligation legacy and PlannedPayment current commitment balances reservation rollups Actual Paid reconciliation and migration cutover: detect inactive legacy paths and double counting**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261123T000905-b97bbe5`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/Configurations/PlannedPaymentLifetimeReservationConfiguration.cs` | 1-41 | 582 | filename-match:payment, filename-match:planned, filename-match:reservation, primary-module, symbol-match:PlannedPaymentLifetimeReservationConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/PaymentObligationConfiguration.cs` | 1-28 | 432 | filename-match:obligation, filename-match:payment, primary-module, symbol-match:PaymentObligationConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/PlannedPaymentConfiguration.cs` | 1-69 | 1049 | filename-match:payment, filename-match:planned, primary-module, symbol-match:PlannedPaymentConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/PlannedPaymentInstallmentConfiguration.cs` | 1-49 | 645 | filename-match:payment, filename-match:planned, primary-module, symbol-match:PlannedPaymentInstallmentConfiguration |
| `apps/api/src/NEXUS.Persistence/Migrations/20261122003656_Sprint2AddPlannedPaymentFoundation.cs` | 1-238 | 4268 | filename-match:payment, filename-match:planned, primary-module, symbol-match:Sprint2AddPlannedPaymentFoundation |
| `migration_script.sql` | 1-9 | 84 | filename-match:migration, primary-module |
| `apps/api/src/NEXUS.Persistence/MeetingReminderScheduler.cs` | 253-260 | 137 | primary-module, symbol-match:IsCurrentInvite |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — REFERENCES_TYPE (inferred-token-match; PaymentObligation)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlementAdjustment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPayment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetApprovalConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetClosing.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetLine.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestApprovalCycle.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestDetail.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestFundingTransfer.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Api/Development/DevelopmentAdminPasswordRecoveryService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Api/Documents/EfTemplateDeletionReferenceValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)
- `apps/api/src/NEXUS.Api/Documents/TemplateDeletionApproverResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Persistence)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **7197** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
