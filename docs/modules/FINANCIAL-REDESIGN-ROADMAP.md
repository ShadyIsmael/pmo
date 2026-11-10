# Financial Management: Proposed Redesign Roadmap

**Status:** Financial Sprints 0-5 source work is recorded. Sprint 6 source implementation adds read-only Program/Portfolio rollups derived from authorized Project records. Consolidated build verification, EF reconciliation where needed, database delivery, and runtime UAT remain deferred to Sprint 7A/7B. No database change is authorized by this record.  
**Review date:** 2026-10-30  
**Related documents:** [Current-State Audit](FINANCIAL-CURRENT-STATE-AUDIT.md), [Target Business Model](FINANCIAL-TARGET-BUSINESS-MODEL.md), [Planned Payments](FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md)

## 1. Purpose and delivery constraints

This roadmap turns the target business model into an ordered, reviewable delivery plan. Project Financial Management Part 1 and the Payment Request workflow in Sprint 2 are approved for source implementation. Approval does not authorize a Development database write, Production change, data correction, or feature retirement.

Preserve the nine agreed rules in the target model throughout delivery. In particular, an actual settlement remains a cash-paid fact, does not create a cost, and does not deduct from the approved project budget. Existing finance records, published reports, and in-flight code remain protected until an approved preservation and cutover plan is completed.

Only `apps/web-react` is in frontend scope. The retired `apps/web` must not be recreated, built, deployed, or treated as an audit target.

## EF source integration and deployment gate - 2026-10-29

The earlier source-level schema freeze has been released for the approved Sprint 2 persistence integration. The model now reuses `ApprovalRequest`, `ApprovalAction`, and `AuditEntries`, with project-scoped payment-request detail and approver assignments. The source migration chain includes `20261029181208_ProjectPaymentRequestPersistence`; no database migration was applied.

The original `20261029155617_InitialFopxPmBaseline` was preserved. Because the current Development connection, expected server/database, and environment inputs are unavailable, its applied status cannot be independently checked. The Sprint 2 migration is therefore additive and keeps the prior source history intact. A new empty database receives the complete model by applying the baseline and then the Sprint 2 migration. Re-squashing to one initial migration requires verified evidence that no target database has applied the original baseline.

The Development connection, explicit expected server/database, and `ASPNETCORE_ENVIRONMENT` inputs remain absent. No database identity, migration history, backup, or physical schema was verified. No database changes or backup operations were performed. Use the repository gate for read-only inspection and a separately authorized Development deployment; a backup is optional for Development/UAT and does not gate the write.

## 2. Delivery sequence and approval gates

| Stage | Outcome | Entry gate |
|---|---|---|
| Sprint 0 | Tenant Fiscal Year Configuration; source implementation precedes annual budget work. | Runtime and database evidence follow the respective sprint handovers. |
| Sprint 1 | Project Annual Budget Foundation, with Tenant-derived fiscal periods. | Project-level only; no Program/Portfolio budgets or operational cutover in Sprint 1. |
| Sprint 2 | Payment Requests use the associated approved Project Annual Budget for new reservations. | Source migration/deployment and runtime acceptance remain part of the gated technical closure. |
| Sprint 3 | Actual Settlements against approved Payment Requests. | Settlement source changes are pending the Sprint 7 migration and runtime closure; no duplicate budget deduction. |
| Sprint 4 | Approved reversal workflow for unpaid commitment only. | Source implementation; no paid-settlement reversal, refund, migration generation, DB deployment, or runtime UAT in this sprint. |
| Sprint 5 | Project Year-End Closing, immutable closing statements, and approved free/reserved Carry Forward source workflow. | Source implementation is recorded in [Sprint 5 Year-End Closing](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md); persistence reconciliation and runtime verification remain deferred. |
| Sprint 6 | Read-only Program/Portfolio reporting from unique, authorized descendant Projects. | Source implementation is recorded in [Sprint 6 Program/Portfolio Rollup](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md); no independent parent budgets or persisted rollups. |
| Sprint 7A | Consolidated technical closure. | Reconcile Sprint 3-5 pending EF model work with the designated owner; build/snapshot/script review and documented database workflow. Sprint 6 currently adds no schema. |
| Sprint 7B | Consolidated runtime UAT and financial reconciliation. | Execute authorized database, permission, browser, performance, and concurrency scenarios after identity, schema, migration, and deployment gates pass. Backups are optional for Development/UAT. |
| Later enterprise scope | Add only business-justified capabilities outside the coherent MVP. | Separate Product Owner request and approval; no implied authorization from this roadmap. |

