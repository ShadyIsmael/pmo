# UPI Task Context Manifest

Task: **Verify isolated versus standard NEXUS.Api build output, compare Api ProjectManagement Persistence SharedKernel DLL timestamps and hashes, correct any confirmed stale standard runtime output, and provide the exact dotnet run startup command**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261127T011240-a5eb4bb`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/ConfiguredStandardWorkingCalendarInitializer.cs` | 1-191 | 2382 | filename-match:standard, module-mentioned, primary-module, symbol-match:ConfiguredStandardWorkingCalendarInitializer |
| `apps/api/src/NEXUS.Persistence/NEXUS.Persistence.csproj` | 1-47 | 944 | filename-match:nexus, filename-match:persistence, module-mentioned, primary-module |
| `apps/api/src/NEXUS.Persistence/Audit/ProjectAuditScopeProvider.cs` | 1-218 | 3442 | filename-match:provide, module-mentioned, primary-module, symbol-match:ProjectAuditScopeProvider |
| `apps/api/src/NEXUS.Persistence/Migrations/20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision.cs` | 1-68 | 657 | filename-match:correct, module-mentioned, primary-module, symbol-match:CorrectProjectAnnualBudgetClosingDecimalPrecision |
| `apps/api/src/NEXUS.Persistence/PhaseGatePersistenceRegistration.cs` | 1-18 | 235 | filename-match:persistence, module-mentioned, primary-module, symbol-match:PhaseGatePersistenceRegistration |
| `apps/api/src/NEXUS.Persistence/Migrations/AppDbContextModelSnapshot.cs` | 16-16 | 21 | module-mentioned, primary-module, symbol-match:BuildModel |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Audit/Application/AuditFieldRedactor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditSources.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` — IMPLEMENTS_INTERFACE (source-class-declaration; ProjectAuditScopeProvider declares IProjectAuditScopeProvider)
- `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` — REFERENCES_TYPE (inferred-token-match; IProjectAuditScopeProvider)
- `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — REFERENCES_TYPE (inferred-token-match; BudgetRevision)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — REFERENCES_TYPE (inferred-token-match; CostEntry)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — REFERENCES_TYPE (inferred-token-match; Invoice)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — REFERENCES_TYPE (inferred-token-match; PaymentObligation)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — REFERENCES_TYPE (inferred-token-match; PaymentSettlement)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlementAdjustment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPayment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetApprovalConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **7681** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
