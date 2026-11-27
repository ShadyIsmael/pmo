# Sprint 1 / Task 01 — Financial Integrity & Database Verification

**Repository:** C:\Projects\pmo  
**Date:** 2026-11-21  
**Status:** Development identity and schema are verified; the source precision correction and focused regression tests pass. One corrective migration remains pending and unapplied, so current model/schema synchronization is not complete.  
**Scope:** Financial integrity review and EF precision correction only. No payment workflow redesign, new Planned Payment aggregate, database write, migration application, reset, reseed, or UAT was performed.

## Documentation reviewed and reused

- `TARGET_FINANCIAL_OPERATING_MODEL_v1.0.md` and `FINANCIAL_TECHNICAL_BLUEPRINT_v1.0.md` for approved lifecycle/calculation rules and the source-vs-runtime evidence boundary.
- `FINANCIAL_MIGRATION_CUTOVER_PLAN.md`, `FINANCIAL_IMPLEMENTATION_BACKLOG.md`, and `FINANCIAL_DESIGN_GAPS_AND_RISKS.md` for the decimal risk, migration safeguards, Sprint dependencies, and approved security boundaries.
- `FINANCIAL-CONSOLIDATION-AUDIT.md` and the earlier sections of this report for the existing reservation, settlement, reporting, and precision findings; those source audits were reused rather than repeated.
- `docs/project/README.md`, `docs/development/local-setup.md`, and `docs/development/ef-core-database-workflow.md` for the actual persistence workflow and read-only gate procedure.
- `docs/development/database-sprint03-readiness-20261029.md` for historical database blockers only; its old identity/history evidence was not treated as current.

## Execution status

| Work item | Status |
|---|---|
| Documentation review | Done |
| Development identity, migration history, and physical schema inspection | Done; target verified; one precision migration remains pending |
| Precision mapping correction and focused regression verification | Done; 3/3 focused tests passed |
| Reservation and settlement review | Existing source audit reused; live row-level reconciliation not run |
| Documentation synchronization | Done |
| Migration application, SQL Server persistence tests, and Product Owner UAT | Not run within this task scope; database synchronization remains incomplete |

## 1. Development database identity and gate result

The active API Development configuration points to localhost:1433 / NexusPmo. The read-only EF Database Gate connected through that configuration and queried SQL Server identity directly. It verified the repository's expected Development target, Shady / NexusPmo. A separate read-only SQL metadata query returned the same identity.

| Identity field | Result |
|---|---|
| Effective Development endpoint | localhost:1433 / NexusPmo (configuration only) |
| Expected target from repository instructions | Shady / NexusPmo |
| SERVERPROPERTY('ServerName') | Shady |
| Actual DB_NAME() | NexusPmo |
| Database ID | 5 |
| SQL Server version / compatibility | Not collected |
| Target identity verified | Yes — Development |

An earlier draft stated database ID 5 and claimed all migrations were applied. The live gate now confirms database ID 5 and disproves the fully-applied claim: one migration is pending.

## 2. EF migration and schema comparison

The read-only Development Database Gate and target-specific SQL generation verified:

- Context: NEXUS.Persistence.AppDbContext.
- Migration assembly: NEXUS.Persistence.
- Repository migration sources: 34.
- Applied in dbo.__EFMigrationsHistory: 33; latest is 20261121020524_AddProjectOriginalBudget.
- Pending: 1 — 20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision.
- EF model synchronized with the source snapshot: Yes.
- Target Development database: verified as Shady / NexusPmo, database ID 5.

Physical schema matched the last-applied model with zero missing mapped objects. Against the current repository model, the gate found exactly two column-definition differences, both the closing allocation decimals described below. No other current-model table, column, index, or foreign-key differences were reported. No live financial row counts or balances were queried.

The earlier migration and its designer record both closing allocation fields as decimal(7,4). The current corrected model records them as decimal(19,4). The live gate confirmed the corrective migration is the sole pending migration and that the physical columns still use the earlier precision.

## 3. Decimal precision finding and correction

**Confirmed in source and in the Development database.**