Before any Development/UAT database update, follow `AGENTS.md`, `current repository database safeguards`, and `docs/development/ef-core-database-workflow.md`: identify the actual context/startup/migration assembly and target, inspect model and migration history, review generated SQL, and verify environment and database identity. Backups are optional; do not require independent backup storage or `RESTORE VERIFYONLY`. A destructive reset still requires explicit Product Owner authorization for Development `Shady/NexusPmo`, live environment/server/database verification, every EF migration, and the full seed before closure. Do not modify Production or any other database; preserve mandatory Production backup and recovery safeguards; never reset migration history, remove migrations, or rewrite applied migrations. Current target and backup evidence are unavailable.

## 3. Earlier proposed design work packages

The detailed work-package analyses below predate the current authorized sprint numbering and contain historical assumptions that have since changed. They are retained as design references only. The current execution scope and status are the table in Section 2 and the corresponding Sprint 0-6 implementation reports. In particular, the former proposed Sprint 4 cost-recognition package is not the current Financial Sprint 4 authorization; cost-category and recognition decisions remain deferred.

Current handovers: [Sprint 2 Payment Requests](FINANCIAL-SPRINT-2-IMPLEMENTATION-REPORT.md), [Sprint 3 Actual Settlements](FINANCIAL-SPRINT-3-ACTUAL-SETTLEMENTS.md), [Sprint 4 Payment Request Reversals](FINANCIAL-SPRINT-4-PAYMENT-REQUEST-REVERSALS.md), [Sprint 5 Year-End Closing](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md), and [Sprint 6 Program/Portfolio Rollup](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md). Read-only reporting semantics are documented in [Financial Reporting](FINANCIAL-PROGRAM-PORTFOLIO-REPORTING.md). Pending cross-sprint EF/build work is tracked in the [Consolidated Technical Closure Backlog](../development/financial-consolidated-technical-closure-backlog.md). No database operation was performed for Sprint 6.

### Sprint 0 - Source inventory and data-safety preflight

**Objective:** Complete source-level inventory and identify the Development database target before persistence writes. Source inventory is complete; Development identity, migration history, physical schema, and backup remain unverified. Database work is paused under the EF schema freeze.

**Business scope:** Product Owner approved Project Financial Management Part 1/Sprint 1 and the bounded Payment Request/Sprint 2 rules. Remaining D1-D17 questions are recorded in the target-model decision register and block only the affected unresolved behavior.

**Backend:** Inventory active finance APIs, entity relationships, authorization paths, report consumers, and integration owners. Use the repository EF database gate to identify the real context, migration assembly, startup project, environment and Development target.

**Frontend:** Map active `apps/web-react` finance views and metrics to their API/source. The retired `apps/web` app is not a target.

**Data model / preservation:** Preserve project ceilings, budget lines/revisions, costs, commitments, payment obligations/settlements, parent Program/Portfolio values, and published snapshots. Do not infer DB identity from filenames, localhost, or health endpoints. The current gate has no connection inputs, so migration history, physical schema, and backup remain unverified.

**Dependencies / decisions:** Approved Part 1/Sprint 1 rules in the target model; remaining decision items stay assigned to future scope.

**Acceptance criteria:**

- Source inventory covers current project financial records and consumers without rewriting historical records.
- Product Owner approval for Part 1/Sprint 1 and remaining D1-D17 statuses are recorded.
- Development DB identity, migration history, schema, and persistence-test evidence are marked unavailable until the gate can verify them. Backup evidence is optional for Development/UAT and does not gate the write.

**Complexity:** Medium.  
**Risks / migration considerations:** `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING`, `ASPNETCORE_ENVIRONMENT=Development`, and explicit expected server/database inputs were absent at the last preflight. Do not infer a target or apply migrations. Continue safe source implementation only; the schema dependency is separately tracked above.

### Sprint 1 - Approved Project Financial Foundation and Audit Trail

**Objective:** Deliver the approved project-only financial summary, project approval configuration, dedicated financial activity, and coherent Project Financial workspace.

**Backend:** Read the authoritative project budget and configured project currency. Add centralized balance calculations that keep settlements separate from the approved budget and do not claim Payment Request allocations/availability before their source records exist. Persist only the `Sequential`, `Parallel`, or `Both` project setting; do not build the approval engine. Add a finance-permission endpoint for the shared audit store, enforce project/tenant scope, and append budget/configuration/settlement events atomically with the related mutation.

