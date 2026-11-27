# FOPX PM — Financial Consolidation Implementation Backlog

**Date:** 2026-11-21  
**Status:** Proposed sequence after Product Owner approval of the Sprint 0 blueprint.  
**Rule:** This backlog is a plan only. It does not authorize implementation, database work, permission grants, or UAT.

## 1. Delivery principles

- Implement the approved operating model as one authoritative Project finance lifecycle.
- Keep lifetime ceiling, lifetime reservation, Annual Budget, Budget Line, payment approval, cash settlement, net actual cost, and parent rollups as distinct meanings.
- Reuse the existing approval engine, Annual Budget, funding-transfer chain, PaymentSettlement, audit, permission catalog, project-scope services, and concurrency patterns.
- Add no second approval engine, no duplicate settlement ledger, no automatic sensitive role grants, and no unrelated role or protected-permission policy.
- Register any new permission in PermissionCatalog with accurate EN/AR text and the correct category; enforce it in API service operations, frontend routes/actions, Tenant/Organization/Project boundaries, and Active Deny.
- No Production database writes. Any later Development/UAT migration requires the documented EF workflow, reviewed SQL, verified exact target, explicit authorization, and physical-schema checks.
- Keep browser UAT until consolidated final UAT; Sprint work may add test scenarios but must not claim UAT completion.

## 2. Dependencies and sequence

| Gate / sprint | Theme | Depends on | Exit result |
|---|---|---|---|
| Sprint 0 | Evidence and design | Current source audit and approved Project financial decisions | Five blueprint documents reviewed and approved; no code or schema changes. |
| Sprint 1 | Authorization, contracts, and schema preflight | Sprint 0 approval; dirty working-tree integration review | Permission matrix, lifecycle contracts, migration plan, and current schema target verified. |
| Sprint 2 | Planned Payment and lifetime reservation | Sprint 1 contracts | Registered Plan and installments reserve lifetime budget once with concurrency safety. |
| Sprint 3 | Annual Budget Lines | Sprint 1 schema and funding contracts | Annual-owned hard-cap lines and exact line availability. |
| Sprint 4 | Payment Request integration | Sprints 2–3; approver snapshot policy | Each request links to one installment and one line; full approve/reject/return/resubmit cycles work. |
| Sprint 5 | Settlement and adjustments | Sprint 4 | PM records completed cash payments; immutable settlements and approved linked adjustments. |
| Sprint 6 | Replacement and closing | Sprints 2–5 | Remaining-commitment replacement and year-end carry-forward preserve paid history and reservations. |
| Sprint 7 | Unified reports and UX | Sprints 2–6; formula decisions | One Project workspace and consistent cash-based reports use authoritative sources. |
| Sprint 8 | Reconciliation, Organization cutover, and legacy fence | All prior sprints; verified Development/UAT schema and data inventory | One-time Organization-wide cutover with no unexplained differences; legacy history read-only. |
| Final UAT | Consolidated operational verification | All source, migration, permission, and reporting acceptance complete | Master UAT Checklist passes for approved scope and environments. |

## 3. Backlog

### Sprint 1 — Authorization and implementation contracts

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-001 / P0 | Approve the proposed aggregate, reservation semantics, Budget Line model, approval-cycle history, refund/correction governance, EAC convention, and replacement blockers | Product Owner records a clear decision for each unresolved item in FINANCIAL_DESIGN_GAPS_AND_RISKS.md. No implementation alternative is silently selected. | Signed decision log; document review. |
| FIN-002 / P0 | Fix Payment Request maker-checker enforcement | The requester cannot be assigned to or decide their own request in sequential or parallel approval. Service rejects self-approval even if the user has permission and active team membership. | Positive/negative tests for non-requester assigned approver and self-approval denial; same-Tenant and cross-scope cases. |
| FIN-003 / P0 | Enforce PM eligibility for settlement | Only the current assigned Project Manager can record completed external payments, in addition to PaymentSettlement.Record and resource scope. Program/Portfolio scope alone cannot satisfy PM eligibility. | PM allowed; authorized non-PM project member denied; Program/Portfolio scoped actor denied; Active Deny honored. |
| FIN-004 / P1 | Resolve Project Manager handover for open request drafts | Former manager cannot silently submit as current authority. Define audited transfer, cancellation or rejection of old drafts; new PM can continue only after the defined transfer. | Handover lifecycle and authorization tests. |
| FIN-005 / P1 | Confirm canonical permission matrix and missing operation keys | Reuse semantically matching keys; register new PlannedPayment, AnnualBudgetLine and settlement-adjustment keys only when required. EN/AR labels, accurate category, API policy, frontend checks, Active Deny, and Master UAT scenarios are present. Existing roles receive no automatic grants. | Catalog/route/service contract checks; role-matrix review. |
| FIN-006 / P0 | Verify source-to-model and migration state in an identity-verified Development/UAT gate | Context, assembly, startup, environment, exact server/database identity, migration history, model snapshot and physical schema are recorded. Reconcile dirty/untracked migration sources before generation. | Documented read-only Database Gate and migration/schema report; no Production access. |

