# FOPX PM — Financial Technical Blueprint v1.0

**Date:** 2026-11-21  
**Status:** Target architecture recommendation for Product Owner review. No implementation authorized in this Sprint 0 task.  
**Repository:** C:\Projects\pmo  
**Scope:** Project Planned Payments, Installments, Annual Budgets and Budget Lines, Payment Requests and approval cycles, settlements, replacement, reporting, authorization, and migration.

## 1. Decision summary

Build one project-owned Planned Payment aggregate with scheduled Installments. A Payment Request points to exactly one Installment and one Annual Budget Line. The request may be for a partial amount of that Installment. The existing shared ApprovalRequest, ApprovalAction, PaymentRequestApproverAssignment, ProjectAnnualBudget, ProjectAnnualBudgetClosing, ProjectPaymentRequestFundingTransfer, and PaymentSettlement mechanisms are reusable foundations.

Keep PaymentSettlement as the authoritative append-only cash record. For target requests, retain its ProjectPaymentRequest owner and resolve its Planned Payment through the request's Installment link. Do not add a parallel settlement ledger. Preserve legacy PaymentObligation-owned settlements as historical records during reconciliation; never set both current owner FKs.

Registering a Planned Payment creates its full Lifetime Budget reservation once. Payment Requests consume Annual Budget and Budget Line capacity but do not create another Lifetime Budget reservation. A return/rejection/cancellation of a request releases Annual Budget and Budget Line reservation while leaving the Planned Payment reservation active. Only an authorized release/closure of the planned commitment changes Lifetime Budget reservation.

This choice matches the approved multi-Installment, single-Budget-Line, reservation, and replacement rules. It is a technical recommendation; the Product Owner must accept the model and unresolved items in the accompanying design-gaps report before implementation.

## 2. Current source architecture and observed behavior

The earlier FINANCIAL-CONSOLIDATION-AUDIT.md is the initial baseline. Current source confirms three partially overlapping areas:

| Area | Current source | Observed behavior and target delta |
|---|---|---|
| Legacy planned payments | PaymentObligation, PaymentObligationService, PaymentObligationController, PaymentSettlement | One amount, due date, optional milestone; no Plan/Installment, Annual Budget, Budget Line, or lifetime reservation. Create saves and audits without reservation. Editable after settlement if the amount remains at least paid. See Domain/PaymentObligation.cs:5–20 and Application/PaymentObligationService.cs:81–166. |
| Approval-based Payment Requests | ApprovalRequest + ProjectPaymentRequestDetail in NEXUS.Api.Finance | Current request is independent of PaymentObligation and has no planned-payment, installment, or Budget Line FK. Draft creation binds current fiscal-year Annual Budget; submission and final approval reserve Annual Budget amount. See Finance/ProjectPaymentRequestWorkflow.cs:391–501, 560–673, 749–797 and Domain/ProjectPaymentRequestDetail.cs:5–50. |
| Annual funding | ProjectAnnualBudget and workflow | New approved funding, approved carry-forward, and approved adjustments form TotalAnnualBudget. Pending and Approved requests reserve; Draft and terminal requests do not. No target Budget Line hard-limit model exists. See Domain/ProjectAnnualBudget.cs:16–49 and Application/PaymentRequestWorkflowPolicy.cs:151–158. |
| Legacy allocation/cost | Budget, BudgetRevision, CostEntry | Budget rows are not children of ProjectAnnualBudget. BudgetRevision is a separate approval workflow. CostEntry is a separate incurred-cost path with free-text classifications. See Domain/Budget.cs:5–24, Domain/BudgetRevision.cs:5–28, and Application/ProjectEvmDirectCostLookup.cs:7–49. |
| Shared settlement | PaymentSettlement and its EF configuration | Existing shared table supports obligation- or request-owned settlement, exact-one-owner constraint, idempotency, and request reference uniqueness. Request settlements support partial payment. See Domain/PaymentSettlement.cs:5–21 and Configurations/PaymentSettlementConfiguration.cs:11–47. |
| Year-end | ProjectAnnualBudgetClosing and ProjectPaymentRequestFundingTransfer | Existing source provides approved close snapshots and append-oriented funding transfers for Payment Requests. It does not transfer the Planned Payment's Lifetime Budget reservation. See Domain/ProjectPaymentRequestFundingTransfer.cs:5–24 and Finance/ProjectAnnualBudgetClosingWorkflow.cs. |
| Reporting | ProjectFinancialLookup, ProjectReportingService, ProjectStatusReportService, ProgramPortfolioFinancialRollupService | Actual cost/EVM consumers use CostEntry. Project/status settlement reports select PaymentObligation-owned settlements; parent rollups select request-owned settlements. The source is split and can omit request settlements from project reports. See ProjectFinancialLookup.cs:90–120, ProjectEvmDirectCostLookup.cs:7–49, ProjectReportingService.cs:505–515, ProjectStatusReportService.cs:557–582, and ProgramPortfolioFinancialRollupService.cs:219–222. |

