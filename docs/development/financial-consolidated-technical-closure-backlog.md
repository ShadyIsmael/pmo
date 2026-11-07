# Financial Management — Consolidated Technical Closure Backlog

**Owner:** Designated EF Migration Owner / build owner
**Target:** Financial Sprint 7A (source, EF, and build closure), then Sprint 7B (runtime and consolidated UAT)
**Database deployment:** Not authorized by this backlog.

## Purpose

Collect finance persistence and verification dependencies deferred from Sprints 2-6. This file records source contracts for the EF owner; it is not permission to generate or apply migrations. Preserve historical migrations and concurrent model/snapshot changes. Resolve against the final current model, not stale assemblies or assumptions about a database.

## Approved technical closure priorities

| Priority | Workstream | Approved scope and boundary |
|---|---|---|
| P0 | API Build Reliability | Establish repeatable full-dependency API builds with normal project references. `BuildProjectReferences=false` alone is not sufficient evidence. |
| P0 | IKVM / MPP Adapter Stabilization | Investigate dependency isolation while preserving Microsoft Project import functionality. Do not remove IKVM or replace the adapter prematurely. |
| P0 | EF Readiness | Reconcile Sprint 3-6 persistence changes, baseline/source migrations, current model, and snapshot under the designated owner; generate and review SQL only through the documented EF Core workflow. |
| P1 | Financial Nullability | Resolve genuine compiler and nullability issues found during authorized consolidated builds. |
| P1 | Runtime UAT | Execute only in Sprint 7B after target, schema, backup, and authorization gates pass. |
| P2 | Roles & Permissions Cleanup | Separate follow-up initiative for duplicate entries, inaccurate categories, and inconsistent EN/AR labels; preserve permission keys and role grants. |

These workstreams are handoff priorities only. Financial Sprint 6 does not start Sprint 7A/7B or perform their builds, EF work, database operations, or UAT.

## EF integration contract

| Source area | Required reconciliation | Safety constraints |
|---|---|---|
| Sprint 2 — Annual Budget association | Ensure Payment Request details reference one Project Annual Budget with stored Fiscal Year/reference provenance; support nullable association for pre-integration historical rows; include scoped foreign keys/indexes. | Do not infer a historical Fiscal Year or budget; preserve existing requests, approvals, and settlements. |
| Sprint 3 — shared settlements | Extend the existing `PaymentSettlements` source table for both legacy `PaymentObligationId` and `ProjectPaymentRequestId`, with `TenantId`, `ProjectId`, currency, description, idempotency/reference indexes, and tenant/project-scoped ownership constraints. Exactly one owner is valid. | Preserve legacy obligation rows. Derive ProjectId only from a verified authoritative obligation; abort or use an approved nullable transition if ownership cannot be proven. Historical settlement currency was not persisted and must remain NULL; do not copy current obligation currency. Preserve amount, date, reference, and owner. No duplicate settlement ledger. |
| Sprint 4 — reversals | Ensure `ProjectPaymentRequestDetails` contains the reversal kind/reference fields, a restrictive same-Tenant/same-Project self-FK from `(TenantId, ProjectId, ReversesApprovalRequestId)` to the original `(TenantId, ProjectId, ApprovalRequestId)`, and a check constraint that prevents self-reference and requires a source link only for reversal kind. Verify indexes and unique idempotency constraints remain compatible. | Reuse ApprovalRequest/ApprovalAction/assignment/AuditEntries. Do not introduce a reservation/reversal ledger. Do not rewrite approval or settlement history. The migration must preserve the original amount as the immutable approved request and derive releases from separately Approved reversal requests. |
| Concurrent/shared changes | Reconcile the latest AppDbContext, configurations, snapshot, baseline/legacy migration strategy, and concurrent non-finance EF changes together. | Only designated EF owner edits DbContext/migrations/snapshot. Do not delete, squash, or regenerate shared migrations without verified application history and an approved strategy. |

## Sprint 7A closure tasks

1. Read current `AGENTS.md`, documented database workflow, and all finance sprint handovers; inspect current Git state and shared EF ownership.
2. Confirm the authoritative current model, migration assembly, baseline, and all pending source changes, including project fiscal calendar, Annual Budget, Payment Request association, shared Settlement ownership, and Reversal self-FK/check constraint.
3. Generate only the migration strategy authorized by the EF owner. Preserve all existing migration files and archive/reconciliation history as required by the owner; do not apply it.
4. Build and synchronize API, Persistence/Design-Time, and the active `apps/web-react` frontend in an authorized runner. Preserve dependency/functionality and do not bypass security review or send unapproved telemetry.
5. Review incremental SQL for nullable historical transitions, verified backfills, scoped FKs, unique indexes, check constraints, no destructive operations, and the selected baseline strategy.
6. Produce reviewed scripts through the EF Core database workflow and document exact migration source/SQL hashes and exit statuses.

