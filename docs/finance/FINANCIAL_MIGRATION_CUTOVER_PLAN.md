# FOPX PM — Financial Migration and Cutover Plan

**Date:** 2026-11-21  
**Status:** Proposed non-destructive plan. No database operation or migration is authorized or performed by this document.  
**Cutover boundary:** One Organization at a time, exactly once, after successful reconciliation and readiness approval.

## 1. Purpose and constraints

Move the current dual payment rails and legacy allocation records toward the approved Planned Payment + Installment + Payment Request model while preserving every source ID, approval, settlement, audit event, year-end snapshot, and published financial report.

The active worktree is extensively modified. The Sprint 1 Task 01 read-only EF gate now verified Development `Shady/NexusPmo` (database ID 5), with 33 of 34 repository migrations applied and `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision` pending. The two closing allocation columns are physically `decimal(7,4) NOT NULL` and have no default constraints; the current model expects `decimal(19,4)`. No row counts, balances, or record-level data quality were queried. No database write or migration application was performed.

Repository requirements apply to any later persistence work: identify the actual context, migration assembly, startup project, environment and database target; review generated SQL; verify the target identity before writes; never modify Production; use new migrations rather than rewrite applied migrations; preserve data and concurrent work. Development/UAT backup is optional and is not an authorization or identity substitute.

## 2. Current source model and migration risks

| Source | Current shape | Migration concern |
|---|---|---|
| PaymentObligation | Single amount/due date/optional milestone; ProjectId and MilestoneId are scalar identifiers with no configured milestone FK | Verify Tenant/Project and milestone ownership before mapping. One source obligation maps to one Planned Payment and one Installment only when deterministic. |
| PaymentSettlement | Shared table with nullable PaymentObligationId or ProjectPaymentRequestId, XOR constraint, nullable historical currency for some legacy rows, optional FundingTransferId | Preserve each row once. Never set both owner FKs, clone it to a second ledger, or infer missing historical settlement currency from today's Project/obligation settings. |
| ProjectPaymentRequestDetail and ApprovalRequest | Separate request rail with approvals, approval actions, frozen assignments, annual-budget association, settlements and reversals | Reconcile requests with plans using stable references and evidence. Current workflow blocks submission/approval when legacy approved requests lack required annual funding association. |
| Budget and BudgetRevision | Legacy project/portfolio allocation rows and separate revision approval history; no AnnualBudget FK | Do not relabel as Annual Budget Lines automatically. Reconcile by verified Project, fiscal year, currency and category; preserve original records and approval history. |
| ProjectAnnualBudget and Closing/Transfer | Project/year funding, approvals, closing snapshots, and append-only transfers | Preserve source/destination budget provenance, transfer chains, approval snapshots and closed-year state. Do not duplicate carried funding or lifetime reservations. |
| CostEntry, Invoice, CostApproval, CostCategory | Separate legacy incurred-cost and dormant persistence paths | Preserve source values, statuses, free-text categories and audit. No automatic conversion to settlement or deletion. |
| Published project/status/parent snapshots | Persisted historical figures with varying source definitions | Never recalculate or overwrite prior published values. Use a new future formula/version and document the cutover boundary. |

### Decimal precision issue

Source review found two ProjectAnnualBudgetClosing monetary fields previously mapped as `decimal(7,4)`: ApprovedPaymentRequestAllocations and EffectiveActiveAllocations. Current source maps them as `decimal(19,4)`, and the corrective migration widens those columns. The identity-verified gate confirmed the correction is the sole pending migration; direct SQL metadata confirmed both physical columns remain `decimal(7,4) NOT NULL` without default constraints. Target-specific SQL was reviewed: it contains only the two widening `ALTER COLUMN` statements and the migration-history insert, with SHA-256 `5AC262122EEAE08331200113479E8448149AF608CBAA62738D50F19028A646BE`. No database write was performed. Do not claim the correction is deployed. Its `Down` migration narrows precision and must not be used unless stored values are proven to fit.

Evidence: apps/api/src/NEXUS.Persistence/AppDbContext.cs:484–503; Configurations/ProjectAnnualBudgetClosingConfiguration.cs:33–44; Migrations/20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision.cs:11–64; docs/finance/SPRINT-1-FINANCIAL-INTEGRITY-REPORT.md §§1–3.

## 3. Required source-data inventory

Run only against a verified read-only target for discovery. Do not infer database identity from localhost, configuration filenames, a health endpoint, or a prior screenshot. Capture the actual environment, server, database, database ID, migration history, EF model/snapshot, and physical schema before any later write.

Inventory by Tenant, Organization, Project, fiscal year, currency, lifecycle state, and source record type:

- Project lifetime ceiling and all approved Project Change Request history.
- Planned PaymentObligation IDs, amounts, currencies, due dates, milestone IDs, settlement totals, deletions, and ownership.
- Every PaymentSettlement by owner, currency/null status, amount/date/reference/idempotency key, audit link, and funding transfer.
- Payment Requests and reversal requests by status, annual-budget association, requester, approver assignments/actions, amount, currency, settlement total, and funding transfer chain.
- Annual Budgets, revisions, approvals, adjustments, carry-forward, closing snapshots, and open/closed period.
- Budget and BudgetRevision rows, including project/portfolio ownership, fiscal year, free-text category/status, approved amount, and history.
- CostEntry rows by type/status, currency, project/budget links, soft-delete state and audit. Keep these distinct from cash settlement.
- Invoices, approvals, categories, due-notification dispatches, and published report snapshots.
- Duplicate idempotency keys, duplicate references, orphaned scalar Project/Milestone IDs, invalid/cross-Tenant references, missing currencies, impossible totals, and requests with ambiguous historical funding.

The present source-only evidence does not establish any live row count or balance. The current audit's proposed calculations and the Sprint 1 report's source observations must not be treated as database results.

## 4. Mapping and reconciliation rules

| Source data | Target mapping | Reconciliation rule / exception |
|---|---|---|
| PaymentObligation with verified Project, currency and amount | Planned Payment with one Installment; preserve source ID in crosswalk and legacy reference | Map due date and optional milestone only after proving same-Project ownership. If amount/currency/Project is invalid or ambiguous, isolate; do not default. |
| PaymentObligation-owned Settlement | Preserve original Settlement identity and legacy owner; associate through a crosswalk only if the Plan/Installment match is deterministic | Keep the XOR owner constraint. No duplicate settlement, owner rewrite, inferred currency or silent link. |
| ProjectPaymentRequestDetail | Retain request, approval, decision and settlement history; add the Plan Installment and Budget Line link only when an authoritative match exists | Do not infer a new Plan from amount/date alone. If a request has no provable Plan, create a reconciliation exception. Legacy unassociated approved requests must be resolved before new submissions/cutover. |
| Existing request-owned Settlement | Preserve as the original request settlement | Resolve the request's target Installment by authoritative request linkage; keep its original request and funding transfer. |
| Budget / BudgetRevision | Preserve source rows and revision workflow; convert to Annual Budget Lines only when Project, fiscal period, currency, category and approved limit are unambiguous | The legacy Budget is not already a target Budget Line. Retain old history as read-only and do not run its separate approval engine for target lines. |
| ProjectAnnualBudget | Preserve identity and approved funding history | No backfill from Project lifetime ceiling, Budget lines, Cost Entries, or parent values. Ensure Budget Line totals and request reservations fit the approved annual amount. |
| ProjectAnnualBudgetClosing / FundingTransfer | Preserve snapshot, approvals, source/destination and supersession chain | Verify one active transfer destination per request and amount = approved unpaid payable only. No additional lifetime reservation. |
| CostEntry / Invoice / dormant records | Preserve unchanged in their own source tables and crosswalk | No automatic conversion to settlements or deletion; unresolved cost recognition remains isolated. |
| Published snapshots | Preserve exact values, timestamps, author, source links and formula interpretation | Never rewrite. Mark legacy definition and cutover date; use new formulas only for new snapshots. |
| Missing settlement currency / unsupported currency | Keep original null/unknown value in the preserved source record | Do not infer from current Project currency. Exclude from cross-currency totals and block any transformation requiring a known currency until explicitly reconciled. |

Every target mapping records source system/type/ID, target ID, transformation version, match method, before/after amounts and currency where known, validation result, actor/time, and unresolved reason. Automated matches must be deterministic and repeatable. Manual matches require dual review by authorized financial governance staff and preserved evidence.

## 5. Reservation and duplicate-prevention controls

1. Before target writes, enumerate all existing lifetime commitments and request allocations without changing them.
2. Create exactly one target Planned Payment and Lifetime Reservation for each approved deterministic source Plan. Enforce a unique source crosswalk so retries cannot create duplicates.
3. A Payment Request linked to a Planned Payment never creates a second lifetime reservation.
4. Existing Payment Request Annual Budget allocations are counted once by request ID and active state. A linked Plan does not add annual/line reservation unless a request is submitted.
5. During backfill, old write APIs remain the sole write source until the Organization-level freeze; target writes stay disabled. Do not dual-write to both rails.
6. Reconcile source and target counts/totals by Project, Tenant, currency, status, request, settlement and funding transfer. Require zero unexplained differences before cutover.
7. For retry safety, use persisted idempotency key/fingerprint, unique source mapping, and transaction rollback. Partial backfills resume from verified checkpoints and do not repeat succeeded rows.
8. Do not count PaymentObligation, its Settlement, a linked Payment Request, and its Settlement as four separate cash/commitment amounts. Define the exposure exactly once at the Plan/reservation level and cash exactly once at settlement level.

## 6. Organization-wide cutover sequence

### Gate 0 — Accept design and freeze scope