### Current endpoints and workflow states

| Subsystem | Current API surface | Current lifecycle |
|---|---|---|
| Legacy Budget allocation | /api/budgets GET/list, GET/{id}, POST, PUT/{id}, DELETE/{id}; BudgetController.cs:9–30 | Budget is editable/deletable subject to service validation. BudgetRevision is Draft → Submitted → Approved/Rejected/Cancelled via /api/budget-revisions and has its own workflow. |
| Cost Entry | /api/costs GET/list, GET/{id}, POST, PUT/{id}, DELETE/{id}; CostController.cs:9–30 | Type and Status are free-text fields; downstream totals apply different filters. |
| Payment Obligation | /api/payment-obligations GET/list, GET/{id}, POST, PUT/{id}, DELETE/{id}, POST/{id}/settlements, GET/{id}/settlements; PaymentObligationController.cs:9–39 | No persisted settlement status. Paid, partially paid, due and overdue are derived from settlement totals and dates. |
| Project Annual Budget | /api/projects/{projectId}/annual-budgets list/create/edit/submit/review/revise/approve/withdraw/delete; approval-configuration GET/PUT; ProjectAnnualBudgetController.cs:11–156 | Annual Budget Draft → review/approval → Approved; approved budget changes use revision. |
| Annual Budget assigned approvals | /api/my-work/annual-budget-approvals list/history/detail; ProjectAnnualBudgetAssignedApprovalsController.cs:9–32 | Assigned reviewer decisions are separate actions against frozen assignment data. |
| Annual Budget closing | /api/projects/{projectId}/annual-budget-closings list/create/submit/decisions/cancel; ProjectAnnualBudgetClosingController.cs:11–65 | Review → PendingApproval → Rejected or Closed; cancellation is explicit. Closing snapshots and transfers are retained. |
| Payment Request | /api/projects/{projectId}/payment-requests list, approvers, milestones, detail, settlements, reversals, create/edit/submit/decisions/cancel; ProjectPaymentRequestController.cs:11–201 | Current request states include Draft, Pending, Approved, Rejected and Cancelled; settlement progress is derived. No ReturnForRevision action exists. |
| Payment Request Settlement | POST /api/projects/{projectId}/payment-requests/{requestId}/settlements; ProjectPaymentRequestController.cs:151–160 | Only approved request, active funding and outstanding balance can settle. Partial settlements are allowed. |
| Payment Request reversal | POST/edit/submit/review/decide/cancel under /api/projects/{projectId}/payment-requests/{requestId}/reversals; ProjectPaymentRequestController.cs:82–148 | Separate approval request; approved amount releases unpaid commitment only. |

### Current authorization policies

| Current subsystem | Current API policy keys | Current enforcement note |
|---|---|---|
| Legacy Budget | Budget.View/Create/Edit/Delete | Controller policy exists; BudgetAuthority caps approved legacy allocation against the lifetime ceiling. |
| Legacy Budget Revision | BudgetRevision.View/Create/EditDraft/SubmitForReview/Approve/Cancel/Delete | Separate revision workflow, distinct from Annual Budget approval. |
| Cost Entry | Cost.View/Create/Edit/Delete | Free-text cost type/status persists; reporting filters are not uniform. |
| Payment Obligation | PaymentObligation.View/Create/Edit/Delete and PaymentObligationSettlement.Record | Controller policy exists, but the service has no corresponding service-level guard and falls back to DemoTenantId on missing tenant context. |
| Payment Request | ProjectPaymentRequest.View/Create/EditDraft/Submit/Approve/Cancel; PaymentSettlement.Record | Canonical controller policies plus workflow scope checks. Current approval is missing requester exclusion; settlement lacks explicit current-PM eligibility. |
| Payment Request reversal | ProjectPaymentReversal.Create/Approve | Separate assigned-approver workflow; releases unpaid commitment only. |
| Annual Budget | ProjectAnnualBudget.View/Create/EditDraft/SubmitForReview/Approve/ReviseApproved/ManageApprovalConfiguration/Delete/Withdraw | Project settings policies also protect approval-configuration reads/writes. |
| Annual Budget closing | ProjectAnnualBudgetClosing.View/Close/CloseApprove | Separate submit/decision/cancel lifecycle. |