**Frontend:** Consolidate the foundation, existing financial operations, and chronological Financial Activity in Project Details → Financial. Keep advanced filters collapsed by default; support event type, actor, date range and financial reference. Use EN/AR/RTL, authoritative values, and explicit unavailable/loading/error states. Preserve existing financial screens and historical records.

**Data model implications:** Approval mode is a project-scoped persisted setting. The required schema change is nullable `Projects.FinanceApprovalMode` (`nvarchar(24)`) with no default or data backfill. Schema synchronization is **BLOCKED** during the freeze; the designated EF Migration Owner must include or supersede it in the consolidated initial migration. The local migration proposal is unverified and must not be applied. `AuditEntries` remains the only audit store. Do not create Settlement-derived costs or budget deductions.

**Dependencies / decisions:** Product Owner approval dated 2026-10-29. D11/D13/D17 remain partially unresolved beyond the bounded configuration, currency, and audit rules approved for Part 1.

**Acceptance criteria:**

- Initial approved project budget is sourced from `Project.Budget` in configured project currency; approved ceiling changes are auditable.
- Payment Request allocations and Available Budget remain unavailable until those source records exist; no misleading zero or settlement-derived amount is shown.
- Settlements contribute only to actual paid/outstanding payment measures and do not create a CostEntry or reduce Approved Budget.
- Approval configuration accepts Sequential, Parallel, Both, or unconfigured; writes require `Finances.Write`, tenant/project scope, and a current concurrency version.
- Financial Activity reads Finance events only through the shared append-only audit service and requires `Finances.Read` plus project/tenant scope.
- A failed financial audit append cannot commit its corresponding protected budget, configuration, or settlement change.
- Existing financial operations/routes remain available; Program/Portfolio Finance is unchanged.
- Database/runtime acceptance is not claimed until Development identity, migrations/schema, applied migration, and persistence-test evidence are verified. Report any explicitly requested backup separately; it is optional for Development/UAT.

**Complexity:** High.  
**Risks / migration considerations:** Development identity, history, and schema are unverified. Keep database application blocked until the delivery gate verifies the target and required migration/schema checks. A backup is optional for Development/UAT. The current source chain is additive; do not treat the initial migration alone as the full Sprint 2 schema.

### Sprint 2 - Payment Requests and approval workflow

**Objective:** Deliver project-only Payment Request records, sequential/parallel approvals, and an approved-only budget reservation derived from authoritative project finance sources.

**Approved behavior:** Authorized Project Managers create requests with title, amount, free-text reason, optional milestone, and eligible Project Team approvers. Project currency is mandatory. Draft/Pending do not reserve. Approval requires an atomic recheck against `Approved Budget - active approved request allocations`. Sequential approval uses the selected order and stops on rejection. Parallel approval records Mandatory/Optional decisions, requires at least one Mandatory approver, and completes when every Mandatory approver approves; any Mandatory rejection rejects. Pre-approval cancellation is authorized. An approved request needs a separately approved Reversal Request; no refund is implied. Attachments are optional.

**Integrated persistence contract (source-level; database deployment pending):**

| Store | Required contract |
|---|---|
| `ApprovalRequest` (existing canonical root) | Reuse the existing tenant-scoped root for a Payment Request (`Type = ProjectPaymentRequest`, `EntityType = Project`, `EntityId = ProjectId`). Its `Id` is the request id; use existing `Title`, `Description` for free-text reason, `EntityNumber` for the financial reference, `Amount`, `Currency`, `RequestedBy`, `RequestedAt`, `Status`, and `RowVersion`. For reversal, use a distinct `Type = ProjectPaymentRequestReversal`. Set `Currency` to the exact configured project currency on every write; never inherit its current GBP default. Preserve existing generic approval records and semantics. |
| `ProjectPaymentRequestDetail` (new one-to-one extension) | Key/FK `ApprovalRequestId`; `ProjectId`; nullable `PlanActivityId` for the optional milestone; `ApprovalMode` (`Sequential` or `Parallel`); request kind (`PaymentRequest` or `ReversalRequest`); nullable `ReversesApprovalRequestId` for a reversal. Enforce tenant-safe Project, `PlanActivity`, and original-request relationships and validate that the milestone belongs to this project’s active Working plan in the service. The extension must not duplicate request amount, currency, status, or requester. |
| `PaymentRequestApproverAssignment` (new durable assignment set) | Tenant-scoped FK `ApprovalRequestId`; `ApproverUserId`; nullable `Position` (positive and unique when present; null for Parallel); `Requirement` (`Mandatory`/`Optional`); assignment state and SQL rowversion. Unique constraints: `(TenantId, ApprovalRequestId, ApproverUserId)` and a filtered `(TenantId, ApprovalRequestId, Position)` only where Position is not null, so parallel checklist members can coexist without a sequential-order collision. Validate active Project Team membership and a real user id when submitting. Sequential selections are all Mandatory; Parallel requires at least one Mandatory. |
| `ApprovalAction` and `AuditEntries` (existing stores) | Reuse `ApprovalAction` for each append-only approve/reject decision, with approver and level/position. Append corresponding financial `AuditEntries` in the same transaction. Do not create a second audit store or treat current GET-only `ApprovalService` as a command workflow. |