AppDbContext applies a decimal precision convention after entity configurations. The broad Allocation name rule assigned precision 7,4 after the closing entity configuration had explicitly assigned 19,4. The earlier migration snapshot also records the two closing fields as 7,4.

The current correction now gives these exact monetary fields precision 19,4:

- ProjectAnnualBudgetClosings.ApprovedPaymentRequestAllocations
- ProjectAnnualBudgetClosings.EffectiveActiveAllocations

Other allocation percentage fields retain precision 7,4:

- PlanAssignments.AllocationPct
- ResourceRequests.RequiredAllocation

This keeps the fix scoped to the monetary columns and avoids widening unrelated percentage columns.

The gate and a direct read-only SQL Server metadata query confirmed both Development columns are currently `decimal(7,4) NOT NULL`. Neither column has a default constraint. The target-specific SQL generated from the live migration history widens both columns to `decimal(19,4)` at the same scale and adds the migration-history row. Its SHA-256 is `5AC262122EEAE08331200113479E8448149AF608CBAA62738D50F19028A646BE`. It contains no data updates, deletes, table drops, index changes, or foreign-key changes; the conditional default-constraint drops have no matching constraints on the verified target. Widening the decimal precision preserves values in place. The Down migration narrows back to decimal(7,4) and must not be used unless stored values are proven to fit that range.

The migration has not been applied. No financial data values were read or changed.

## 4. Reservation and settlement calculation audit

Source review found the current calculation behavior:

- Annual Budget Available is max(0, TotalAnnualBudget minus active allocations). TotalAnnualBudget is composed from approved new funding, approved carry-forward, and approved adjustments.
- Active payment-request allocations include requests in Pending or Approved status. Draft and terminal requests do not reserve. Funding transfers into the fiscal year are added; transfers out and approved reversal releases are subtracted.
- Actual Paid is derived from confirmed PaymentSettlement records in the fiscal-year date range.
- Settlements do not reduce the current annual-budget allocation. A settled request continues to count at its full allocated amount until an approved reversal releases the unpaid balance.
- An approved reversal can release only the amount remaining after settlements and prior approved releases.
- Outstanding unpaid commitment is the original amount less settlements and approved releases.
- Cross-year funding transfers move the remaining unpaid commitment between adjacent fiscal years. The source requires an approved closing, matching budget/project/currency scope, and a valid transfer chain.
- PaymentObligation and ProjectPaymentRequest remain separate payable rails. ProjectReportingService and ProjectStatusReportService use PaymentObligation-linked settlements for their actual-paid totals, so request-linked settlements are omitted there. Portfolio/program rollups have a separate request-settlement calculation.

Product Owner rule for the consolidation: a Registered Planned Payment reserves its amount immediately; approval authorizes payment; only confirmed settlement counts as Actual Paid. This task did not change any lifecycle. Follow-up design must identify the explicit registration event, include registered amounts in the reservation and budget-ceiling checks, and avoid treating an unregistered draft as registered. Approval must not be counted as paid. Settlement must be the source of Actual Paid, and reservation-to-paid transitions must not release budget twice or double-count exposure.

These calculation findings are based on the documented source behavior and the prior focused audit. This task did not query live annual-budget allocations, settlement totals, reversals, transfers, closing snapshots, or row-level balances; no record-level financial reconciliation is claimed.

## 5. Security and historical integrity

No authorization policy, permission catalog, role grant, Tenant/Organization boundary, payment state, approval history, settlement, reversal, audit record, closing snapshot, idempotency key, or concurrency mechanism was changed by this task.

The corrective migration alters decimal metadata only and contains no data update. It remains unapplied, so no database records or schema objects were changed. No permission was automatically granted and no legacy protected-permission list was restored.

Two documented authorization dependencies remain for the dedicated Security Hardening work: enforce maker-checker so a Payment Request submitter cannot approve their own request, and require the current assigned Project Manager in addition to scoped permission before recording a settlement. Without those controls, separation of duties and actor eligibility can fail. This task did not redesign RBAC, grant permissions, or alter approval/settlement history.

## 6. Build and test evidence

