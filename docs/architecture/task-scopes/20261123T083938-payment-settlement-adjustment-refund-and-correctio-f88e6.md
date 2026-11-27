# UPI Task Context Manifest

Task: **PaymentSettlementAdjustment refund and correction approval authorization, assigned approver, maker checker, confirm refund execution, gross net Actual Paid, current Project Manager permission and tenant organization project isolation**
Primary module: **permissions** (task-explicit-alias)
Architecture snapshot: `20261123T000905-b97bbe5`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Security/PermissionAuthorizationContractTests.cs` | 1-778 | 13950 | filename-match:authorization, filename-match:permission, primary-module, symbol-match:PermissionAuthorizationContractTests |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Authorization/ApiAuthorizationDefaults.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Authorization/FinancialPermissionPolicyRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` — REFERENCES_TYPE (inferred-token-match; WorkflowLookupPolicyRegistration)
- `apps/api/src/NEXUS.Api/Authorization/WorkflowLookupPolicyRegistration.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Authorization)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalsController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — REFERENCES_TYPE (inferred-token-match; ProjectAnnualBudgetDraftApprovalConfigurationController)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestAssignedApprovalsController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/projects/workflow-lookups)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestFundingOptions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Finance)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **13950** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
