# UPI Task Context Manifest

Task: **Consolidate all valid implementation changes and commits from local branches, worktrees, stashes and reflogs into main, preserving the latest valid implementation; audit repository state and verify required builds.**
Primary module: **audit** (task-explicit-alias)
Architecture snapshot: `20261127T185307-ded35c2`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Audit/Infrastructure/AuditRepository.cs` | 1-22 | 189 | filename-match:audit, filename-match:repository, module-mentioned, primary-module, symbol-match:AuditRepository |
| `apps/web-react/src/test/audit-phase-gate-localization.test.tsx` | 8-16 | 112 | filename-match:audit, filename-match:local, module-mentioned, primary-module, symbol-match:AuditEntityTranslationProbe |
| `apps/api/src/NEXUS.Modules.Audit/Api/AuditController.cs` | 1-41 | 507 | filename-match:audit, module-mentioned, primary-module, symbol-match:AuditController |
| `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` | 1-137 | 1542 | filename-match:audit, module-mentioned, primary-module, symbol-match:ProjectAuditController |
| `apps/api/src/NEXUS.Modules.Audit/Application/AuditFieldRedactor.cs` | 1-115 | 1262 | filename-match:audit, module-mentioned, primary-module, symbol-match:AuditRedactionResult |
| `apps/api/src/NEXUS.Modules.Audit/Application/AuditService.cs` | 1-382 | 5104 | filename-match:audit, module-mentioned, primary-module, symbol-match:AuditService |
| `apps/api/src/NEXUS.Modules.Audit/Application/AuditSources.cs` | 7-20 | 145 | filename-match:audit, module-mentioned, primary-module, symbol-match:AuditSources |
| `apps/api/src/NEXUS.Modules.Audit/Application/IAuditService.cs` | 1-21 | 395 | filename-match:audit, module-mentioned, primary-module, symbol-match:IAuditService |
| `apps/api/src/NEXUS.Modules.Audit/Application/IProjectAuditScopeProvider.cs` | 8-11, 13-20 | 123 | filename-match:audit, module-mentioned, primary-module, symbol-match:IProjectAuditScopeProvider |
| `apps/api/src/NEXUS.Modules.Audit/Domain/AuditActivity.cs` | 1-12 | 106 | filename-match:audit, module-mentioned, primary-module, symbol-match:AuditActivity |
| `apps/api/src/NEXUS.Modules.Audit/ModuleRegistration/AuditModuleRegistration.cs` | 1-13 | 106 | filename-match:audit, module-mentioned, primary-module, symbol-match:AuditModuleRegistration |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Application)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/planned-payments)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budget-closings)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/annual-budgets/{budgetId:guid}/approval-configuration)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/audit)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/audit)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/audit)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/audit)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/audit)
- `apps/api/src/NEXUS.Modules.Audit/Application/AuditTrailContracts.cs` — REFERENCES_TYPE (inferred-token-match; AuditActorOption)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `packages/shared-types/src/types/project.ts` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — REFERENCES_TYPE (inferred-token-match; AuditEntry)
- `apps/api/src/NEXUS.Modules.Audit/Domain/AuditEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Tenant.cs` — REFERENCES_TYPE (inferred-token-match; Tenant)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Audit.Domain)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; _audit:IAuditService.AppendAsync at line 262)
- `apps/api/src/NEXUS.Api/Account/AccountService.cs` — REFERENCES_TYPE (inferred-token-match; IAuditService)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9591** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