**Required transactional behavior (historical proposal; current implementation is described in the Sprint 2 and Sprint 4 handovers):** Serialize approvals that change reservation on the common Project financial lock, reread authoritative persisted balances, reject over-allocation, and write decision/audit state atomically. Reservation is derived from canonical request/reversal status; do not add a second ledger. The now-approved Sprint 4 rule releases only unpaid commitment under a separate approval and does not create settlements, refunds, or costs.

**API/UI contract (historical proposal; current status supersedes this paragraph):** Payment Request endpoints remain project-scoped and preserve requester/team validation and existing planned-payment screens. Reversal endpoints and UI are implemented at source level; see [Financial Sprint 4 Reversals](FINANCIAL-SPRINT-4-PAYMENT-REQUEST-REVERSALS.md). Approved requests remain non-cancellable directly.

**Audit event actions in the original Payment Request implementation:** `Created`, `Edited`, `Submitted`, `ApproversAssigned`, `ApproverApproved`, `ApproverRejected`, `Approved`, `Rejected`, `BudgetReserved`, and `Cancelled` on the `ProjectPaymentRequest` audit entity. Current reversal actions and their shared audit/timeline integration are listed in the [Sprint 4 handover](FINANCIAL-SPRINT-4-PAYMENT-REQUEST-REVERSALS.md).

**Historical status snapshot (superseded by Sections 2 and 3 above and the Sprint 2-4 handovers):** This description predates Annual Budget integration and Sprint 4 reversal implementation. It must not be used as evidence of the current reservation source, migration state, build result, or reversal availability. Current sprint handovers report the current source status; runtime/database acceptance is still pending.

**Dependencies / decisions:** Approved bounded workflow rules; D2, D8, D11, D13, and D17 remain partially open only as listed in the Decision Register. The unpaid-commitment reversal subset is approved in Sprint 4; remaining settlement-correction/refund and initial sensitive-permission delegation decisions are not.

### Earlier proposed work package (prior label: Sprint 3) - Approved project ceiling, allocations, and revisions

**Objective:** Establish one authoritative project budget ceiling and controlled allocation history.

**Backend:** Preserve the Project Change Request boundary for ceiling changes. Represent initial approval and accepted ceiling changes as immutable, effective-dated decisions (or a proven equivalent history). Keep project allocation lines separate from the ceiling. Route changes to approved allocation amounts only through the approved revision workflow; keep draft edits distinct. Enforce the approved allocation-total rule and an explicit approved exception path if D2 permits one.

**Frontend:** Provide a project budget view with baseline, current approved ceiling, allocations, unallocated amount, revision history, approval queue, and as-of/effective dates. Clearly label proposal/draft values separately from approved values.

**Data model implications:** Likely requires version/decision history and controlled linkage to existing Change Requests and `BudgetRevision` records. Preserve current IDs and relationships. Do not overwrite historical line values or treat `Budget.ActualAmount`/`CommittedAmount` as authoritative actuals.

**Dependencies / decisions:** Sprint 1; D1, D2, D11, and D13.

**Acceptance criteria:**

- The current ceiling is reproducible from approved decisions effective at the requested date, while the original baseline remains available.
- Approved allocations are separate from project ceiling and cannot exceed it unless an approved, auditable exception is allowed by policy.
- Direct update paths cannot bypass the revision/approval policy.
- Concurrent or duplicate revision attempts cannot both approve incompatible values.
- Rejected, withdrawn, and pending revisions do not alter approved amounts; history identifies actor, reason, and decision time.

**Complexity:** High.  
**Risks / migration considerations:** Legacy `Project.Budget`, Change Request history, budget lines, and inferred “original budget” may not reconcile. Produce per-project exceptions for Product Owner/Finance resolution; retain unresolvable source values and prevent cutover for affected projects rather than silently choosing one.

### Earlier proposed work package (prior label: Sprint 4) - Cost categories, recognition, labor, and forecast inputs

**Objective:** Define what counts as actual cost and keep future forecast amounts separate from posted actuals.