### Sprint 2 — Planned Payment and lifetime reservation

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-007 / P1 | Add PlannedPayment and Installment model | One Project-owned Plan has one or more installments; sum equals total; due date and optional same-Project milestone per installment; currency comes from authoritative Project data. | Domain arithmetic, invalid milestone, currency and scope tests. |
| FIN-008 / P0 | Implement explicit registration and lifetime reservation | Draft has no reservation. Registration reserves full amount once. Current ceiling is approved Project baseline plus approved Project Change Requests. Concurrent registrations cannot exceed available budget. Request creation never reserves lifetime again. | Arithmetic, ceiling, idempotency, cross-scope and concurrent registration tests. |
| FIN-009 / P1 | Enforce edit-before-first-request and lock-after-first-request | Registered unrequested Plan may change only by atomic revalidation. After first request, amount/Installments are immutable except through replacement. Preserve audit and concurrency version. | Positive edit, over-limit rejection, locked edit denial and stale-version tests. |
| FIN-010 / P1 | Implement atomic Quick One-off Payment Request creation | One request creates one registered Planned Payment and exactly one Installment and lifetime reservation; replay with same key returns same result; conflicting payload is rejected. | Atomic rollback, idempotent replay, unique constraints and lifetime conservation tests. |

### Sprint 3 — Annual Budget Lines

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-011 / P1 | Add AnnualBudgetLine under one ProjectAnnualBudget | Line belongs to one approved Annual Budget and inherits Project/Tenant/Fiscal Year/currency. Draft line changes do not independently approve. | FK/scope, currency, annual ownership and API contract tests. |
| FIN-012 / P0 | Enforce hard limits and Annual Budget revisions | Pending and Approved requests reserve a line once. Sum of active reservations never exceeds line limit or annual available amount. Increasing limits requires approved Annual Budget Revision. | Boundary, concurrent allocation, approval/revision and negative cross-line tests. |
| FIN-013 / P1 | Add organization/project governance configuration | Authorized governance actor configures lines and approval defaults without granting payment approval or settlement authority. Record changed values, actor, timestamp and reason. | Permission positive/negative tests; audit snapshot checks. |

### Sprint 4 — Payment Request and approval-cycle integration

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-014 / P1 | Link Payment Requests to exactly one Plan Installment and one Annual Budget Line | Server validates same Project, Annual Budget/FY, line and Installment; no multi-line request; partial amount cannot exceed remaining installment. | Relationship, currency, tenant/project boundary and amount tests. |
| FIN-015 / P0 | Implement full approval outcomes | Approve in Full, Reject, Return for Revision. No partial approval. Submission validates annual and line funds; approval revalidates without double-reserving. | State-transition table and race tests. |
| FIN-016 / P0 | Add immutable approval cycles and resubmission | Every submission freezes approval method, assignments, annual/line source, amount, currency and submitter. Return releases annual/line reserve but not Plan lifetime reserve. Resubmission creates a new cycle; previous cycle is unchanged. | Snapshot mutation denial, return/resubmit, concurrent decision and audit tests. |
| FIN-017 / P1 | Apply Annual Budget default approval configuration and exception override | Project Settings preloads defaults. Authorized submission-only override does not change settings and requires reason/audit. Approvers are active and eligible. | Default/override, eligibility, audit, stale configuration and permission tests. |

### Sprint 5 — Settlement, refund and correction

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-018 / P0 | Route PM settlement recording through approved Payment Request | Current PM plus PaymentSettlement.Record; only approved request with active Annual Budget funding; currency, reference, remaining amount and funding transfer validated. Existing settlement and audit save atomically. | Partial/multiple payment, overpay, wrong currency/FY, PM-only, failure rollback and idempotency tests. |
| FIN-019 / P1 | Preserve immutable Settlement history and settle ledger ownership | Target settlement remains request-owned. Legacy obligation rows remain original and unique. No duplicated settlement or XOR owner violation. | Persistence constraints and source-to-target identity reconciliation. |
| FIN-020 / P1 | Add linked refunds/corrections | Original settlement is unchanged. Approved adjustment affects Net Actual Cost only; no automatic lifetime capacity release. Permission, approver, thresholds and evidence follow approved decision. | Positive/negative authorization, amount bounds, idempotency, audit and reporting tests. |

