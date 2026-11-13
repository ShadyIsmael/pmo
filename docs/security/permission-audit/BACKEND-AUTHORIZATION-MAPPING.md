# Backend Authorization Mapping

**Generated:** 2026-11-13  
**Source:** `apps/api/src/NEXUS.Api` controllers, `apps/api/src/NEXUS.Api/Authorization` policy registrations, `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`

## Overview

This document maps every API controller endpoint to its declared authorization policy. The mapping is derived from static source analysis of `[Authorize]` attributes and policy registrations in `Program.cs`. **No runtime verification was performed** — this is a source-only audit.

---

## Policy Registration Catalog

### Canonical Financial Policies (`FinancialPermissionPolicyRegistration.cs`)

| Policy Name | Required Permission Claim | Notes |
|-------------|---------------------------|-------|
| `ProjectAnnualBudget.View` | `ProjectAnnualBudget.View` | |
| `ProjectAnnualBudget.Create` | `ProjectAnnualBudget.Create` | |
| `ProjectAnnualBudget.EditDraft` | `ProjectAnnualBudget.EditDraft` | |
| `ProjectAnnualBudget.ConfigureDraftApproval` | `ProjectAnnualBudget.ConfigureDraftApproval` | |
| `ProjectAnnualBudget.SubmitForReview` | `ProjectAnnualBudget.SubmitForReview` | |
| `ProjectAnnualBudget.Approve` | `ProjectAnnualBudget.Approve` | |
| `ProjectAnnualBudget.ReviseApproved` | `ProjectAnnualBudget.ReviseApproved` | |
| `ProjectAnnualBudget.ManageApprovalConfiguration` | `ProjectAnnualBudget.ManageApprovalConfiguration` | |
| `ProjectAnnualBudget.Delete` | `ProjectAnnualBudget.Delete` | |
| `ProjectAnnualBudget.Withdraw` | `ProjectAnnualBudget.Withdraw` | |
| `ProjectAnnualBudgetClosing.View` | `ProjectAnnualBudgetClosing.View` | |
| `ProjectAnnualBudget.Close` | `ProjectAnnualBudget.Close` | |
| `ProjectAnnualBudget.CloseApprove` | `ProjectAnnualBudget.CloseApprove` | |
| `ProjectAnnualBudget.CarryForward` | `ProjectAnnualBudget.CarryForward` | |
| `ProjectPaymentRequest.Approve` | `ProjectPaymentRequest.Approve` | |
| `PaymentSettlement.Record` | `PaymentSettlement.Record` | |
| `CostUtilization.Export` | `Resource.View` AND `CostUtilization.Export` | Dual requirement |
| `ProjectFinancialFoundation.View` | `ProjectFinancialFoundation.View` | |
| `ProgramFinancialRollup.View` | `ProgramFinancialRollup.View` | |
| `PortfolioFinancialRollup.View` | `PortfolioFinancialRollup.View` | |

### Workflow Lookup Policies (`WorkflowLookupPolicyRegistration.cs`)

