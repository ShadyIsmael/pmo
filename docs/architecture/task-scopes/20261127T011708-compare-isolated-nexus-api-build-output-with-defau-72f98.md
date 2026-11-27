# UPI Task Context Manifest

Task: **Compare isolated NEXUS.Api build output with default dotnet run --no-build runtime output; verify DLL hashes and timestamps for Api, ProjectManagement, Persistence, SharedKernel, and correct stale output path**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261127T011240-a5eb4bb`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj` | 1-47 | 944 | filename-match:nexus, filename-match:persistence, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Persistence/Management/ManagementScopeResolver.cs` | 1-367 | 7184 | filename-match:management, module-mentioned, primary-module, symbol-match:ManagementScopeResolver |
| `apps/api/src/NEXUS.Persistence/Migrations/20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision.cs` | 1-68 | 657 | filename-match:correct, module-mentioned, primary-module, symbol-match:CorrectProjectAnnualBudgetClosingDecimalPrecision |
| `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` | 1-18 | 235 | filename-match:persistence, module-mentioned, primary-module, symbol-match:PhaseGatePersistenceRegistration |
| `apps/api/src/NEXUS.Persistence/Configurations/PaymentNotificationDispatchConfiguration.cs` | 1-23 | 321 | filename-match:no, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Persistence/Migrations/AppDbContextModelSnapshot.cs` | 16-16 | 21 | module-mentioned, primary-module, symbol-match:BuildModel |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — REFERENCES_TYPE (inferred-token-match; PaymentNotificationDispatch)
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
- `packages/shared-types/src/types/notification.ts` — REFERENCES_TYPE (inferred-token-match; NotificationType)
- `apps/api/src/NEXUS.Modules.Identity/Application/CurrentUserDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/EffectivePermissionResolver.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthenticationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IAuthorizationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IRoleAdministrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)
- `apps/api/src/NEXUS.Modules.Identity/Application/IUserAdministrationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Application)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9362** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
