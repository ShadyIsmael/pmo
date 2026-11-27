# Sprint 5 — Task 01: Cash Flow Foundation

**Status:** IMPLEMENTED — UNVERIFIED  
**Owner:** Finance Domain & Backend; QA support  
**Date:** 2026-11-23

## Outcome

Added a Project Cash Flow API and calculation path over the existing Planned Payment, Payment Request, settlement, and executed-adjustment records. The implementation reuses the existing Actual Paid settlement calculator and adds a batched multi-project read path for hierarchy reporting. No new financial tables, permission keys, or role grants were introduced.

Main implementation files:

- `apps/api/src/NEXUS.Modules.CostManagement/Application/DTOs/ProjectCashFlowDtos.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ProjectCashFlowCalculator.cs`
- `apps/api/src/NEXUS.Modules.CostManagement/Application/ActualPaidSettlementCalculator.cs`
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs`
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowReader.cs`

## Financial semantics

- **Planned residual:** scheduled installment amount less reliably linked settlements and approved unpaid request balances. Pending requests remain in Planned.
- **Committed unpaid:** approved request amount less linked settlements and validated approved reversals/releases. Approval alone never becomes Actual.
- **Actual Gross:** confirmed settlements on settlement dates.
- **Executed adjustments / Actual Net:** only executed refunds and corrections affect net actual; refunds retain the existing negative sign and corrections retain the existing sign semantics.
- **Timing variance:** actual settlement date minus expected date, expressed in days. A positive value is late; a negative value is early. It is not a monetary variance.
- **Data quality:** missing dates remain undated; uncertain or invalid references, currencies, and provenance surface quality codes and Partial/Unavailable totals. No currency conversion or synthetic combined exposure is produced.

The calculator validates adjustment provenance against an in-project source settlement, including currency and owner/link consistency. Invalid lines remain visible with quality flags while affected adjustment/net totals are unavailable. The reader also rejects cross-project reversal provenance and validates project currency codes against the existing catalog.

## Tests and verification

Focused source tests were added for calculation reconciliation, partial settlements, refunds/corrections, date bucketing, currency isolation, missing references, and authorization. The batched settlement reader has an in-memory SQLite parity test; it does not use the shared Development database.

Backend test evidence is **not verified**. The normal filtered test command exited 1 without diagnostic output. A serial `dotnet test` attempt began compiling project references but remained in `NEXUS.Modules.ProjectPlanning.MppAdapter` after 150 seconds, emitting extensive existing IKVM warnings about unsupported Java class formats and missing dependency classes; it was stopped before test execution. The API build also exited 1 with 0 warnings and 0 errors. No Cash Flow backend test is reported as passing.

The offline EF model check passed: model synchronized; 39 repository migrations; no Sprint 5 migration; no database connection opened. This does not establish target Development schema or runtime persistence.

## Remaining verification

Backend compilation and focused tests must complete in a working .NET/IKVM build environment. Product Owner Development runtime/persistence verification remains pending. No database operation or Agent UAT was performed.