### Sprint 6 — Replacement and Year-End Closing

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-021 / P0 | Implement Replace Remaining Commitment | A guided operation shows paid and outstanding amounts, resolves active requests through authorized workflows, closes/supersedes the original, releases only eligible unpaid Plan reservation, creates linked successor and reserves its revised remaining amount once. No paid amount is rebooked. | 500/200/300 replaced by 400 gives 200 consumed + 400 reserved = 600 exposure; replay/concurrency/rollback/history tests. |
| FIN-022 / P0 | Preserve existing Annual Budget Closing and funding-transfer chain | Transfer only unpaid approved request funding; source and destination budgets/years/currency/closing valid; one active destination; no new lifetime reservation; snapshots immutable. | Carry-forward tests, closure blockers, transfer chain/idempotency and multi-year cases. |
| FIN-023 / P1 | Complete precision mapping/migration reconciliation | Closing monetary fields remain decimal(19,4); allocation percentages remain decimal(7,4); generated SQL and physical schema verified before apply. No data loss in Down path. | Model metadata, migration SQL review, physical schema verification and focused persistence tests. |

### Sprint 7 — Unified reporting and UX

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-024 / P0 | Create one authoritative financial calculation service | Project, status, Program and Portfolio reports use one currency-aware source and formula version. Gross settlements, net adjustments, annual allocation and lifetime exposure remain distinct. No historical snapshots rewritten. | Report parity, owner coverage, currency split, and snapshot preservation tests. |
| FIN-025 / P1 | Implement Cash-Based CPI/CV/EAC | Net settlement-based Actual Cost is used and labeled Cash-Based Performance Indicators. Exact EAC formula is approved, versioned, and handles zero denominators as unavailable. | Arithmetic, zero denominator, date cut-off and label tests. |
| FIN-026 / P1 | Mount authoritative Project Financial workspace | One Project route exposes overview, plans/installments, Annual Budgets/Lines, Payment Requests, settlements/adjustments, closing and audit. No orphan feature or duplicate direct-settlement flow. | Component/API tests and navigation/access review. |
| FIN-027 / P1 | Meet frontend data-integrity and localization rules | Remove hardcoded USD/GBP and fabricated figures; labels are translated EN/AR; RTL amount alignment is verified; loading/empty/error state is clear; permission gating matches backend. | i18n, RTL and frontend permission tests. |

### Sprint 8 — Reconciliation and Organization cutover

| ID / priority | Task | Acceptance criteria | Verification |
|---|---|---|---|
| FIN-028 / P0 | Build versioned dry-run migration and reconciliation tools | Source IDs, target IDs, deterministic rules, before/after totals, exceptions and idempotency are retained. Ambiguous/unowned/missing-currency rows are isolated. | Repeatable dry run, no-write validation, exception and retry tests. |
| FIN-029 / P0 | Reconcile legacy records and reservation totals | Every PaymentObligation, request, settlement, budget/revision, closing/transfer, cost entry and snapshot has a disposition. No amount/status difference is unexplained; no duplicate lifetime/annual reservation exists. | Per-Project/Tenant/year/currency source-to-target reconciliation and authorized finance review. |
| FIN-030 / P0 | Execute one Organization-wide cutover | Freeze all financial mutations in the Organization; resolve in-flight transactions; verify Go gates; switch once; activate one write rail for all Projects; legacy becomes read-only. | Identity-verified deployment report, applied history/physical schema, post-cutover reconciliation and operational sign-off. |
| FIN-031 / P1 | Retire legacy writes only after retention and approval | No premature table or history deletion. Separate approval for retirement; preserve audit and crosswalk. | Legacy route remains read-only; retention and audit evidence. |

## 4. Cross-sprint acceptance checklist

- All financial mutation endpoints enforce service-level permissions, Active Deny, functional eligibility and exact resource scope.
- Organization-owned role behavior remains intact; no new System Role or protected permission is assumed.
- Every monetary mutation has one idempotent, concurrency-safe source of truth and an audit event in the same transaction.
- Amounts use exact decimal arithmetic and authoritative currency; no cross-currency addition or fallback.
- Published snapshots and historical settlement/approval/audit records remain unchanged.
- Annual Budget and Budget Line funding are reserved exactly once. Lifetime reservation is registered at Plan registration and is never duplicated by requests.
- Every operation has positive, negative, self-approval, Active Deny, Tenant/Organization/Project cross-scope, replay, and concurrency verification where applicable.
- Master UAT Checklist is updated; actual Browser UAT is only run in the consolidated UAT phase and reported truthfully.
- No undocumented migration is applied and no Production database write occurs.

## 5. Sprint 0 completion boundary

Sprint 0 ends after the five blueprint documents are delivered for Product Owner review. It does not start Sprint 1, alter code or migrations, grant permissions, execute a database gate/write, run tests/builds, or perform Browser UAT.

