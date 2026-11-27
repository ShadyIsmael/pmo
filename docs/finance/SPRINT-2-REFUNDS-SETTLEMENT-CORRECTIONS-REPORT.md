# Sprint 2 Task 06 — Refunds & Settlement Corrections

**Status:** Implementation complete (backend + persistence). Frontend workspace extension and focused automated tests remain deferred items.  
**Date:** 2026-11-22

## 1. Domain & Persistence

- Added `PaymentSettlementAdjustment` entity (`apps/api/src/NEXUS.Modules.CostManagement/Domain/PaymentSettlementAdjustment.cs`) with `Refund`/`Correction` types, original settlement reference, related project/payment request/installment, amount/currency, reason, effective date, recorded timestamp, actor (via audit), idempotency key, and optional replacement settlement link.
- Original `PaymentSettlement` remains immutable; adjustments are separate linked records.
- Added EF configuration and `DbSet`.
- Migration `20261122121003_AddTask06SettlementAdjustments` created and applied to Development (`Shady / NexusPmo`, database ID 5). 0 pending after apply.

## 2. Workflows

- `ProjectPaymentRequestWorkflow.RecordRefundAsync` and `RecordSettlementCorrectionAsync` (plus shared `RecordSettlementAdjustmentAsync`) enforce:
  - `PaymentSettlement.Record` permission + active actor + current Project Manager.
  - Original settlement exists and belongs to authorized project.
  - Positive amount with 4 decimal places; matching currency; mandatory reason; effective date.
  - Cumulative refunds cannot exceed original settled amount.
  - Idempotent retries return original adjustment; conflicting reuse rejected.
  - Serializable transaction; atomic persistence.
  - No automatic Lifetime Budget release; no modification of original settlement; no duplicate payment consumption.

## 3. Authorization

- Reused existing `PaymentSettlement.Record` permission (no new permission/role/approval engine).
- Backend authorization independent of frontend; tenant/project isolation enforced.

## 4. Financial Calculations & Reporting

- `ActualPaidSettlementCalculator.SumNetAdjustmentsAsync` added to support Net Actual Paid (Gross settlements adjusted by refunds/corrections). Gross Actual Paid remains sum of confirmed original settlements.
- Downstream reporting wiring (UI labels, gross vs net) documented as follow-up.

## 5. Changed Files

- Domain: `PaymentSettlementAdjustment.cs`
- Persistence: `AppDbContext.cs`, `PaymentSettlementAdjustmentConfiguration.cs`, migration `20261122121003_AddTask06SettlementAdjustments`
- DTOs: `PaymentRequestDtos.cs`
- Workflow: `ProjectPaymentRequestSettlementAdjustments.cs`
- Controller: `ProjectPaymentRequestController.cs`
- Calculator: `ActualPaidSettlementCalculator.cs`
- Fix: `PlannedPaymentService.cs` (BusinessValidation argument)

## 6. Migration & Database Evidence

- Applied migration `20261122121003_AddTask06SettlementAdjustments`; model synchronized; schema verified by EF.

## 7. Build & Tests

- API build: 0 errors.
- Focused tests: Not run (deferred; pre-existing test compile blockers remain).
- Frontend: Not modified in this pass; workspace extension deferred.

## 8. Outstanding Blockers

- Frontend Financial Workspace extension (Refund/Correct actions, history, gross/net display, EN/AR/RTL) pending.
- Focused automated tests for refunds/corrections pending.
- Reporting integration of Net Actual Paid across overview/status/annual/rollups pending.
- Permission Matrix and Master UAT Checklist documentation update pending manual edit.

## 9. Recommended Next Task

- Task 07 (not started).