**Backend:** Add the approved controlled category catalog and cost lifecycle. Only an approved/posted cost counts as actual. Support evidence, source references, effective recognition date, correction/reversal, and allocation linkage. Implement the approved labor rule using one authoritative source per labor period. Provide explicit forecast-to-complete items with method/version/as-of metadata; never infer ETC from the approved budget alone.

**Frontend:** Replace free-text lifecycle/category choices with localized controlled codes. Separate drafts, pending approval, posted actuals, corrections, and forecasts. Display cost date and recognition date where both exist; show the selected labor/forecast method and drill-through source.

**Data model implications:** Requires controlled category references, cost state/history, evidence/source linkage, and forecast records or versions. Existing free-text category/type/status data needs a reviewed mapping; unknown values remain preserved and excluded from new posted calculations until resolved.

**Dependencies / decisions:** Sprints 0-3; D3, D4, D5, D6, D10, D11, D13, and D17.

**Acceptance criteria:**

- Draft, rejected, and unposted costs are excluded from actual cost; approved posted facts are included once at the approved effective date.
- A source labor/time fact cannot be recognized twice through both time-derived and manual entries.
- A settlement never creates or changes a cost; a cost posting never implies cash payment.
- Reversals preserve the original fact and produce a traceable net amount.
- Forecast inputs are distinguishable from actuals, carry a method/version/as-of date, and do not include actual-to-date twice in EAC.
- Tax gross/net presentation follows the approved rule and all monetary calculations use the approved precision and rounding policy.

**Complexity:** High.  
**Risks / migration considerations:** Existing `CostEntry` records may lack dates, categories, state, currency, or labor source keys. Do not auto-post based on free-text `Status`; map each record deterministically, retain source values, and quarantine ambiguous entries from authoritative calculations until reviewed.

### Earlier proposed work package (prior label: Sprint 5) - Commitment lifecycle and cost exposure reconciliation

**Objective:** Make purchase/contract exposure auditable and prevent commitments and recognized costs from being counted twice.

**Backend:** Add approved commitment changes/amendments and releases as history. Record explicit amount-level allocations from recognized costs to commitments. Calculate authorized gross amount, recognized-against amount, and remaining commitment separately. Guard close/cancel transitions when unreconciled exposure or disallowed linked obligations remain. Do not use the current `InvoicedAmount` copy as an independent exposure source.

**Frontend:** Provide commitment details, supplier/reference, approval, amendment/release history, linked costs, remaining exposure, and close/cancel validation. Keep invoices as references/evidence unless D9 approves a minimum invoice workflow.

**Data model implications:** Requires commitment event history and amount-level cost allocation/reconciliation. A full vendor invoice workflow is out of scope unless separately approved. Existing commitment IDs and records remain available during transition.

**Dependencies / decisions:** Sprint 4; D7, D9, D11, D13, and D17.

**Acceptance criteria:**

- Remaining commitment equals approved authorized amount less accepted releases and explicit recognized-cost allocations as of the same cutoff.
- A linked recognized cost reduces remaining commitment once and remains in actual cost once; it is not added again as gross commitment exposure.
- Unlinked costs do not reduce commitments by guesswork.
- Amendments/releases are approved, effective-dated, concurrency-protected, and auditable.
- Closing/cancelling a commitment cannot erase unresolved recognized, paid, or approved obligation facts.
- Any invoice amount is included only through an explicitly approved lifecycle and cannot duplicate a recognized cost.

**Complexity:** High.  
**Risks / migration considerations:** `PurchaseCommitment.InvoicedAmount`, active status, and cost records may not provide enough detail to reconstruct historical matching. Preserve gross source values, expose unmatched amounts, and require reviewed reconciliation before reporting the new remaining-commitment metric as authoritative.

### Earlier proposed work package (prior label: Sprint 6) - Payment obligation corrections, evidence, and notification policy

**Objective:** Complete the existing planned-payment lifecycle while preserving its separation from cost and budget.

**Backend:** Keep one obligation with multiple partial settlements, idempotent settlement creation, balance/concurrency protection, and separate due versus paid state. Add immutable settlement reversal/adjustment events, controlled obligation revisions/cancellation, evidence references, and the approved timezone/recipient/escalation policy. Maintain tenant-scoped payment ownership and authorization.

**Frontend:** Extend the planned-payments and project finance views with obligation approval/revision, settlement history, evidence, outstanding balance, reversal flow, local due state, and notification context. Keep milestone linkage optional and informational.

**Data model implications:** Likely requires settlement reversal/adjustment and obligation decision history, evidence references, timezone/dispatch fields, and constraints for valid net settlements. Existing settlement rows must remain unchanged and linkable.

