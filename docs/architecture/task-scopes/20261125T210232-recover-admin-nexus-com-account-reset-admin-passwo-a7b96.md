# UPI Task Context Manifest

Task: **Recover admin@nexus.com account: reset Admin password in Development database Shady/NexusPmo using supported ASP.NET Identity password hashing, then verify login through /api/auth/login and check Admin roles and permissions**
Primary module: **permissions** (task-explicit-alias)
Architecture snapshot: `20261125T190942-cd9ddbb`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Authorization/ApiAuthorizationDefaults.cs` | 10-15 | 59 | filename-match:auth, module-mentioned, primary-module, symbol-match:ApiAuthorizationDefaults |
| `apps/api/tests/NEXUS.Api.Tests/Security/CashFlowAuthorizationTests.cs` | 1-128 | 1647 | filename-match:auth, module-mentioned, primary-module, symbol-match:CashFlowAuthorizationTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/PlatformSettingsAuthorizationTests.cs` | 1-42 | 474 | filename-match:auth, module-mentioned, primary-module, symbol-match:PlatformSettingsAuthorizationTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/TenantRuntimeSettingsAuthorizationTests.cs` | 1-93 | 1249 | filename-match:auth, module-mentioned, primary-module, symbol-match:TenantRuntimeSettingsAuthorizationTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/EffectivePermissionResolverTests.cs` | 38-48 | 151 | module-mentioned, primary-module, symbol-match:Protected_admin_baseline_survives_deny_while_deny_wins_for_ordinary_permissions |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalsController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — REFERENCES_TYPE (inferred-token-match; ProjectCashFlowController)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowReader.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/{projectId:guid}/cash-flow)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestAssignedApprovalsController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **3580** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
