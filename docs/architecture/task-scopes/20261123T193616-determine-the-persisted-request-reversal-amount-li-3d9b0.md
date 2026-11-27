# UPI Task Context Manifest

Task: **Determine the persisted Request Reversal amount linked to its original Payment Request via ReversesRequestId; map only ProjectPaymentRequestDetail, ApprovalRequest and request reversal amount fields for the cash-flow committed balance.**
Primary module: **approvals** (task-explicit-alias)
Architecture snapshot: `20261123T184406-e63e1bc`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` | 1-109 | 1474 | filename-match:approval, filename-match:request, primary-module, symbol-match:ApprovalRequest |
| `apps/api/tests/NEXUS.Modules.Approvals.Tests/ApprovalRequestDomainTests.cs` | 1-107 | 1234 | filename-match:approval, filename-match:request, primary-module, symbol-match:ApprovalRequestDomainTests |
| `apps/api/tests/NEXUS.Api.Tests/Unit/AssignedApprovalScopeMiddlewareTests.cs` | 1-56 | 637 | filename-match:approval, filename-match:id, primary-module, symbol-match:AssignedApprovalScopeMiddlewareTests |
| `apps/api/src/NEXUS.Modules.Approvals/Api/ApprovalController.cs` | 1-105 | 1310 | filename-match:approval, primary-module, symbol-match:ApprovalController |
| `apps/api/src/NEXUS.Modules.Approvals/ModuleRegistration/ApprovalsModuleRegistration.cs` | 1-13 | 112 | filename-match:approval, primary-module, symbol-match:ApprovalsModuleRegistration |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Approvals/Application/ApprovalService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Approvals/Application/IApprovalService.cs` — CALLS_TYPED_METHOD (source-call-and-member-type; _approvalService:IApprovalService.CancelAsync at line 86)
- `apps/api/src/NEXUS.Modules.Approvals/Application/IApprovalService.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Approvals/Application/IApprovalService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/web-react/src/features/projects/components/PlanningImportSheet.tsx` — REFERENCES_TYPE (inferred-token-match; Message)
- `packages/shared-types/src/types/identity.ts` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalAction.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — REFERENCES_TYPE (inferred-token-match; BaseEntity)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAggregateRoot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IApprovalDelegationEligibilityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAuditable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ICostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetApprovalNotificationPublisher.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ApprovalRequest)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetWorkflow.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Approvals.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **4767** tokens (method varies per file)
- Requested planning budget: **8000** tokens
- Other-context allowance: **3200** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