## Sprint 7B runtime/UAT gates

Before any Development database deployment, separately verify actual runtime identity, environment/configuration source, physical schema, applied migration history, and relevant existing data; verify an independent backup and restore readiness; review the approved deployment/rollback plan; and obtain explicit authorization. The previous `Invalid object name 'Tenants'` log is not by itself evidence of which database is active or which migration strategy is safe.

After an authorized isolated runtime target exists, execute consolidated API/database/browser/permission/financial tests from the Master UAT Checklist. Include settlement/reversal concurrent execution under SQL Server; source inspection alone cannot establish runtime concurrency safety. No production write is included.

## Known policy dependencies

- Initial delegation authority for `ProjectPaymentReversal.Create` and `ProjectPaymentReversal.Approve`; no seeded role may receive either sensitive permission automatically.
- Sprint 5 authority for closed fiscal periods, Year-End Closing, Carry Forward, and treatment of approved reversals after fiscal-year transition. Sprint 4 fails closed if the original budget fiscal year is not current.
- Any paid-settlement correction/refund requires separate approved business, permission, and persistence rules. It is not implied by commitment reversal.

## Current status

Sprint 4 reversal source and documentation are present. Compile status is **NOT VERIFIED**; migration/snapshot/SQL status is **DEFERRED**; database status is **UNCHANGED / NOT ACCESSED** for Sprint 4; runtime UAT is **DEFERRED**. This backlog does not claim Financial Sprint 7 closure.

## Sprint 5 — Year-End Closing persistence reconciliation (Sprint 7A)

- Reconcile source entities and mappings for `ProjectAnnualBudgetClosing` and `ProjectPaymentRequestFundingTransfer`, plus `ProjectAnnualBudget.ApprovedReservedCarryForward`, closed metadata, `ProjectPaymentRequestDetail.FundingTransferId`, and `PaymentSettlement.FundingTransferId`.
- Coordinate these changes with pending Sprint 3 Settlement and Sprint 4 Reversal schema before any snapshot or migration work. Do not regenerate or apply a migration from stale assemblies.
- Review Tenant/Project/source/destination/completed-closing FKs; currency, amount, fiscal-year and carry-subset checks; one closed closing per Project/FY; active closing uniqueness; idempotency; consecutive transfer-chain/successor uniqueness; and indexes needed by annual allocation and audit reads.
- Validate existing data before any required constraints. Do not infer historical Fiscal Years, currency, transfers, or ownership. Preserve nullable legacy funding references until verified reconciliation exists.
- Review current FundingTransferId on settlement and reversal, approved destination budget opening, historical transfer chain, destination capacity, closed-period checks, audit atomicity, and no double reservation. Generate/review incremental SQL only under the approved gate; do not apply it as part of Sprint 7A.
- Verify `PaymentObligationService`'s Tenant fiscal-period resolution, closed-period rejection, and common serializable Project-row lock against closing and Payment Request writers. Preserve the existing Payment Obligation settlement owner and audit transaction; do not split the shared settlement ledger.
- Update EF snapshot only by the designated owner after all Sprint 3–5 model changes are reconciled.
- Sprint 7B must verify role delegation, cross-scope access, fiscal boundaries, duplicate/retry behavior, concurrent close/settlement/reversal/approval, multi-year transfers, immutable closed statements, audit events, and EN/AR/RTL UI.

## Sprint 6 - Program/Portfolio computed rollups

- Source-level endpoints and UI aggregate existing Project annual-budget, Payment Request, reversal, settlement, funding-transfer, and closing records. No persisted parent financial entity or Sprint 6 schema migration was introduced.
- The implementation uses batched reads in 800-Project chunks and computes totals over the full authorized Project population before paging drill-down rows. Review query plans and indexes during Sprint 7A if representative portfolio scale shows a need; candidate existing-table indexes include tenant/project/year on budgets and closings, tenant/project/request on details/transfers, and tenant/project/payment-date on settlements. Do not add indexes without checking the consolidated model and actual execution plans.
- Verify the explicit `financial-rollup` route does not use the shared `admin` role-name GET bypass. It requires `Program.View` or `Portfolio.View`, `finances.read`, authenticated tenant, and authorized parent/Project scope for every caller.
- No EF Snapshot or migration change is required for Sprint 6. No build, SQL Server query, database deployment, UAT, authorization runtime, or performance test was performed. Keep runtime evidence in Sprint 7B.