| Policy Name | Type | View Permission | Operation Permissions |
|-------------|------|-----------------|----------------------|
| `WorkflowLookup.Project.Portfolios` | Lookup | `Portfolio.View` | `Project.Create`, `Project.Edit` |
| `WorkflowLookup.Project.Programs` | Lookup | `Program.View` | `Project.Create`, `Project.Edit` |
| `WorkflowLookup.Project.OrganizationalUnits` | Lookup | `Settings.View` | `Project.Create`, `Project.Edit` |
| `WorkflowLookup.Project.Currencies` | Lookup | `ReferenceData.View` | `Project.Create`, `Project.Edit` |
| `WorkflowLookup.Project.Users` | Lookup | `Project.View` | `Project.Create`, `Project.Edit` |
| `WorkflowLookup.Resource.OrganizationalUnits` | Lookup | `Settings.View` | `Resource.View`, `Resource.Create`, `Resource.Edit` |
| `WorkflowLookup.Resource.BaseCalendars` | Lookup | `Settings.View` | `Resource.View`, `Resource.Create`, `Resource.Edit` |
| `WorkflowLookup.Resource.ReferenceData` | Lookup | `Resource.View` | `Resource.Create`, `Resource.Edit` |
| `WorkflowLookup.Resource.Vendors` | Lookup | `Resource.View` | `Resource.Create`, `Resource.Edit` |
| `WorkflowLookup.Business.Currencies` | Lookup | `ReferenceData.View` | `Portfolio.Create`, `Portfolio.Edit`, `Program.Create`, `Program.Edit`, `Resource.View`, `Resource.Create`, `Resource.Edit`, `Resource.ViewCostRate`, `Resource.EditCostRate` |
| `WorkflowLookup.Meeting.Users` | Lookup | `Meeting.View` | `Meeting.Create`, `Meeting.Edit`, `Meeting.Manage` |
| `WorkflowLookup.WorkItem.Users` | Lookup | `WorkItem.View` | `WorkItem.Create`, `WorkItem.Edit` |
| `WorkflowLookup.Risk.Users` | Lookup | `Risk.View` | `Risk.Create`, `Risk.Edit` |
| `WorkflowLookup.Resource.Users` | Lookup | `Resource.View` | `Resource.Create`, `Resource.Edit` |
| `WorkflowLookup.Handover.ManagerCandidates` | Operation | — | `Handover.AdminInitiate` |
| `WorkflowLookup.ProgramCreate.Portfolios` | Operation | — | `Program.Create` |
| `WorkflowLookup.ProgramEdit.Portfolios` | Operation | — | `Program.Edit` |
| `WorkflowLookup.Portfolio.Managers` | Operation | — | `Portfolio.Create`, `Portfolio.Edit`, `Handover.AdminInitiate` |
| `WorkflowLookup.Program.Managers` | Operation | — | `Program.Create`, `Program.Edit`, `Handover.AdminInitiate` |
| `PaymentObligationProjectLookupController.PolicyName` | Operation | — | `PaymentObligation.View`, `PaymentObligation.Create`, `PaymentObligation.Edit` |

**Lookup Policy Logic:**
- `AddLookupPolicy`: Requires View permission OR any operation permission
- `AddOperationLookupPolicy`: Requires ANY operation permission (no View requirement)

---

## Controller-to-Policy Mapping

### Account Controller (`AccountController.cs`)
| Method | Route | Authorization |
|--------|-------|---------------|
| `Get` | `GET api/account` | `[Authorize]` (class-level) |
| `UpdateProfile` | `PUT api/account/profile` | `[Authorize]` (class-level) |
| `ChangePassword` | `POST api/account/change-password` | `[Authorize]` (class-level) |
| `GetSessions` | `GET api/account/sessions` | `[Authorize]` (class-level) |
| `RevokeSession` | `DELETE api/account/sessions/{sessionId}` | `[Authorize]` (class-level) |
| `RevokeOtherSessions` | `POST api/account/sessions/revoke-others` | `[Authorize]` (class-level) |

### Finance Controllers

#### ProjectAnnualBudgetController
| Method | Route | Authorization |
|--------|-------|---------------|
| `GetPage` | `GET api/projects/{projectId}/annual-budgets` | `[Authorize]` (class-level) |
| `GetAssignedReview` | `GET api/projects/{projectId}/annual-budgets/{itemId}/review/{requestId}` | `[Authorize]` (class-level) |
| `GetApprovalConfiguration` | `GET api/projects/{projectId}/annual-budgets/approval-configuration` | `[Authorize]` (class-level) |
| `SaveApprovalConfiguration` | `PUT api/projects/{projectId}/annual-budgets/approval-configuration` | `[Authorize]` (class-level) |
| `Create` | `POST api/projects/{projectId}/annual-budgets` | `[Authorize]` (class-level) |
| `UpdateDraft` | `PUT api/projects/{projectId}/annual-budgets/{itemId}` | `[Authorize]` (class-level) |
| `SubmitForReview` | `POST api/projects/{projectId}/annual-budgets/{itemId}/submit` | `[Authorize]` (class-level) |
| `ReviseApproved` | `POST api/projects/{projectId}/annual-budgets/{itemId}/revisions` | `[Authorize]` (class-level) |
| `DecideReview` | `POST api/projects/{projectId}/annual-budgets/{itemId}/review/{requestId}` | `[Authorize]` (class-level) |
| `Approve` | `POST api/projects/{projectId}/annual-budgets/{itemId}/approve` | `[Authorize]` (class-level) + `[Authorize(Policy="ProjectAnnualBudget.Approve")]` |