### Development database and migration evidence

The updated Sprint 1 Financial Integrity Report records a read-only Development gate verification of `Shady/NexusPmo` (database ID 5), with 33 of 34 repository migrations applied and `20261121123355_CorrectProjectAnnualBudgetClosingDecimalPrecision` pending. The two physical closing allocation columns are `decimal(7,4) NOT NULL` with no default constraints; the current model expects `decimal(19,4)`. No row counts, financial balances, or record-level reconciliation were performed, and no migration was applied.

The worktree has substantial in-flight finance changes: PaymentSettlement, AppDbContext, payment mappings, and the model snapshot are modified; earlier payment lifecycle migration files are deleted in the working tree; several recent finance migrations are untracked. Treat all checked-out source as evidence about the current working tree only, not the applied Development schema. See docs/finance/SPRINT-1-FINANCIAL-INTEGRITY-REPORT.md §§1–3, 7.

## 3. Recommended target domain model

All finance records are owned by one Project and carry or inherit verified Tenant and Organization scope. Tenant/project relationships use composite keys or equivalent database-enforced scope. Do not authorize by display name, role name, client-supplied Tenant, Project, Annual Budget, or Budget Line identifiers.

| Entity | Core fields / relationship | Invariants |
|---|---|---|
| Project Lifetime Budget Decision | Project ID, amount, currency, effective date, decision type, approved Project Change Request, actor, audit | Current ceiling is the latest effective approved decision. Use existing project/change-request source where possible; do not duplicate current budget authority. |
| PlannedPayment | Project ID, public ID, description/reference/payee reference, currency, total amount, state, registered actor/time, idempotency key/fingerprint, predecessor/successor links, concurrency token | Full amount reserved at registration. Total equals child Installment amounts. Editable before first request only, subject to reservation revalidation. Financially locked after first request. Replacement never rewrites original. |
| PlannedPaymentInstallment | PlannedPayment ID, stable sequence, scheduled amount, due date, optional PlanActivity/Milestone ID, state projection | Exactly one parent. Sum of scheduled amount equals parent amount. Milestone belongs to the same Project and is optional. |
| PlannedPaymentLifetimeReservation | PlannedPayment ID, original reserved amount, released amount, release reason/reference, status/version | One reservation per registered Plan. Create once in the registration transaction. Consumed settlement moves outstanding reserve into consumed capacity; it does not create or release capacity. Release is permitted only by an authorized plan cancel/replacement. |
| ProjectPaymentRequest | Stable business request ID, Project ID, Installment ID, Annual Budget ID/FY, Budget Line ID, amount/currency, requester, state, idempotency key/fingerprint | Exactly one Installment, exactly one Annual Budget, exactly one Budget Line. Sum of active requests cannot exceed unrequested Installment amount or either budget cap. Settled amounts remain inside their original request allocation. Requests never create lifetime reservations. |
| Payment Request Approval Cycle | ProjectPaymentRequest ID, sequential cycle number, ApprovalRequest ID, approval-mode snapshot, approver assignment snapshot, submitted time, result and reason | One immutable snapshot per submission. Existing ApprovalRequest/ApprovalAction and frozen assignment mechanisms are reused; return/resubmit creates a new cycle, never overwrites the old one. |
| AnnualBudget | Existing ProjectAnnualBudget | Remains independent from lifetime ceiling. Preserve funding source, fiscal year, currency, approval/revision/closing history. |
| AnnualBudgetLine | AnnualBudget ID, category/code, description, currency, authorized limit, status, revision/audit reference | One parent Annual Budget. Hard limit is enforced in service and database-safe transaction. No independent approval engine; an approved Annual Budget Revision authorizes limit change. |
| Annual Budget Reservation | Derived from Payment Request lifecycle and approved reversals, optionally materialized for locking/query performance | Pending and Approved requests consume amount once. Return, rejection, cancellation, or approved reversal releases only the eligible amount. A settlement does not subtract the same allocation again. |
| PaymentSettlement | Existing row, with request owner for target workflow; original obligation owner retained for legacy history | Append-only cash fact. Request-owned settlement resolves through request → Installment → PlannedPayment. Preserve XOR owner, idempotency, reference uniqueness, funding transfer, original currency and audit. |
| Settlement Adjustment | New linked refund/correction record: source settlement, type, amount, currency, reason, evidence, requested/approved actors and timestamps, state, idempotency/audit | Compensating record only. Requires explicit authorization and approval rules. Does not edit settlement or return lifetime capacity. |
| Commitment Replacement | Predecessor Plan, successor Plan, amount released, new amount, active request resolutions, actor, reason, idempotency and audit references | Captures remaining-commitment replacement. The final close/release/create/reserve operation is atomic. No paid amount is rebooked. |
| ProjectPaymentRequestFundingTransfer | Existing append-only transfer chain | Reuses source/destination annual budgets, closing ID, amount, supersedes link and idempotency. Transfer amount is unpaid approved request funding only; it never changes Lifetime Budget reservation. |
| Reconciliation Match / Exception | Source system/type/ID, target ID when matched, match rule/version, status, evidence, actor, reason, resolution | Preserve source identifiers and uncertain matches. Unresolved rows stay isolated and block cutover; never infer missing Tenant, currency, Project, milestone, or budget line. |
| Financial Snapshot | Existing published status report or new report snapshot metadata | Preserve prior snapshots exactly. New snapshots record exact currency, cutoff, formula version, source IDs/versions, and calculation outcome. |

