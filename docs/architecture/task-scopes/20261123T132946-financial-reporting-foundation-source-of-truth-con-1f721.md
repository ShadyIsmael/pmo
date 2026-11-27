# UPI Task Context Manifest

Task: **Financial Reporting Foundation & Source-of-Truth Consolidation: consolidate reporting definitions for Total Project Budget, Annual Approved Budget, Lifetime Reserved, Outstanding Commitments, Gross Actual Paid, Confirmed Refunds and Corrections, Net Actual Paid, Available Lifetime Budget, Annual Budget and Budget Line Utilization. Identify existing financial reporting services, calculators, APIs, and persistence mappings.**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261123T092540-af21596`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectAnnualBudgetLineConfiguration.cs` | 1-42 | 642 | filename-match:annual, filename-match:budget, filename-match:line, module-mentioned, primary-module, symbol-match:ProjectAnnualBudgetLineConfiguration |
| `apps/api/src/NEXUS.Persistence/Migrations/20261030122718_AddProjectAnnualBudgetFoundation.cs` | 1-78 | 1316 | filename-match:annual, filename-match:budget, filename-match:foundation, module-mentioned, primary-module, symbol-match:AddProjectAnnualBudgetFoundation |
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectAnnualBudgetApprovalConfigurationConfiguration.cs` | 1-71 | 980 | filename-match:annual, filename-match:budget, module-mentioned, primary-module, symbol-match:ProjectAnnualBudgetApprovalConfigurationConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectAnnualBudgetClosingConfiguration.cs` | 1-84 | 1632 | filename-match:annual, filename-match:budget, module-mentioned, primary-module, symbol-match:ProjectAnnualBudgetClosingConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectAnnualBudgetConfiguration.cs` | 1-68 | 1141 | filename-match:annual, filename-match:budget, module-mentioned, primary-module, symbol-match:ProjectAnnualBudgetConfiguration |
| `apps/api/src/NEXUS.Persistence/Migrations/20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation.cs` | 1-89 | 1105 | filename-match:annual, filename-match:budget, module-mentioned, primary-module, symbol-match:AddProjectPaymentRequestAnnualBudgetAssociation |
| `apps/api/src/NEXUS.Persistence/Migrations/20261101114229_AddProjectAnnualBudgetApprovalConfiguration.cs` | 1-118 | 1970 | filename-match:annual, filename-match:budget, module-mentioned, primary-module, symbol-match:AddProjectAnnualBudgetApprovalConfiguration |
| `apps/api/src/NEXUS.Persistence/Migrations/20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson.cs` | 1-24 | 197 | filename-match:annual, filename-match:budget, module-mentioned, primary-module, symbol-match:AddDraftAnnualBudgetApprovalConfigurationJson |
| `apps/api/src/NEXUS.Persistence/Configurations/ResourceReportingLineConfiguration.cs` | 1-21 | 297 | filename-match:line, filename-match:reporting, filename-match:source, module-mentioned, primary-module, symbol-match:ResourceReportingLineConfiguration |
| `apps/api/src/NEXUS.Persistence/Configurations/ResourceCalendarConfiguration.cs` | 1-24 | 242 | filename-match:source, module-mentioned, primary-module, symbol-match:ResourceCalendarConfiguration |
| `apps/api/src/NEXUS.Persistence/Migrations/20261029155617_InitialFopxPmBaseline.cs` | 9-9 | 18 | filename-match:line, module-mentioned, primary-module, symbol-match:InitialFopxPmBaseline |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Invoice.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentNotificationDispatch.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentObligation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlement.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlementAdjustment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPayment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentInstallment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/PlannedPaymentLifetimeReservation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetApprovalConfiguration.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetApprovalApprover)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetApprovalConfiguration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetClosing.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectAnnualBudgetLine.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestApprovalCycle.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestDetail.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestFundingTransfer.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/Role.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserOrganization.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserPermissionOverride.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserRole.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/UserSession.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Identity.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9540** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