**Dependencies / decisions:** Existing payment feature and Sprint 1; D8, D11-D13, D15 where payment reporting is snapshotted, and D17.

**Acceptance criteria:**

- Outstanding balance equals approved obligation amount less valid applied settlements plus/minus accepted immutable corrections, never below zero without an explicit approved exception.
- Multiple partial settlements and idempotent replay remain supported under concurrent writes.
- Paid/partially paid state and upcoming/due/overdue state are independently derived and displayed.
- Settlement reversals preserve the original transaction, require a reason and authorized action, and update net paid/outstanding exactly once.
- Recording, reversing, or correcting a settlement does not create a `CostEntry` or deduct from the approved project budget.
- Milestone association is optional; changing a milestone does not change payment amount or due date.
- Notifications follow the approved timezone, recipient, preference, deduplication, and escalation policy.

**Complexity:** Medium-High.  
**Risks / migration considerations:** Existing obligation/settlement migrations and model snapshot changes are uncommitted concurrent work. Reconcile against the actual shared model and migration history only during authorized implementation; never infer Development application state from repository files. Historical settlement records without evidence remain valid but should show evidence as unavailable rather than fabricated.

### Earlier proposed work package (prior label: Sprint 7) - Shared financial calculations, rollups, and immutable snapshots

**Objective:** Replace competing metric formulas with a single, versioned calculation contract and preserve as-of reporting.

**Backend:** Implement the shared calculation/query service for project, tenant overview, Program, Portfolio, and status-report capture. Require explicit tenant, authorized unique project set, currency, cutoff, and formula version. Return separate typed metrics for ceiling, allocations, actual cost, remaining commitment, paid, outstanding, ETC/EAC, variances, and availability. Store formula version, cutoff, capture timestamp, currency, source IDs/versions, and calculation outcome in immutable published snapshots. Group by currency; no mixed-currency total without an approved FX policy.

**Frontend:** Bind project/dashboard/Program/Portfolio finance displays to the same typed results. Label each measure, currency, and as-of date; provide drill-through to source records. Show missing forecast/currency as unavailable, not zero or a fallback.

**Data model implications:** May require versioned calculation metadata and additional snapshot source/provenance fields. Existing published snapshots remain byte-for-byte/logically immutable; any corrected report is a new version linked to the prior publication.

**Dependencies / decisions:** Sprints 1-6; D8, D10, D14, D15, and D16. D14 may remain deferred for MVP only if all outputs stay currency-separated.

**Acceptance criteria:**

- A given project/currency/cutoff/formula version returns the same values to each consuming API and report capture path.
- Actual cost uses posted recognized costs only; remaining commitment excludes recognized amounts already matched; cash paid and payment outstanding remain separate.
- Availability does not subtract settlements and uses only the approved treatment for payment obligations.
- EAC is actual plus remaining forecast, with no duplicate actual or commitment amount.
- Program/Portfolio aggregation deduplicates project IDs, respects authorization, uses a common cutoff, and reports currencies separately.
- Existing published snapshots do not change; a correction creates a linked replacement publication with reason and provenance.
- Source IDs/versions and formula version allow a reviewer to explain each published value.

**Complexity:** High.  
**Risks / migration considerations:** Current dashboards and snapshots may rely on stale budget-line copies or legacy parent totals. Compare old and proposed values by source and explain differences; do not silently replace or rewrite historical results. If a prior snapshot lacks provenance, preserve it and identify the limit instead of backfilling guessed source values.

### Later cutover work package (prior label: Sprint 8) - Historical reconciliation, controlled cutover, and MVP acceptance

**Objective:** Move consumers to the approved sources and calculations only after historical data and outputs reconcile.

**Backend:** Run approved, repeatable reconciliation/backfill commands in a non-production validation environment first. Provide exception reports, read-only comparison outputs, and a controlled cutover/rollback mechanism. Keep legacy read paths available until acceptance; do not hard-delete or rewrite old records. Apply only approved, reviewed migrations through the repository database gate.

**Frontend:** Finish the Project Finance, Budgets, Costs & Forecast, Commitments, Payments, Program/Portfolio, and Published Reports information architecture using the shared API contract. Remove duplicate labels/calculations only after the replacement view is accepted; do not remove existing feature access before explicit retirement approval.

**Data model implications:** Backfill only well-mapped records, preserve source IDs and original values, and retain crosswalks from legacy to new records. Unmapped/conflicting rows remain visible as reconciliation exceptions and are excluded from new authoritative totals until resolved. No destructive cleanup is part of this sprint.