### Relationship outline

- Project 1 → many Planned Payments.
- Planned Payment 1 → many Installments.
- Installment 1 → many Payment Requests; a request belongs to exactly one Installment.
- Annual Budget 1 → many Annual Budget Lines.
- Payment Request → exactly one Annual Budget and one Annual Budget Line from that same Annual Budget.
- Payment Request 1 → many immutable approval cycles and zero/many request-owned settlements.
- Payment Settlement 1 → zero/many approved Settlement Adjustments.
- Planned Payment 1 → one lifetime reservation; replacement creates a successor link and a separate reservation.
- Annual Budget Closing 1 → many append-only funding transfers; each transfer has one active destination for a request at a time.

## 4. Workflow, concurrency, and persistence design

### Reuse current infrastructure

Current request workflows already use project-scoped authorization, serializable transactions, Project-row UPDLOCK/HOLDLOCK and Annual Budget locking for financial commands. Request idempotency uses a key plus fingerprint; settlements have tenant/idempotency and request/reference unique indexes; audit rows are written in the business transaction. Reuse these patterns rather than implementing independent locks or a second ledger. Evidence: Finance/ProjectPaymentRequestWorkflow.cs:268–374, 403–495, 567–673, 882–920; ProjectPaymentRequestDetailConfiguration.cs:90–93; PaymentSettlementConfiguration.cs:25–31.

The new reservation command must lock the Project lifetime-ceiling/reservation scope and Annual Budget/Line scope in a consistent order. Create Plan, Installments, reservation, and audit atomically. Registering and one-off creation use idempotency keys plus canonical payload fingerprints. Database constraints enforce one reservation per Plan, one request-to-line, tenant-scoped composite foreign keys, nonnegative amounts, and unique idempotency references.

Request submit/approval locks and rechecks Annual Budget and Line availability. Approval does not write another allocation. Settlement locks the request and funding budget, confirms Approved state, verifies currency, FY, active transfer, and remaining payable, then writes settlement and audit in the same transaction.

Replacement uses a single project-scoped serializable transaction after request preconditions are met. It checks concurrency version, prior paid amount, eligible release, current ceiling, successor amount, and idempotency. Any failure rolls back every close/release/create/reserve/audit write.

### API contract proposal

Retain the current project scope and canonical policy patterns. Proposed routes are illustrative; route names can follow current API conventions:

| Operation | Proposed endpoint | Behavior |
|---|---|---|
| List/read Plans | GET /api/projects/{projectId}/planned-payments and GET /api/planned-payments/{id} | Project-scoped list and detail with installments, reservations, request/settlement history, currency, remaining commitment and permission-based actions. |
| Draft/register Plan | POST /api/projects/{projectId}/planned-payments, POST /api/planned-payments/{id}/register | Draft data validation first; registration is the explicit reservation event. Validate Project Manager eligibility, canonical permission, exact Project currency and lifetime headroom. |
| Edit Plan / installment | PUT /api/planned-payments/{id} | Draft or Registered with no Payment Requests only. Lock after first Payment Request. Require concurrency token and idempotency for reservation-changing operations. |
| Replace remaining commitment | POST /api/planned-payments/{id}/replace-remaining | Guided operation; returns structured active-request blockers or atomically commits successor after required request resolutions. Requires reason and idempotency. |
| Create Payment Request | POST /api/projects/{projectId}/payment-requests | Requires one Plan Installment, one approved Annual Budget Line, amount within unrequested installment capacity, currency, and an idempotency key. Draft does not reserve annual/line funds. One-off variant atomically creates Plan + one installment + request. |
| Submit / decide / return | Existing POST request submit/decisions, extended with ReturnForRevision | Snapshot approval configuration and ordered assignments. Revalidate annual/line availability. Enforce maker-checker, active team assignment, order/mode and audit. |
| Record Settlement | Existing POST /api/projects/{projectId}/payment-requests/{id}/settlements | Current Project Manager records external completed payment; require PaymentSettlement.Record, approved request, open funding budget, currency match, remaining balance, idempotency and audit. Never accept raw Plan or Budget identifiers without server-side scope validation. |
| Refund/correction | POST /api/settlements/{id}/adjustments and approval decision route | Append linked adjustment with reason, evidence, separate permission and approval; do not update/delete original settlement. |
| Annual Budget Lines | Project Annual Budget Line CRUD and revision endpoints | Authorized governance role manages draft lines; enforce annual scope and limit. Approved annual revision authorizes cap change. No line-level approval engine. |
| Year-End | Existing annual-budget closing and funding-transfer routes | Preserve existing approvals, snapshot, closing, and transfer chain; update snapshot formulas and prevent duplicate lifetime reservation. |
| Financial summary | Project summary API | Return separate ceiling, reserved/consumed, annual/line availability, gross settlements, net actual, outstanding planned, and cash-based KPI values with exact currency and formula version. |

All mutations validate current effective permission, Active Deny, Tenant/Organization/Project scope, resource ownership, functional eligibility, and concurrency inside the service, not only at the route.

### Annual approval configuration

Project Settings remains authoritative for default Annual Budget approval method, order, and eligible approvers. Submission configuration preloads those defaults. An exceptional per-submission override is allowed only when explicitly authorized, retains the Project Settings unchanged, records who changed what and why, validates approver eligibility, and stores an immutable plan snapshot. This follows the existing annual-budget foundation and current Product Owner decision.

## 5. Permission and role matrix

Permission keys are necessary but do not alone establish functional eligibility. Every operation checks effective canonical permission after Active Deny and verifies the actor's current functional assignment. Existing roles are organization-owned; no hard-coded role-name allowlists or administrative bypasses are permitted.

