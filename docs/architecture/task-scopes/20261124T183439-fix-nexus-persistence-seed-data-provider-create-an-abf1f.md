# UPI Task Context Manifest

Task: **Fix NEXUS.Persistence SeedDataProvider.CreateAndVerifyDevelopmentBackupAsync SQL reader Int16 database ID conversion and add focused regression test**
Primary module: **persistence** (task-explicit-alias)
Architecture snapshot: `20261124T143831-c08b56f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs` | 35-35, 153-153, 358-361, 623-630, 967-972 | 343 | filename-match:data, filename-match:id, filename-match:provider, filename-match:seed, module-mentioned, primary-module, symbol-match:SeedDataProvider |
| `apps/api/tests/NEXUS.Api.Tests/Integration/DevelopmentWbsSqlServerPersistenceTests.cs` | 1-278 | 3791 | filename-match:development, filename-match:persistence, filename-match:sql, module-mentioned, primary-module, symbol-match:DevelopmentWbsSqlServerPersistenceTests |
| `scripts/database/20261105214717_AddMeetingNotificationPublicIdentifiers.idempotent.sql` | 1-53 | 464 | filename-match:id, module-mentioned, path-match:database, primary-module |
| `apps/api/src/NEXUS.Persistence/AppDbContext.cs` | 371-375 | 77 | module-mentioned, primary-module, symbol-match:SaveChangesAsync |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanRequest.cs` — PERSISTS_ENTITY (inferred-dbset-name; AIPlanRequest)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanRequest.cs` — REFERENCES_TYPE (inferred-token-match; AIPlanRequest)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.AIPlanning.Domain)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanResponse.cs` — PERSISTS_ENTITY (inferred-dbset-name; AIPlanResponse)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanResponse.cs` — REFERENCES_TYPE (inferred-token-match; AIPlanResponse)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIPlanResponse.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.AIPlanning.Domain)
- `apps/api/src/NEXUS.Modules.AIPlanning/Domain/AIProviderType.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.AIPlanning.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — PERSISTS_ENTITY (inferred-dbset-name; ApprovalAction)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalAction)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — PERSISTS_ENTITY (inferred-dbset-name; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — PERSISTS_ENTITY (inferred-dbset-name; Delegation)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — REFERENCES_TYPE (inferred-token-match; Delegation)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/Delegation.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — PERSISTS_ENTITY (inferred-dbset-name; AuditActivity)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — REFERENCES_TYPE (inferred-token-match; AuditActivity)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — PERSISTS_ENTITY (inferred-dbset-name; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — PERSISTS_ENTITY (inferred-dbset-name; Budget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — PERSISTS_ENTITY (inferred-dbset-name; BudgetRevision)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — REFERENCES_TYPE (inferred-token-match; BudgetRevision)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/BudgetRevision.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.CostManagement.Domain)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — PERSISTS_ENTITY (inferred-dbset-name; CostApproval)
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/CostApproval.cs` — REFERENCES_TYPE (inferred-token-match; CostApproval)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **4675** tokens (method varies per file)
- Requested planning budget: **8000** tokens
- Other-context allowance: **3200** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