**Dependencies / decisions:** All prior sprints; Product Owner and Finance approval of reconciliation tolerances, exception ownership, cutover, and rollback plan. D15-D17 must be resolved for snapshot, parent-total, and evidence preservation.

**Acceptance criteria:**

- Per-tenant and per-project reconciliation reports account for every source record as mapped, intentionally excluded with reason, or unresolved; totals by currency and lifecycle state tie to the approved source inventory.
- New outputs are compared to the old dashboards and reports at the same scope/cutoff. Every material difference has a source-based explanation and owner sign-off.
- Published history and settlement history remain unchanged; new snapshots and transformed records retain source provenance.
- Authorization tests show no cross-tenant/project reads or writes; calculation checks cover overpayment, partial settlement, reversal, duplicate commitment/cost linkage, multiple currencies, as-of boundaries, and project-set deduplication.
- Finance and Product Owner accept the MVP outputs, unresolved exceptions, and rollback criteria before production cutover is separately considered.
- Legacy data and routes are not retired until a separate, explicit approval confirms replacement completeness, preservation, and retention.

**Complexity:** High.  
**Risks / migration considerations:** Historical values may be irreconcilable or lack currency/effective dates; those are blockers or visible exceptions, not a reason to invent values. Production cutover, if later requested, requires a separate release plan and authorization. This roadmap itself authorizes no Production access or change.

## 4. MVP completion checklist

The MVP is coherent only when all of the following are true:

- The Product Owner Decision Register has approved or safely deferred every decision that affects an enabled path.
- Project ceiling, allocation, recognized actual cost, commitment, payment obligation, and settlement remain distinct source records.
- Shared formulas, authorization scope, currency handling, effective timing, and double-count rules are consistent across project, dashboard, Program, Portfolio, and snapshots.
- Financial approvals and posted-fact corrections are auditable, concurrency-safe, and non-destructive.
- Planned payments continue to support multiple partial settlements and preserve the agreed no-auto-cost/no-auto-budget-deduction rule.
- Historical records and published reports are either reconciled with provenance or explicitly retained as unresolved legacy history; no silent deletion or rewrite has occurred.
- Database synchronization, Development target identity, backup, applied migrations, physical schema, and persistence-test evidence are reported truthfully for any future database implementation. None of those checks is claimed by this design-only roadmap.

## 5. Product Owner decision linkage