| Operation | Functional actor | Canonical permission | Additional enforcement |
|---|---|---|---|
| View Project financials | Project user with financial read eligibility | ProjectPaymentRequest.View and any existing specific finance view keys | Tenant/Organization/Project scope; only permitted fields/actions are exposed. |
| Create/edit/register Planned Payment | Current assigned Project Manager | Reuse matching existing ProjectPaymentRequest.Create only if semantics match; otherwise add PlannedPayment.Create/Register/EditDraft in PermissionCatalog with EN/AR metadata | Must match Project.ProjectManagerId or active ProjectManagerAssignment at mutation time; validate current ceiling, currency and line scope. |
| Submit Payment Request | Current assigned Project Manager/requester | ProjectPaymentRequest.Submit | Recheck current PM after handover; validate one Installment and one Line, approved/open Annual Budget, line limit and approval snapshot. |
| Approve/reject/return Payment Request | Active assigned approver | ProjectPaymentRequest.Approve | Active Project Team eligibility, exact pending assignment, order/mode, scope and requester != approver. No self-approval. |
| Cancel Draft/Pending request | Authorized requester/current Project Manager | ProjectPaymentRequest.Cancel | Record reason; release Annual Budget and Line reservation once. An Approved request requires approved reversal. |
| Record completed payment | Current assigned Project Manager | PaymentSettlement.Record | Functional PM check plus permission, approved request, active funding, amount/currency, idempotency and audit. No Finance Operations role. |
| Create/approve reversal | Authorized Project financial actor / assigned approver | ProjectPaymentReversal.Create / ProjectPaymentReversal.Approve | Current source pattern: explicit assignments, separate approver, unpaid portion only, same budget/FY and audit. |
| Create/approve refund or correction | Proposed authorized requester / assigned settlement approver | Add distinct canonical keys if no existing key has matching meaning | Immutable source settlement, maker-checker, amount/currency checks, reason/evidence, Active Deny and audit. No automatic grants. |
| Configure Annual Budget approval defaults | Project/Organization financial governance authority | ProjectAnnualBudget.ManageApprovalConfiguration | Scope to authorized Project/Organization; does not approve an Annual Budget or payment. Changes audited. |
| Create/edit Annual Budget and Lines | Authorized Project/Organization financial governance authority | Existing ProjectAnnualBudget.Create/EditDraft/ReviseApproved and a new AnnualBudgetLine.* key only if no current matching permission exists | Lines cannot exceed approved Annual Budget amount; approved Annual Budget Revision required for increased hard caps. |
| Approve Annual Budget / revision / close | Assigned approver | ProjectAnnualBudget.Approve / CloseApprove / other current exact key | Verify current assignment, maker-checker, fiscal period, state and resource scope. |
| Parent Program/Portfolio finance view | Authorized read-only parent viewer | Existing parent financial view permission | Read-only calculated descendant Project rollup; currency grouped; no parent transaction or budget writes. |

PermissionCatalog.cs currently contains ProjectPaymentRequest.Create/EditDraft/Submit/Cancel/Approve, PaymentSettlement.Record, ProjectPaymentReversal.Create/Approve, and Annual Budget permissions. Program.cs registers their API policies. SeedDataProvider and the checked-in role matrix do not establish grants for request approval, settlement, or reversals. Persisted grants were not queried. Register missing operation keys and EN/AR labels before implementation; do not automatically grant roles. Add positive, negative, maker-checker, handover, and cross-scope cases to docs/quality/MASTER-UAT-CHECKLIST.md.

### Current source security gaps to close

1. Ordinary Payment Request approval checks an active eligible assignee and decision order but does not compare approver ID to request.RequestedBy. The PermissionCatalog description promises separation of duties, but enforcement is absent in ProjectPaymentRequestWorkflow.cs:699–714 and :1468–1483. Enforce requester != approver in the service and add regression coverage.
2. CreateAsync checks that the actor is the current Project Manager at ProjectPaymentRequestWorkflow.cs:394–409 and :1496–1499. SubmitAsync checks original requester but does not recheck current Project Manager at :560–604. Define and enforce handover behavior; reject a former PM or formally transfer/cancel the draft with audit.
3. RecordSettlementAsync checks PaymentSettlement.Record, active actor, project scope, request approval, funding, and balance, but not that the actor is the assigned Project Manager. Management scope can derive from Program/Portfolio context. Add an explicit current PM eligibility check at :246–275 unless the Product Owner changes the approved PM-only rule.
4. Legacy PaymentObligationService has no service-level finance permission guard, falls back to DemoTenantId in CurrentTenant(), and records its settlement audit after saving the business record. Do not expose this legacy write rail during target cutover. Fence it, migrate/reconcile, then make it read-only before any later retirement.

## 6. Frontend and route blueprint

The supported frontend is apps/web-react. Keep one Project finance workspace as the authoritative user entry point and mount the existing annual-budget and Payment Request components after reconciling their contracts. Do not maintain parallel global and project-specific write flows.

Current source routes /projects/:id/financials to ProjectFinanceTab (routes/AppRoutes.tsx:139), which renders the older budget/cost views and Planned Payments link (ProjectFinanceTab.tsx:177 and :310–318). ProjectPaymentRequests, ProjectFinancialWorkspace, and ProjectFinanceOverview have no production callers. PlannedPaymentsPage.tsx has one amount/date form and direct settlement controls, no installments or replacement. Its lifetime preview subtracts both Planned Payments and approved Requests (PlannedPaymentsPage.tsx:112–159, 344–351), double-counting the same exposure under the approved rule. CostsPage.tsx:34 defaults currency to USD without an authoritative source.