#### ProjectAnnualBudgetClosingController
| Method | Route | Authorization |
|--------|-------|---------------|
| `GetPage` | `GET api/projects/{projectId}/annual-budget-closings` | `[Authorize]` (class-level) |
| `GetApprovers` | `GET api/projects/{projectId}/annual-budget-closings/approvers` | `[Authorize]` (class-level) |
| `Prepare` | `POST api/projects/{projectId}/annual-budget-closings` | `[Authorize]` (class-level) |
| `Submit` | `POST api/projects/{projectId}/annual-budget-closings/{closingId}/submit` | `[Authorize]` (class-level) |
| `Decide` | `POST api/projects/{projectId}/annual-budget-closings/{closingId}/decisions` | `[Authorize]` (class-level) |
| `Cancel` | `POST api/projects/{projectId}/annual-budget-closings/{closingId}/cancel` | `[Authorize]` (class-level) |

#### ProjectPaymentRequestController
| Method | Route | Authorization |
|--------|-------|---------------|
| `GetPage` | `GET api/projects/{projectId}/payment-requests` | `[Authorize]` (class-level) |
| `GetEligibleApprovers` | `GET api/projects/{projectId}/payment-requests/approvers` | `[Authorize]` (class-level) |
| `GetEligibleReversalApprovers` | `GET api/projects/{projectId}/payment-requests/reversal-approvers` | `[Authorize]` (class-level) |
| `GetMilestones` | `GET api/projects/{projectId}/payment-requests/milestones` | `[Authorize]` (class-level) |
| `GetById` | `GET api/projects/{projectId}/payment-requests/{requestId}` | `[Authorize]` (class-level) |
| `GetSettlements` | `GET api/projects/{projectId}/payment-requests/{requestId}/settlements` | `[Authorize]` (class-level) |
| `GetReversals` | `GET api/projects/{projectId}/payment-requests/{requestId}/reversals` | `[Authorize]` (class-level) |
| `CreateReversal` | `POST api/projects/{projectId}/payment-requests/{requestId}/reversals` | `[Authorize]` (class-level) |
| `UpdateReversal` | `PUT api/projects/{projectId}/payment-requests/{requestId}/reversals/{reversalId}` | `[Authorize]` (class-level) |
| `SubmitReversal` | `POST api/projects/{projectId}/payment-requests/{requestId}/reversals/{reversalId}/submit` | `[Authorize]` (class-level) |
| `GetAssignedReversalReview` | `GET api/projects/{projectId}/payment-requests/{requestId}/reversals/{reversalId}/review` | `[Authorize]` (class-level) |
| `DecideReversal` | `POST api/projects/{projectId}/payment-requests/{requestId}/reversals/{reversalId}/decisions` | `[Authorize]` (class-level) |
| `CancelReversal` | `POST api/projects/{projectId}/payment-requests/{requestId}/reversals/{reversalId}/cancel` | `[Authorize]` (class-level) |
| `RecordSettlement` | `POST api/projects/{projectId}/payment-requests/{requestId}/settlements` | `[Authorize]` (class-level) + `[Authorize(Policy="PaymentSettlement.Record")]` |
| `Create` | `POST api/projects/{projectId}/payment-requests` | `[Authorize]` (class-level) |
| `Update` | `PUT api/projects/{projectId}/payment-requests/{requestId}` | `[Authorize]` (class-level) |
| `Submit` | `POST api/projects/{projectId}/payment-requests/{requestId}/submit` | `[Authorize]` (class-level) |
| `Decide` | `POST api/projects/{projectId}/payment-requests/{requestId}/decisions` | `[Authorize]` (class-level) + `[Authorize(Policy="ProjectPaymentRequest.Approve")]` |
| `Cancel` | `POST api/projects/{projectId}/payment-requests/{requestId}/cancel` | `[Authorize]` (class-level) |