| Check | Outcome |
|---|---|
| EF Database Gate inspect/build | Passed; model synchronized; 34 repository migrations, 33 applied, 1 pending; 23 NU1900 vulnerability-feed warnings |
| Target-specific migration SQL review | Passed; generated from verified Development history; two precision widenings and history insert only; SHA-256 recorded in §3; not applied |
| Read-only SQL metadata query | Passed; confirmed Shady/NexusPmo, database ID 5, both `decimal(7,4) NOT NULL`, no default constraints |
| API and persistence build | API and persistence project builds passed before the full test-project compilation failure; no source errors were reported in either project |
| Focused xUnit precision tests | Passed 3/3 in a temporary isolated harness using the SQL Server EF provider model; no database connection opened |
| Full NEXUS.Api.Tests build/test | Blocked before test execution by unrelated current-tree test compilation errors, including missing `PaymentRequestStatuses`, `ITenantFiscalYearService`, `FiscalYearCalendarService`, and `AddCanonicalFinancialActionPolicies`, plus ambiguous `IAuthorizationService` references and a stale `ProjectFinancialSummaryDto.CommittedCost` assertion |
| SQL Server persistence tests | Not run; the required migration is pending and the physical schema differs from the current model |
| Agent-executed UAT | Not run |

Files changed:

- `FinancialDecimalPrecisionMappingTests.cs` — use the SQL Server provider to inspect model metadata without creating SQLite tables; the old fixture failed on SQL Server-specific `nvarchar(max)` schema.
- This report, `FINANCIAL_MIGRATION_CUTOVER_PLAN.md`, `FINANCIAL_TECHNICAL_BLUEPRINT_v1.0.md`, `TARGET_FINANCIAL_OPERATING_MODEL_v1.0.md`, and `FINANCIAL_DESIGN_GAPS_AND_RISKS.md` — update their Development identity, migration, and precision evidence.

## 7. Required database completion fields

| Required field | Evidence |
|---|---|
| EF model synchronized | Yes |
| Pending migrations | 1 — `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision` |
| Target environment and database identity verified | Yes — Development, `Shady / NexusPmo`, database ID 5 |
| Backup status | Not requested; this was read-only Development inspection |
| Migrations applied | None |
| Minimal Seed and idempotency | Not run |
| Physical schema verified | No — verified against the last-applied model, but two current-model decimal definitions remain mismatched |
| Persistence tests | Not run against SQL Server; focused model mapping tests passed 3/3 |
| Blocker | The required precision migration is pending and prohibited from application by this task; post-apply schema verification remains outstanding |

## 8. Recommended follow-up for Sprint 1 Tasks 02–05

1. **Task 02 — Financial API and registration semantics:** implement or remove the missing GET /api/costs/summary call, and define the explicit Registered Planned Payment event. Enforce reservation at registration, budget ceilings, idempotency, and concurrency without treating approval as payment.
2. **Task 03 — Costs UI data integrity:** remove hardcoded USD and fabricated approved/forecast amounts; use authoritative API values and approved currency data.
3. **Task 04 — Finance authorization:** enforce matching Budget and Cost permissions in UI and API, preserve Active Deny and resource scope, and add positive, negative, and cross-scope cases to the Master UAT Checklist. Do not automatically grant permissions.
4. **Task 05 — Database and reporting readiness:** the read-only gate now verified the Development identity, migration history, closing schema, and defaults (see §§1–3). The precision migration remains unapplied by this task's restriction. After separate authorization, apply only the reviewed script and verify the resulting schema; independently profile both payable rails and reconcile request-linked settlements into project/status reporting before planning cutover.

## 9. Handover

The EF mapping correction, scoped migration source, and regression assertions are prepared; the focused mapping tests pass. The read-only gate verified Development `Shady/NexusPmo` (database ID 5), 33 applied migrations, and the single pending precision correction. The target-specific SQL was reviewed and hashed, and the two physical columns are confirmed as non-null `decimal(7,4)` with no defaults. No migration or data change was made. Database delivery remains incomplete until the pending correction is separately authorized, applied, and verified against the physical schema. Financial row-level balances, full test-project compilation, SQL Server persistence tests, and Product Owner UAT remain unverified.