Recommended Project workspace:

1. Overview: lifetime ceiling, consumed, reserved and available; Annual Budget and Line availability; gross paid and net actual; outstanding Plans; formula/currency labels.
2. Planned Payments: registration, installment editor, due dates/milestones, reservation preview, request history, replace-remaining action and immutable settlement history.
3. Annual Budgets: fiscal-year funding, approval status, Budget Lines, caps, revisions, default approval settings and closing.
4. Payment Requests: request creation by installment/line, configure approval snapshot, inbox decisions, return/resubmit and funding status.
5. Settlements and adjustments: PM marks external completed payments; partial settlements and history; authorized linked refunds/corrections.
6. Audit: immutable action/history view with actor, timestamps, reason and linked request/settlement/transfer.

Use current SideSheet patterns for bounded edits/actions and retain existing user-approved Finance navigation if feasible. Make Replace Remaining Commitment a simple guided action that shows paid history, current open amount, outstanding requests, funding effects and resulting exposure before the final authorized commit. Avoid showing two controls that both say “settle” on Plan and Request.

Preserve EN/AR strings in authoritative locale catalogs and RTL direction. Current project finance amount columns contain text-right alignment (ProjectFinanceTab.tsx:395); audit them for RTL. Avoid hardcoded USD/GBP defaults and fabricated approved/forecast values; every displayed amount/currency must come from the authoritative API. Permission-aware disabled/hidden actions must not replace backend enforcement.

## 7. Entity disposition

| Current record or capability | Target disposition | Required treatment |
|---|---|---|
| Project.Budget, OriginalProjectBudget and approved Project Change Requests | Reuse / extend | Remain lifetime ceiling source. Validate ceiling changes against active consumed plus reserved exposure. |
| PaymentObligation | Migrate, then read-only legacy; retire later only after approval | Backfill to Planned Payment with one Installment only when references, currency and ownership are verified. Preserve original IDs/crosswalk. |
| Obligation-owned PaymentSettlement | Read-only legacy history; migrate link only when deterministic | Preserve original settlement, owner and audit; do not set both owner FKs or duplicate it. |
| ApprovalRequest and ApprovalAction | Reuse / extend | One approval engine. Add immutable cycle identity and ReturnForRevision/resubmit history. |
| ProjectPaymentRequestDetail | Extend or normalize into business request + approval cycles | Add planned installment and exactly one Budget Line relationship. Keep original request/approval/settlement history. |
| PaymentRequestApproverAssignment | Reuse / extend | Frozen assignment per cycle; enforce requester exclusion and current eligibility. |
| ProjectAnnualBudget | Reuse / extend | Preserve existing annual approvals, revisions, closing and transfer records; add line children and hard limits. |
| ProjectAnnualBudgetClosing and ProjectPaymentRequestFundingTransfer | Reuse / extend | Preserve snapshots and append-only transfer provenance; ensure carried funding does not duplicate lifetime reservation. |
| Budget and BudgetRevision | Reconcile, then read-only legacy or retire later | They are not target Annual Budget Lines and must not run a parallel line approval engine. Preserve values and revision audit. |
| CostEntry and CostCategory | Read-only legacy for cash reporting; future disposition depends on approved incurred-cost policy | Keep separate from settlements. Do not auto-create cost entries. Maintain exact report semantics until migration is reconciled. |
| CostApproval and Invoice | Preserve and isolate; retire only after data inventory and approval | No current operational workflow found. No deletion or inferred mapping. |
| PaymentNotificationDispatch and due-payment notifications | Extend | Derive reminders from the target Plan/Installment remaining amount; deduplicate with idempotent dispatch. Recipient/timezone policy is a gap. |
| Published status reports and financial snapshots | Reuse as immutable history | Never rewrite. Add formula version to future reports and explain definition changes. |
| Legacy direct settlement UI and APIs | Fence, then read-only during transition | No parallel way to settle outside an approved Payment Request after Organization cutover. |

The record dispositions align with docs/finance/FINANCIAL-DATA-PRESERVATION-INVENTORY.md; that inventory is source-only and contains no live database counts.