#### ProjectFinancialFoundationController
| Method | Route | Authorization |
|--------|-------|---------------|
| `Get` | `GET api/projects/{projectId}/financial-foundation` | `[Authorize]` (class-level) |
| `UpdateApprovalConfiguration` | `PUT api/projects/{projectId}/financial-foundation/approval-configuration` | `[Authorize]` (class-level) |

### Governance Controller

#### AssignmentRecoveryController
| Method | Route | Authorization |
|--------|-------|---------------|
| `GetPage` | `GET api/governance/assignment-recovery/{entityType}` | `[Authorize]` (class-level) |
| `GetManagerCandidates` | `GET api/governance/assignment-recovery/{entityType}/{publicId}/manager-candidates` | `[Authorize]` (class-level) + `[Authorize(Policy="Governance.AssignmentRecovery.View")]` + service checks `Initiate` or `Manage` + tenant scope |
| `GetSponsorCandidates` | `GET api/governance/assignment-recovery/projects/{publicId}/sponsor-candidates` | `[Authorize]` (class-level) |
| `GetHistory` | `GET api/governance/assignment-recovery/{entityType}/{publicId}/history` | `[Authorize]` (class-level) |
| `CreateInitialAssignment` | `POST api/governance/assignment-recovery/{entityType}/{publicId}/initial-assignment` | `[Authorize]` (class-level) |
| `CreateAdministrativeHandover` | `POST api/governance/assignment-recovery/{entityType}/{publicId}/administrative-handover` | `[Authorize]` (class-level) |

---

## Authorization Architecture Notes

### 1. Dual-Layer Authorization
- **Class-level `[Authorize]`**: Ensures authenticated user with valid tenant context
- **Method-level Policy**: Specific permission claim required (e.g., `ProjectAnnualBudget.Approve`)

### 2. Permission Claim Format
All policies use `RequireClaim("permission", "<CanonicalKey>")` where CanonicalKey matches `PermissionCatalog` entries (e.g., `ProjectAnnualBudget.View`, `Roles.ConfigurePermissions`).

### 3. Scope Enforcement
- **Tenant scope**: Enforced by `UserEffectivePermissionService` — only permissions matching user's tenant are included
- **Project scope**: Enforced by controllers via route parameter `{projectId:guid}` and service-layer tenant/project boundary checks
- **Organization/Portfolio/Program**: Enforced by service-layer queries filtering by the appropriate scope

### 4. Lookup Policy Pattern
Workflow lookup endpoints use a "View OR Operation" pattern:
```csharp
HasPermission(viewPermission) || operationPermissions.Any(HasPermission)
```
This allows read access for users with either view permission or any related operation permission.

### 5. CostUtilization.Export Dual Requirement
Unique among financial policies — requires BOTH:
1. `Resource.View`
2. `CostUtilization.Export`

---

## Verification Status

| Check | Status | Evidence |
|-------|--------|----------|
| Policy registration in `Program.cs` | **Verified** | Source inspection of `FinancialPermissionPolicyRegistration.AddCanonicalFinancialPolicies()` and `WorkflowLookupPolicyRegistration.AddWorkflowLookupPolicies()` |
| Controller `[Authorize]` attributes | **Verified** | Source inspection of 605 endpoint entries in `API-ENDPOINT-INVENTORY.csv` |
| Policy-to-catalog alignment | **Verified** | All registered policy permission strings exist as active `IsActive=true` entries in `PermissionCatalog` |
| Runtime enforcement | **NOT VERIFIED** | No authenticated API runtime tests executed; SQL identity verification failed |

---

## Gaps and Follow-up

1. **Missing Policy Registrations**: Some controllers may have method-level policies not captured in the two registration classes above (e.g., `ProjectChangeRequestController`, `DocumentController`, `ManagerHandoverController`)
2. **Implicit Authorization**: Class-level `[Authorize]` without method-level policy relies on downstream service checks — not statically verifiable
3. **Cross-scope Validation**: Project-scoped permissions require service-layer enforcement not visible in controller attributes

**Next Steps for Full Coverage:**
- Complete controller scan for all `[Authorize(Policy=...)]` method attributes
- Map service-layer authorization checks (e.g., `ProjectAnnualBudgetService`, `AssignmentRecoveryService`)
- Execute runtime integration tests against Development database