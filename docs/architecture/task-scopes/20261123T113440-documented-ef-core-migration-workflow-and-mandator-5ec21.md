# UPI Task Context Manifest

Task: **Documented EF Core migration workflow and mandatory Development Database Gate: determine NEXUS.Persistence DbContext, API startup project, migration assembly, appsettings.Development connection; verify SERVERPROPERTY ServerName, DB_NAME, database ID, environment; apply reviewed migration; update docs/finance/SPRINT-3-CONSOLIDATED-CLOSURE-REPORT.md.**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `scripts/database/financial-migrations-20261030161223-to-20261031000210.idempotent.sql` | 1-535 | 7386 | filename-match:id, filename-match:migration, module-mentioned, path-match:database, primary-module |
| `scripts/database/pending-migration-20261027202734.sql` | 1-45 | 394 | filename-match:migration, module-mentioned, path-match:database, primary-module |
| `scripts/database/pending-migration-20261030161223.sql` | 1-21 | 410 | filename-match:migration, module-mentioned, path-match:database, primary-module |
| `scripts/database/pending-migration-20261101114229.sql` | 1-101 | 1376 | filename-match:migration, module-mentioned, path-match:database, primary-module |
| `apps/api/src/NEXUS.Persistence/Migrations/AppDbContextModelSnapshot.cs` | 14-14 | 18 | filename-match:context, filename-match:db, module-mentioned, primary-module, symbol-match:AppDbContextModelSnapshot |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanRequest.cs` — REFERENCES_TYPE (inferred-token-match; AIPlanRequest)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanResponse.cs` — REFERENCES_TYPE (inferred-token-match; AIPlanResponse)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalAction)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — REFERENCES_TYPE (inferred-token-match; Delegation)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — REFERENCES_TYPE (inferred-token-match; AuditActivity)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — REFERENCES_TYPE (inferred-token-match; BudgetRevision)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — REFERENCES_TYPE (inferred-token-match; CostApproval)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — REFERENCES_TYPE (inferred-token-match; CostCategory)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — REFERENCES_TYPE (inferred-token-match; CostEntry)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — REFERENCES_TYPE (inferred-token-match; Invoice)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — REFERENCES_TYPE (inferred-token-match; PaymentNotificationDispatch)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — REFERENCES_TYPE (inferred-token-match; PaymentObligation)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` — REFERENCES_TYPE (inferred-token-match; PaymentRequestApproverAssignment)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — REFERENCES_TYPE (inferred-token-match; PaymentSettlement)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlementAdjustment.cs` — REFERENCES_TYPE (inferred-token-match; PaymentSettlementAdjustment)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPayment.cs` — REFERENCES_TYPE (inferred-token-match; PlannedPayment)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs` — REFERENCES_TYPE (inferred-token-match; PlannedPaymentInstallment)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs` — REFERENCES_TYPE (inferred-token-match; PlannedPaymentLifetimeReservation)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetApprovalConfiguration.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetApprovalApprover)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetClosing.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetClosing)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetLine.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetLine)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestApprovalCycle.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestApprovalCycle)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestDetail.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestDetail)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestFundingTransfer.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestFundingTransfer)
- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — REFERENCES_TYPE (inferred-token-match; Document)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentCategory.cs` — REFERENCES_TYPE (inferred-token-match; DocumentCategory)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9584** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
