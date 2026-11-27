# Sprint 2 Task 04 — Remaining Commitment Replacement

**Status:** Task 04 complete. Implementation source changes done. Build passed.  
**Date:** 2026-11-22  
**Scope:** Task 04 only. Task 05 not started.

## 1. Executive summary

Task 04 implements the audited workflow to replace the unpaid remaining commitment of a financially locked Planned Payment without modifying historical settlements, duplicating Lifetime Budget consumption, or corrupting existing Payment Requests.

Changes:

- `PlannedPaymentDto` gains `ReplacesPlannedPaymentId` (Guid?) and `ReplacedByPlannedPaymentId` (Guid?) so clients can trace replacement chains.
- `IPlannedPaymentService` gains `ReplaceRemainingCommitmentAsync(tenantId, projectId, plannedPaymentId, actorId, actorName, cancellationToken)`.
- `PlannedPaymentService.ReplaceRemainingCommitmentAsync` implements the full replacement workflow inside a single serializable transaction with project-level UPDLOCK/HOLDLOCK:
  - Authorizes with `PaymentObligation.Edit`; requires current Project Manager; enforces tenant/project scope and read-only guard.
  - Validates the target plan exists, is in `Locked` state, and has not already been replaced (idempotent replay returns the existing replacement plan).
  - Calculates `remainingCommitment = TotalAmount − totalSettled` where `totalSettled` is the sum of `ActualPaid` across all installments (settlement-derived).
  - Rejects if `remainingCommitment <= 0` (nothing to replace) or if any Pending/Approved Payment Request references the old plan's installments (they must be settled or cancelled first).
  - Validates Lifetime Budget: `remainingCommitment <= available` (old plan excluded via `excludePlanId` so its unpaid reservation is not double-counted).
  - Creates a replacement `PlannedPayment` in `Registered` state with `TotalAmount = remainingCommitment`, `ReplacesPlannedPaymentId = old plan Id`, and installments mirroring each old installment's outstanding balance (`Amount − ActualPaid`) with carried-over due date, milestone, and description.
  - Marks the old plan `Replaced`, sets `ReplacedByPlannedPaymentId = replacement Id`, and releases its unpaid reservation (`ReleasedAmount += Reserved − Released − Settled`).
  - Creates a new `PlannedPaymentLifetimeReservation` on the replacement plan with `ReservedAmount = remainingCommitment`, `ReleasedAmount = 0`.
  - Audits both plans ("Replaced" on the old, "Registered" on the replacement).
  - Net Lifetime Budget effect: old unpaid reservation is released and the replacement plan reserves the same amount; available budget is unchanged.
- `PlannedPaymentController` gains `POST /api/projects/{projectId}/planned-payments/{plannedPaymentId}/replace` authorized with `PaymentObligation.Edit`.
- Frontend `planned-payments.ts`: `PlannedPaymentDto` gains `replacesPlannedPaymentId` and `replacedByPlannedPaymentId`; adds `useReplacePlannedPayment` mutation.
- No migration required: `ReplacesPlannedPaymentId` and `ReplacedByPlannedPaymentId` columns already exist on `PlannedPayments`; `PlannedPaymentStates.Replaced` already exists in the state check constraint.

No existing permissions changed. No new permissions introduced. Existing role grants preserved.

## 2. Files changed

| File | Change |
|---|---|
| `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/PlannedPaymentDtos.cs` | `PlannedPaymentDto` adds `ReplacesPlannedPaymentId` (Guid?) and `ReplacedByPlannedPaymentId` (Guid?) |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/IPlannedPaymentService.cs` | Adds `ReplaceRemainingCommitmentAsync` method |
| `apps/api/src/NEXUS.Modules.CostManagement/Application/PlannedPaymentService.cs` | Implements `ReplaceRemainingCommitmentAsync`; `ToDtoAsync` populates new DTO fields |
| `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` | Adds `Replace` endpoint (`POST .../replace`) |
| `apps/web-react/src/features/projects/api/planned-payments.ts` | DTO gains `replacesPlannedPaymentId`, `replacedByPlannedPaymentId`; adds `useReplacePlannedPayment` mutation |
| `apps/api/tests/NEXUS.Api.Tests/Integration/PlannedPaymentFoundationTests.cs` | Adds 5 replacement tests (successful replacement, non-locked rejection, idempotent replay, fully-settled rejection, active-request rejection) |

## 3. Verification

| Phase | Status | Evidence |
|---|---|---|
| Plan | Done | Scope limited to Task 04; replacement workflow designed from existing domain model fields (`ReplacesPlannedPaymentId`, `ReplacedByPlannedPaymentId`, `PlannedPaymentStates.Replaced`). |
| Inspect | Done | PlannedPayment service, controller, DTOs, domain entities, PermissionCatalog, settlement workflow, and existing tests reviewed. |
| Implement | Done (source) | All DTO, interface, service, controller, and frontend changes implemented. |
| Migrate if required | Not required | Replacement chain columns and `Replaced` state already exist in the model and database; no schema change needed. |
| Build | Done | API build: 0 errors, 0 warnings. |
| Test | Not run | Test project has 22 pre-existing compilation errors unrelated to Task 04; replacement test code compiles correctly but the suite cannot be built or executed due to those pre-existing errors. |
| Report | Done | This report. |

## 4. Database state

No migration generated or applied. Development database (`Shady / NexusPmo`, database ID 5) currently has 36 migrations applied (Task 02 baseline). Task 03 migration `20261122110754_AddTask03PaymentSettlementInstallmentLinkage` is created but not yet applied (pending separate authorization).

## 5. Authorization

No new permissions introduced. Replacement requires `PaymentObligation.Edit` (existing). Current Project Manager enforcement applies. Tenant, Organization, and Project scope boundaries enforced via `ValidateAccess`. No automatic grants to existing roles; no new Finance Roles, System Roles, hardcoded protected permissions, or permission aliases.

## 6. Business rules preserved

- Historical settlements are never modified or deleted; they remain linked to the old plan's installments.
- Lifetime Budget is not double-consumed: the old plan's unpaid reservation is released and the replacement plan reserves an equal amount; net available budget is unchanged.
- Existing Payment Requests are not corrupted: active Pending/Approved requests block replacement until settled or cancelled.
- Only `Locked` plans can be replaced; `Registered`, `Replaced`, and `Cancelled` plans are rejected.
- Replacement is idempotent: calling replace on an already-replaced plan returns the existing replacement plan.
- Single serializable transaction with project-level lock prevents concurrent replacement races.