## 8. Verification strategy (not run in Sprint 0)

No test or UAT was executed for this blueprint. Implementation sprints should reuse existing test infrastructure and add:

| Test group | Required cases |
|---|---|
| Amount and conservation | Installment sum equals Plan; registration reserves once; request does not reserve lifetime again; settlement moves reserve to consumed without changing availability; refund does not restore lifetime capacity. |
| Annual funding and Budget Lines | Draft does not reserve; Pending and Approved reserve exactly once; one request/one line; per-line cap; annual cap; pending concurrent submissions cannot over-allocate; return/reject/cancel/resubmission behavior. |
| Planned Payment lifecycle | Draft edit; register; first-request lock; cancellation before request; replacement after paid and partially settled; 500/200/300 to 400 yields exposure 600; active requests block unsafe replacement. |
| Approval integrity | Full approval only; assigned approver; ordered/parallel rules; self-approval denied; ReturnForRevision releases annual/line reservation but not lifetime; immutable prior cycle; approval plan override audit. |
| Settlement/refund/correction | PM eligibility; partial/multiple settlements; overpayment denial; duplicate idempotent retry; wrong currency/FY/transfer; immutable settlement; approved linked refund/correction; unauthorized adjustment denied. |
| Carry-forward and closing | Close snapshot immutability; block unsafe active requests; transfer only remaining approved unpaid funding; destination/source ownership; transfer idempotency and concurrency; no extra lifetime reservation. |
| Tenant/resource security | Positive and negative permissions; Active Deny; tenant, organization, project, plan, installment, annual budget, line, request and settlement cross-scope; manager handover; assigned reviewer outside normal scope only for the exact request. |
| Migration/reconciliation | Deterministic and ambiguous mapping, orphaned scalar project/milestone IDs, nullable legacy settlement currency, duplicate/exactly-once linkage, legacy approved requests, historical approval and audit preservation, no duplicate reservation. |
| Reporting | All settlement owners included after cutover; CostEntry not double counted; gross vs net paid; currency separation; snapshots unchanged; EVM label and formula version; parent project counted once. |
| UI and localization | EN/AR, RTL amount columns, keyboard/validation, loading/empty/error states, permissions, one-off, installments, return/resubmit, replacement blockers, partial settlement and adjustment history. |

The Master UAT Checklist must include positive, negative and cross-scope permission scenarios. Browser UAT remains out of Sprint 0.

Reuse the existing API test project areas for CostManagement arithmetic, Finance workflows, Integration/persistence, Security/authorization, and Unit/domain rules. Extend current coverage for Annual Budget reservations, reversal and closing rather than duplicating those tests. Add new focused cases for PlannedPayment/Installment registration, lifetime reservation, Budget Line caps, return/resubmission cycles, adjustments, replacement, manager handover, and Organization cutover. Reuse the existing web-react Vitest/i18n test setup for route, component, permission, and locale coverage; add no tests until an implementation sprint is approved.

## 9. Evidence references

- Earlier source baseline: docs/finance/FINANCIAL-CONSOLIDATION-AUDIT.md.
- Newer integrity and schema-verification boundary: docs/finance/SPRINT-1-FINANCIAL-INTEGRITY-REPORT.md.
- Data preservation: docs/modules/FINANCIAL-DATA-PRESERVATION-INVENTORY.md.
- Project ceiling and Annual Budget lifecycle: docs/modules/FINANCIAL-PROJECT-LIFETIME-CEILING-ANNUAL-BUDGET-LIFECYCLE.md.
- Payment Request and settlement backend: apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs and ProjectPaymentReversalWorkflow.cs.
- Authorization registry and endpoint policies: apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs, apps/api/src/NEXUS.Api/Program.cs and apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs.
- Persistence and migration source: apps/api/src/NEXUS.Persistence/AppDbContext.cs, apps/api/src/NEXUS.Persistence/Configurations/PaymentSettlementConfiguration.cs and apps/api/src/NEXUS.Persistence/Migrations.
- Frontend: apps/web-react/src/routes/AppRoutes.tsx, features/projects/ProjectFinanceTab.tsx, ProjectPaymentRequests.tsx, ProjectFinancialWorkspace.tsx, and features/finance/PlannedPaymentsPage.tsx.