Product Owner accepts the target model, unresolved policies, permission matrix, retention/disposition, reports and cutover gates. Confirm exact data mapping and Organization scope. No feature writes or migration begins before this approval.

### Gate 1 — Identity and schema preflight

Identify current runtime environment, actual SQL server/database identity, AppDbContext, migration assembly/startup, applied migration IDs, model snapshot, physical tables/indexes/constraints/defaults, and expected target. Review all proposed additive migration SQL. Ensure existing in-flight changes are integrated and migration ordering is deterministic. Record the actual results; never infer deployment from source files or migration filename.

### Gate 2 — Read-only data profile and reconciliation

Produce the inventory in Section 3, compare source to target rules, resolve or isolate exceptions, and obtain financial governance sign-off. Verify protected histories, current balances, reporting consumers, active requests, transfer chains, and line caps. No target cutover while any difference is unexplained or any necessary source value is inferred.

### Gate 3 — Additive schema and service build

Prepare reviewed additive tables/columns/indexes/constraints for Plans, Installments, lifetime reservation, Annual Budget Lines, request linkage/cycles, adjustment records and reconciliation crosswalk. Do not rewrite applied migrations or drop legacy tables. Validate model/snapshot and generated SQL offline. Apply only reviewed pending Development migrations after target identity and explicit authorization are established; never Production.

### Gate 4 — Dry-run and deterministic backfill

Run the same versioned mapper in dry-run mode first. Review counts, unique matches, exceptions, and before/after amount totals. Then backfill in bounded, idempotent batches while legacy writes are controlled. Persist crosswalk and audit. Re-run reconciliation against the same source snapshot and prove exactly-once relationships and conserved totals.

### Gate 5 — Organization freeze and final readiness

Freeze financial mutations for the entire Organization, not one Project at a time. Resolve in-flight requests, settlement operations, approvals, and year-end tasks. Reconcile final deltas and confirm no open transaction is lost. Obtain Organization readiness approval from the authorized Product Owner/governance roles.

### Gate 6 — One-time Organization switch

Change the Organization's single financial-mode/cutover state atomically. Only one active write rail is allowed. The target API and frontend turn on together for all Projects in that Organization. Existing legacy records become read-only history. A per-Project or mixed-rail fallback is prohibited.

### Gate 7 — Verify and monitor

Verify applied migrations and physical schema; count source/target records; validate lifetime/annual/line availability; sample approval, settlement, reversal, transfer, and audit chains; reconcile currency totals; confirm reports; confirm all Organizations/Projects use the same cutover mode. Record unresolved items and owner. Keep rollback writes disabled until checks pass.

### Gate 8 — Later retirement

Retire legacy write endpoints, tables, and client components only after the retention period, final reconciliation, audit approval, and a separate authorized decision. Do not remove preserved records as part of the first cutover.

## 7. Go / No-Go criteria

### Go only when

- The Product Owner approved the target model and all required policy choices are resolved.
- Actual database identity and environment are verified, and the database is Development/UAT; no Production write is permitted.
- Migration/model/schema review is complete, including decimal precision and default constraints.
- Source and target record counts, amounts, currencies, ownership and lifecycle states reconcile with no unexplained differences.
- All open approved requests, reversals, settlements, year-end transfers and approval cycles are safely resolved or represented.
- All historical settlements, audit records and published snapshots are retained and linked.
- No duplicate lifetime or annual reservation exists; every request is linked to exactly one Budget Line; hard caps reconcile.
- Permission Catalog, EN/AR labels, API service enforcement, frontend checks, Active Deny, maker-checker and Master UAT cases are complete.
- Cutover freeze, idempotency, transaction recovery, monitoring owner and forward-repair procedure are agreed.

### No-Go conditions

- Unknown target identity, pending/unreviewed migration SQL, model/schema mismatch, or unverified decimal/default constraints.
- Ambiguous ownership, missing/guessed currency, missing Project/Milestone link, unsupported lifecycle status, or unresolved orphan.
- Any unexplained difference in financial totals, settlement count, approvals, transfers, or report snapshots.
- Active request can settle against a replaced Plan, self-approval is possible, target settlement actor is not PM-verified, or legacy writes remain enabled.
- Line/annual/lifetime reservation cannot be proven exactly once under concurrent writes.
- Partial project-only cutover or a dual-write state is proposed.

## 8. Rollback, recovery, and data safety

Before target writes begin, rollback means disable target writes and leave legacy source unchanged. After target financial writes begin, do not delete new records, restore an old snapshot over them, reset a migration history, or revert by dropping columns. Halt new writes for the whole Organization if integrity is at risk, preserve the audit, reconcile committed transactions, and repair forward with additive migration and compensating records.

Published reports and source settlements are never rewritten. The corrective precision migration may be reversed only if physical values are inspected and proven to fit the old precision; otherwise narrowing is a data-loss risk. Keep migration history and crosswalks intact. Record every recovery action, actor, scope, and reconciliation evidence.

