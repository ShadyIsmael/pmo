# UPI Task Context Manifest

Task: **ProgramPortfolioFinancialRollupService pending payment request funding availability**
Primary module: **cost-management** (task-explicit-alias)
Architecture snapshot: `20261122T232617-5fa03f0`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestFundingOptions.cs` | 1-105 | 2002 | filename-match:funding, filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestWorkflow |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestLineFunding.cs` | 1-88 | 1422 | filename-match:funding, filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestWorkflow |
| `apps/api/src/NEXUS.Modules.CostManagement/Domain/ProjectPaymentRequestFundingTransfer.cs` | 9-24 | 204 | filename-match:funding, filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestFundingTransfer |
| `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestAssignedApprovalsController.cs` | 1-43 | 522 | filename-match:payment, filename-match:request, primary-module, symbol-match:ProjectPaymentRequestAssignedApprovalsController |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PaymentRequestDtos.cs` | 6-6, 20-20, 33-33, 38-38, 47-47 | 79 | filename-match:payment, filename-match:request, primary-module, symbol-match:CreatePaymentRequestDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentRequestApproverAssignment.cs` | 10-23 | 194 | filename-match:payment, filename-match:request, primary-module, symbol-match:PaymentRequestApproverAssignment |
| `apps/web-react/src/test/quick-one-off-payment-request.test.tsx` | 55-78 | 218 | filename-match:payment, filename-match:request, primary-module, symbol-match:renderRequests |
| `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetPendingApprovalDto.cs` | 8-8 | 18 | filename-match:pending, primary-module, symbol-match:ProjectAnnualBudgetPendingApprovalDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` | 3-3, 9-9, 14-14, 21-21, 27-27 | 77 | filename-match:payment, primary-module, symbol-match:CreatePlannedPaymentInstallmentDto |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectFinancialFoundationDtos.cs` | 3-3 | 16 | filename-match:financial, primary-module, symbol-match:ProjectFinancialFoundationDto |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.Organization/Domain/Currency.cs` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/web-react/src/features/projects/ProjectPaymentRequests.tsx` — REFERENCES_TYPE (inferred-token-match; ApprovalMode)
- `packages/shared-types/src/types/cost.ts` — REFERENCES_TYPE (inferred-token-match; Budget)
- `packages/shared-types/src/types/organization.ts` — REFERENCES_TYPE (inferred-token-match; Currency)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetAssignedApprovalsController.cs` — CALLS_API_ROUTE (inferred-route-prefix; api/my-work/annual-budget-approvals)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestFundingTransfer)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestMyWork.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestSettlementAdjustments.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentReversalWorkflow.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPaymentRequestWorkflow)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — REFERENCES_TYPE (inferred-token-match; Error)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAggregateRoot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IApprovalDelegationEligibilityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAuditable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ICostUtilizationActivityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IMeetingPublicIdLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IOrganizationLifecycleHandler.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectAudienceLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectFinancialLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IProjectOrganizationLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Budget

- Initial selected source: **4752** tokens (method varies per file)
- Requested planning budget: **8000** tokens
- Other-context allowance: **3200** tokens

The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