Use the canonical IDs and recommendations in [the target-model Decision Register](FINANCIAL-TARGET-BUSINESS-MODEL.md#8-product-owner-decision-register). Minimum decision gates are:

| Delivery | Decisions that block the affected scope |
|---|---|
| Sprint 0 - Tenant Fiscal Year | Tenant setting and fiscal-date calculation; no independent calendar scope. |
| Sprint 1 - Project Annual Budget | D1, D2, D11, D13, D17 within the approved Project scope. |
| Sprint 2 - Annual Budget integration | D2, D8, D11, D13, D17; approved requests reserve once, settlements do not deduct again. |
| Sprint 3 - Actual Settlements | D8, D11, D13, D17 within posted payment recording. |
| Sprint 4 - unpaid commitment reversals | D2, D11, D13, D17 for the bounded release-only workflow; paid reversal/refund remains unapproved. |
| Sprint 5 - Year-End Closing / Carry Forward | D1, D7-D9, D11-D13, D15, D17 as applicable; source implementation prepared, technical closure deferred to Sprint 7A/7B. |
| Sprint 6 - Project-derived parent reporting | D14 and D16; no independent Program/Portfolio budget. |
| Sprint 7 - technical closure / UAT | Applicable remaining decisions and deployment/reconciliation gates; does not implicitly approve unresolved business rules. |

If a decision is deferred, document the disabled functionality or safe behavior, the owner, and the point at which approval is required. A deferred answer must not be converted into an unapproved financial assumption.

## 6. Scope explicitly deferred from the MVP

Unless separately approved with a justified business case, do not include full accounts payable, invoice matching, ERP/procurement integrations, FX conversion, multi-entity accounting, a general ledger, journal entries, accruals, capitalization/depreciation, fiscal close, statistical forecasting, or bank/payment-provider execution. The existing `Invoice` entity does not by itself authorize an invoice workflow.

## 7. Stop point

The Tenant Fiscal Year Configuration prerequisite was completed through the existing Tenant settings store and is documented in [the fiscal-year delivery record](tenant-fiscal-year-configuration.md). That earlier record's stop point was superseded on 2026-10-30 by separate authorization for Project Annual Budget Foundation Sprint 1. Year-end closing, carry-forward execution, operational budget cutover, and Program/Portfolio Finance remain outside that authorization.

## Project Annual Budget Foundation Sprint 1 addendum - 2026-10-30

**Status:** Source implementation is in progress. The focused additive EF migration is not applied; Development runtime acceptance remains blocked until the migration passes the database gate and is separately approved for application.

**Scope:** A project-owned annual budget record per Tenant fiscal year, tenant-calendar-derived fiscal boundaries, Project currency, Draft/Approved lifecycle, immutable approval snapshot, rowversion, project/Tenant constraints, and shared `AuditEntries` events. The frontend workspace is inside Project Details → Financial and preserves existing finance views.

**Compatibility (superseded for new requests by the Sprint 2 integration below):** At Sprint 1 delivery, `Project.Budget` continued to control Payment Request reservations. Carry forward remains zero. No historical or fiscal-year-dependent records are reassigned.

**Authorization:** Read and draft write reuse `finances.read` and `finances.write`. Approval uses the new `ProjectAnnualBudget.Approve` permission together with `finances.write`; no role receives it automatically. Initial permission delegation authority is unresolved, so the approval command remains fail-closed and is unavailable until an explicitly authorized grant is made.

**Verification:** Runtime and SQL Server concurrency tests are deferred to consolidated Sprint 7 UAT. See [Project Annual Budget Foundation](PROJECT-ANNUAL-BUDGET-FOUNDATION.md) and the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#project-annual-budget-foundation-security).

## Payment Request Annual Budget integration - 2026-10-30

New request creation associates the request detail with the Project Annual Budget for the Fiscal Year resolved from the server's UTC financial date and authenticated Tenant fiscal calendar. The association stores Annual Budget ID, Fiscal Year, and reference snapshot; a composite Tenant/Project FK prevents cross-scope links. Drafts and pending requests reserve nothing. Submission requires the linked budget to be Approved and to match Tenant, Project, currency, and period. Final approval recalculates active Approved allocations for that same budget under the existing serializable transaction and locks the Project and Annual Budget rows before saving the approval and shared audit events.

The operational formula is `Approved Annual Budget - active Approved Payment Request allocations`. Settlements are not deducted again, full/partial settlement does not release allocations, and no CostEntry or separate reservation ledger is created. `Project.Budget` remains preserved as a historical/legacy ceiling but is no longer the spendable source for new integrated requests; the workspace no longer presents a competing legacy available balance.

Existing requests remain unchanged and unassociated historical approvals are not backfilled. The service blocks new spending transitions when approved legacy requests without an Annual Budget association could cause an unsafe annual balance. No historical FY is inferred. Cross-year pending requests retain their original budget association and approval history; this work adds no automatic transfer, cancellation, carry-forward, or closed-period rules.

Authorization reuses `finances.read` for reads and `finances.write` for Payment Request commands. Creation requires the existing authorized Project Manager condition; a decision additionally requires assignment to that active eligible Project Team approver. Eligible candidates are active Project Members linked to active `Resource.UserId` users who possess `finances.write`. No Payment Request permission keys or role grants are added. Annual Budget approval remains independently protected by `ProjectAnnualBudget.Approve`.

Current source changes are pending additive migration generation, SQL review, and compile verification. No database was accessed or changed. Development identity, migration history, physical schema, and backup readiness remain unverified; no runtime or concurrency result is claimed. All functional, API/database, browser, permission, and concurrency execution is deferred to Sprint 7 UAT.

## Sprint 5 delivery update — 2026-10-30

Sprint 5 has source-level Project Year-End Closing, approval, immutable statement, explicit free balance carry-forward, destination Annual Budget opening, and append-only outstanding-commitment funding transfers. It preserves original requests, settlements, reversals, audit history, and Project-only budget ownership. It adds no parent budget or automatic close. Migration/snapshot reconciliation and builds remain deferred to Sprint 7A; database delivery and runtime UAT remain deferred to Sprint 7B. See [Sprint 5 implementation record](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md).

## Sprint 6 delivery update - 2026-10-30

Sprint 6 adds computed, read-only Program/Portfolio reporting from authorized Project sources. The current hierarchy is deduplicated, currencies remain separate, carry-forward/reversal/current funding and actual payment date dimensions are preserved, and incomplete data or partial scope remains provisional. No parent budget or financial write workflow is introduced. Build verification and UAT remain deferred; no EF or database change was made. See [Sprint 6 implementation record](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md) and [Financial Reporting contract](FINANCIAL-PROGRAM-PORTFOLIO-REPORTING.md).
