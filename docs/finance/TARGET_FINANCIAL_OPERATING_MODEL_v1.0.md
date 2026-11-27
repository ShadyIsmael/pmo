# FOPX PM — Target Financial Operating Model v1.0

**Date:** 2026-11-21  
**Status:** Approved business rules consolidated; ready for Product Owner review of the proposed technical choices and explicit unresolved items below.  
**Scope:** Project financial planning, annual funding, payment requests, cash settlements, reporting, and organization-wide cutover.

## 1. Authority and evidence boundary

The Sprint 0 task brief and the Product Owner decisions carried in the active project context are authoritative for target behavior. They supersede open alternatives in the earlier source audit. The audit remains useful as a description of earlier source state, not as current truth. The later Sprint 1 integrity report and current source were used to verify present behavior.

This remains a source-based design, with later Sprint 1 Task 01 database evidence recorded in `docs/finance/SPRINT-1-FINANCIAL-INTEGRITY-REPORT.md`: Development identity `Shady/NexusPmo` (database ID 5) is verified; 33 of 34 repository migrations are applied; and the pending precision correction corresponds to two physical `decimal(7,4)` columns where the current model expects `decimal(19,4)`. No row counts, historical balances, or record-level reconciliation were performed. No migration, database write, or UAT was performed; the focused model-mapping tests are documented in the Sprint 1 report.

The approved rules below are binding. Technical mechanisms are recommendations unless explicitly marked as approved. Implementation remains out of scope until the Product Owner accepts this blueprint.

## 2. Approved operating rules

### Budget authority and reservations

1. A Project has one lifetime budget ceiling. Its current approved value is established by its approved baseline and approved Project Change Requests. Annual budgets, budget lines, payment plans, and parent-level rollups do not create or increase this ceiling.
2. Registering a Planned Payment reserves its full amount immediately against the current approved lifetime ceiling.
3. The payment request linked to that Planned Payment does not reserve lifetime budget again.
4. A Project Change Request is the only route to change the lifetime ceiling.
5. Annual funding is independent of the lifetime ceiling and must be approved and secured before a payment is executed.
6. Budget Lines belong to one Annual Budget. Each line is a hard spending limit. Exceeding it requires an approved Annual Budget Revision.
7. Each Payment Request uses exactly one Budget Line. Multi-line requests are unsupported.
8. Budget Lines do not have an independent approval engine. Their authorization follows the Annual Budget and its approved revisions.

### Planned Payments and requests

9. A Planned Payment can contain multiple Installments. Installment amounts must sum exactly to the Planned Payment amount. Each installment has its own due date and optional milestone.
10. One Installment may be funded by one or more Payment Requests. Each request covers one installment and a partial request is allowed, subject to the installment's remaining amount.
11. A Quick One-off Payment Request creates one linked Planned Payment and one Installment as part of the same operation. The lifetime reservation is created exactly once.
12. A Planned Payment becomes financially locked when its first Payment Request is created. Later changes use the approved replacement flow; prior requests, settlements, and audit history remain linked to the original.
13. A replacement changes only the remaining commitment. It preserves amounts already paid, formally resolves active requests, releases only eligible unpaid lifetime reservation, and creates a linked replacement whose new remaining amount is reserved once.
14. Advance payments are ordinary Installments; there is no special Advance Payment type.

### Approval, settlement, and actual cost

15. A Payment Request can be approved in full, rejected, or returned for revision. Partial approvals are not allowed.
16. Pending and approved Payment Requests reserve their amount against both their single Annual Budget and their single Budget Line. Approval changes the request's state; it must not create a second reservation.
17. Return for Revision releases the Annual Budget and Budget Line reservations but preserves the Planned Payment's lifetime reservation. Resubmission revalidates funding and begins a new auditable approval cycle.
18. Payment Settlements are immutable records of completed cash payments made outside FOPX PM. A Project Manager records the completed payment, including partial payments. A settlement must not exceed the approved request's remaining payable amount.
19. Payment Settlements are the authoritative cash-based Actual Paid source. Do not create a duplicate Cost Entry when recording a settlement.
20. Refunds and corrections are separate linked, authorized, audited records. They do not edit or delete the original settlement.
21. A refund or correction does not automatically release Lifetime Budget reservation or consumed capacity.
22. Cash-based CPI, CV, and EAC must be labeled as Cash-Based Performance Indicators. They are not standard incurred-cost EVM.

### Roles, governance, and cutover

23. The Project Manager creates and submits Planned Payments and Payment Requests, and records actual completed payments after the external payment occurs.
24. Assigned Approvers decide Payment Requests. The requester must not approve their own request.
25. There is no separate Finance Operations or payment executor role.
26. Enforce both the applicable functional eligibility and canonical scoped permission, plus Tenant, Organization, Project, and resource boundaries, Active Deny, and audit.
27. Retain organization-owned roles. Do not introduce new System Roles or protected permissions without explicit Product Owner approval. No role is automatically granted new sensitive permissions by this blueprint.
28. Financial cutover is Organization-wide. An Organization switches once, only after successful reconciliation and readiness approval.
29. Historical financial records and published snapshots remain preserved. Unresolved matches are isolated and never guessed.

## 3. Authoritative calculation contract

All formulas operate within one Project and one currency. Do not add across currencies or substitute a default currency. There is no FX conversion in this model.

| Measure | Authoritative definition |
|---|---|
| Current Approved Lifetime Budget (L) | The current effective ceiling from the approved Project budget baseline and approved Project Change Requests. Never derive it by summing Annual Budgets or Budget Lines. |
| Lifetime Consumed (C) | Gross valid Payment Settlement amounts linked to target Plans and deterministically reconciled legacy Project payment records. Approved refunds or corrections affect net cash cost, but do not restore lifetime capacity. Unresolved legacy rows remain reconciliation exceptions and block cutover. |
| Remaining Planned Payment | For each registered Planned Payment, planned amount minus linked valid settlements minus authorized Plan cancellation/replacement releases, floored at zero. An approved reversal of a Payment Request alone does not release the Plan's lifetime reservation. Count it once at the Planned Payment level; do not add its linked Payment Requests again. |
| Lifetime Reserved (R) | Sum of remaining amounts for registered, active Planned Payments. Settlement moves value from reserved to consumed; it does not increase availability. Approved releases remove only the eligible unpaid portion. |
| Available Lifetime Budget | max(0, L − C − R). Registration is rejected if the new full reservation exceeds the available amount. |
| Annual Budget Authorization for fiscal year y | Approved new funding + approved carry-forward + approved adjustments for that Project and fiscal year. Draft or pending amounts are not available. |
| Active Annual Budget Allocations | Pending and approved Payment Request allocations charged to that fiscal year, adjusted for approved reversals and the one active funding-transfer assignment. Draft, rejected, cancelled, and returned requests do not reserve. |
| Annual Budget Available | max(0, approved Annual Budget authorization − active allocations). Settlement does not deduct the allocation a second time. |
| Budget Line Available | max(0, the Line's approved limit − active request allocations assigned to that one Line). A request cannot split its reservation across Lines. |
| Payment Request Reservation | The request amount, assigned to exactly one Annual Budget and one Budget Line. It starts at submission and remains while Pending or Approved, including after partial/full settlement, until rejection, cancellation, return, or approved reversal releases the eligible amount. These events do not release the linked Planned Payment lifetime reservation. |
| Unrequested Installment Amount | max(0, scheduled installment amount − active Payment Request allocations already covering that installment). Pending and Approved requests reserve their requested amount within the installment; settlements remain within their original request and do not make that request amount requestable again. Return, rejection, cancellation, and approved reversal free only the eligible request allocation. |
| Actual Paid (gross) | Sum of valid, confirmed Payment Settlements by settlement date and currency. Approval, planned amount, and due date are not cash paid. |
| Net Actual Cost | Gross valid settlements minus approved, linked refunds and corrections. Preserve the gross cash series separately for audit and cash-flow reporting. |
| Remaining Payable for a request | max(0, approved request amount − valid settlements − approved release for unpaid commitment). |
| Cross-year outstanding funding | Remaining Payable for an approved request assigned to the single active funding transfer destination. Carry-forward transfers funding provenance; it creates no additional Lifetime Budget reservation. |
| Cash-Based CPI | Earned Value divided by net settlement-based Actual Cost, when the denominator is greater than zero. |
| Cash-Based CV | Earned Value minus net settlement-based Actual Cost. |
| Cash-Based EAC | Proposed CPI-only projection: BAC divided by Cash-Based CPI when CPI is greater than zero. The exact EAC convention remains a Product Owner decision before implementation; publish formula/version with every snapshot. |

### Conservation and anti-duplication rules

- At registration, the lifetime equation changes by the full Planned Payment amount once.
- On settlement, remaining lifetime reservation falls by the settled amount while Lifetime Consumed rises by the same amount; Available Lifetime Budget is unchanged.
- A Payment Request never adds a second lifetime reservation.
- The sum of active Payment Request allocations for an Installment cannot exceed its scheduled amount. A partial settlement remains attached to its original request; it does not create capacity for a duplicate request against the same amount.
- A one-off request creates exactly one Planned Payment and one lifetime reservation in the same idempotent transaction.
- Pending-to-approved changes no Annual Budget or Budget Line reservation amount.
- Return for Revision releases annual/line reservation while leaving the registered plan's lifetime reservation active.
- A settlement does not release lifetime capacity; only an authorized Plan cancellation or replacement releases its unpaid lifetime reservation.
- An approved Payment Request reversal releases Annual Budget and Budget Line allocation only. The Plan remains reserved until it is cancelled or replaced.
- A refund changes Net Actual Cost only after approval. It does not restore Lifetime Budget availability.
- A replacement for an original payment with 500 paid 200 and 300 outstanding, replaced by 400, retains 200 consumed, releases the eligible original 300, and reserves 400. Total lifetime exposure becomes 600, a net increase of 100.
- A payment and its request are two views of the same planned exposure, not two additive commitments.
- A cost entry is not created from the settlement; historical Cost Entries remain distinct and are reconciled separately.

## 4. Target lifecycle and transition rules

### Planned Payment

| Transition | Actor and preconditions | Budget and audit effect |
|---|---|---|
| Draft → Registered | Current Project Manager with effective register permission; valid Project, currency, installments, due dates, and lifetime headroom | Atomically create one full lifetime reservation and an audit event. Idempotent retry returns the same result. |
| Registered → Edited | Current Project Manager; no Payment Request has yet been created | Recalculate and atomically replace the reservation amount after validating ceiling availability; retain an audit event. |
| Registered → Locked | First Payment Request is created | Freeze financial terms. Requests begin their independent Annual Budget and Budget Line reservation lifecycle. |
| Locked → Partially Paid / Paid | Approved requests settle; amounts reconcile to installment and request limits | Append settlements. Move reservation to consumed capacity without changing lifetime availability. |
| Locked → Superseded | Replacement completes after active requests are formally resolved | Release only unpaid eligible original reservation; preserve historical requests and settlements; link predecessor and successor. |
| Registered → Cancelled | No request has been created; current Project Manager has cancel permission | Release the full unused reservation and audit reason. |

Recommended implementation status values are Draft, Registered, Locked, Partially Paid, Paid, Cancelled, Closed, and Superseded. Paid/partially paid may be derived from linked settlements; persist only where required for efficient queries and consistency.

### Installment

Installments are children of exactly one Planned Payment. Their immutable scheduled amount, sequence, due date, and optional Project milestone must reconcile to the parent total. A milestone is a reference, not evidence of payment. The recommended display states are Scheduled, Partially Requested, Fully Requested, Partially Paid, Paid, and Released. These are derived from linked requests, settlements, and approved releases; do not maintain an independent approval workflow at the installment level.

### Payment Request and approval cycle

| Transition | Actor and preconditions | Budget and audit effect |
|---|---|---|
| Draft → Pending Approval | Current Project Manager/requester submits a request for one installment and one Budget Line; Annual Budget and Line are approved/open and have headroom | Reserve the request amount against the Annual Budget and Line once; freeze approval plan, approver assignments, amount, currency, fiscal year, line, and submission timestamp. |
| Pending Approval → Approved | Active assigned approver, canonical permission, correct approval order, and requester is not the actor | Revalidate scope and funds; preserve the same reservation; append decision and audit. No partial amount. |
| Pending Approval → Rejected | Active assigned approver with the same controls | Release annual and line reservation; preserve decision history. Lifetime plan reservation remains until plan cancellation/replacement. |
| Pending Approval → Returned for Revision | Active assigned approver with the same controls | Release annual and line reservation; preserve the return reason and frozen cycle snapshot. |
| Returned → Pending Approval | Requester after correction, current PM eligibility, and renewed funding validation | Create a new immutable approval cycle and assignment snapshot; reserve again once. Keep prior cycle unchanged. |
| Draft/Pending → Cancelled | Authorized requester/PM before approval, subject to configured workflow | Release annual and line reservation; record actor and reason. An Approved request requires the authorized unpaid-commitment reversal path. |
| Approved → Partially Paid / Paid | Current Project Manager records an external completed payment | Append one or more settlements. Do not count approval as paid. |
| Approved → Reduced Active Allocation | Approved reversal of unpaid amount only | Release only approved unpaid amount from Annual Budget and Line; preserve settlements. |

An approval cycle is immutable once a decision is recorded. A return/resubmission must create a new cycle snapshot. Reuse the existing approval service and records; do not create a second approval engine.

### Settlement, refund, correction, replacement, and closing

- Settlement: append only; positive amount; immutable; idempotent; same currency as the approved request; must fit remaining payable and preserve the original request and funding-transfer link.
- Refund/correction: create a distinct linked record referencing the original settlement, with amount, type, reason, evidence, actor, approval decision, and audit. No direct settlement edit/delete. Exact approval roles, thresholds, and permission keys are unresolved below.
- Replacement: the Project Manager starts one guided “Replace Remaining Commitment” operation. The system validates and lists outstanding requests. Draft/Pending requests must be formally resolved; an Approved request's unpaid amount must use the approved reversal workflow before replacement can commit. The final close/release/create/reserve operation is one project-locked, idempotent, audited transaction. The original remains immutable and the successor points back to it.
- Year-End Closing: freeze a versioned snapshot; block unsafe legacy associations and unresolved requests/reversals; carry forward only unpaid approved request funding through one append-only transfer chain; retain original request and settlement history; do not reserve lifetime budget again.
- Organization cutover: no project may switch independently. Do not enable the target write rail for any Project until the entire Organization passes reconciliation and readiness approval.

### Complete transition, notification, and recovery contract

Every accepted mutation writes an audit event in the same transaction. The domain events below are proposed notification triggers; delivery and recipient resolution remain disabled until the Project Owner approves the recipient/timezone policy. No user or role is inferred from a broad administrative scope. Failed commands leave no partial reservation or audit mutation; idempotent retries return the original successful result.

| State transition | Actor, permission, preconditions | Budget effect | Audit and notification event | Reversal or recovery |
|---|---|---|---|---|
| Planned Payment Draft to Registered | Current Project Manager; proposed PlannedPayment.Register; valid Plan, Installments, Project currency and sufficient lifetime headroom | Create the full lifetime reservation once | PlannedPaymentRegistered; proposed notice to configured Project financial recipients | Atomic rollback if any row fails; same idempotency key returns same Plan. |
| Registered to Edited | Current Project Manager; proposed PlannedPayment.EditDraft; no request exists; concurrency token current | Recalculate the Plan reservation; reject if ceiling would be exceeded | PlannedPaymentChanged with actor/reason | Failed edit leaves old terms/reservation. After first request, edit is blocked. |
| Registered to Locked | Current Project Manager; ProjectPaymentRequest.Create; valid one-line request draft linked to an Installment | Lifetime reservation unchanged; Annual/Line reserve starts only on submit | PlannedPaymentLocked / PaymentRequestCreated | Request draft can be cancelled; Plan remains reserved. |
| Registered to Cancelled | Current Project Manager; proposed PlannedPayment.Cancel; no Payment Request or settlement exists | Release the full unused Plan reservation | PlannedPaymentCancelled with reason | Cancellation is terminal; recovery creates a new linked Plan rather than reopening history. |
| Locked to Partially Paid / Paid | Current Project Manager; PaymentSettlement.Record; approved linked request, active funding, correct currency and remaining payable | Decrease unpaid Plan reservation and increase Lifetime Consumed by the same gross cash amount; Annual/Line allocation remains counted | PaymentSettlementRecorded; proposed notice to request owner and configured financial recipients | Settlement is immutable. Correct it only with a separately approved linked Adjustment. |
| Locked to Superseded | Current Project Manager; proposed PlannedPayment.ReplaceRemaining; all active requests resolved and predecessor/successor amounts reconcile | Release only original unpaid Plan reservation; create and reserve successor amount once | PlannedPaymentReplaced with reason, linked IDs and before/after exposure | Entire close/release/create/reserve rolls back on error. After commit, repair via another linked replacement, never deletion. |
| Installment Scheduled to Partially/Fully Requested | Current Project Manager; ProjectPaymentRequest.Submit; request amount within unrequested capacity; Annual Budget and Line available | Reserve the submitted request amount against Annual Budget and one Line; no new lifetime reservation | PaymentRequestSubmitted; proposed notice to frozen assigned approvers | If command fails, no reservation. Returned/rejected/cancelled amount may be requested again after validation. |
| Approval cycle Draft to Pending | Requester is current Project Manager; ProjectPaymentRequest.Submit; approved/open budget, eligible assignments and immutable snapshot | Annual/Line reservation starts once; lifetime reservation remains with Plan | ApprovalCycleSubmitted; proposed notice to each assigned approver | Failed submit rolls back cycle, assignments and reserve together. Resubmit uses a new cycle number and idempotency key. |
| Approval cycle Pending to Approved | Active assigned approver; ProjectPaymentRequest.Approve; correct mode/order, effective permission and requester is not the actor | No reservation delta; approval authorizes payment | PaymentRequestApproved; proposed notice to requester | Decision is immutable. Wrong actor/order or stale state is rejected; reversal is a separate workflow. |
| Approval cycle Pending to Rejected | Active assigned approver; ProjectPaymentRequest.Approve; same assignment and maker-checker controls | Release Annual/Line request reservation; Plan lifetime reservation stays | PaymentRequestRejected with reason; proposed notice to requester and PM | Rejection is terminal for that cycle. Preserve it; any new request uses an explicit new request/cycle. |
| Approval cycle Pending to Returned for Revision | Active assigned approver; ProjectPaymentRequest.Approve; reason required | Release Annual/Line reservation; Plan lifetime reservation stays | PaymentRequestReturned with reason; proposed notice to requester | Original cycle is immutable. Corrected resubmission creates a new cycle and revalidates all funding. |
| Returned to new Pending cycle | Current Project Manager/requester; ProjectPaymentRequest.Submit; edited amount/line/approvers valid; new snapshot | Reserve Annual/Line amount once again; lifetime remains unchanged | PaymentRequestResubmitted; proposed notice to assigned approvers | Failed validation leaves prior Returned cycle unchanged and no active reservation. |
| Draft/Pending request to Cancelled | Authorized current requester/PM; ProjectPaymentRequest.Cancel; not already Approved | Release Annual/Line reservation once; Plan lifetime remains | PaymentRequestCancelled with reason; proposed notice to assigned approvers if pending | Idempotent cancellation; Approved request cannot be cancelled by this route. |
| Settlement Adjustment Draft to Pending Approval | Authorized actor with proposed SettlementAdjustment.Create; original settlement exists; amount/reason/evidence valid | No lifetime or Annual Budget capacity change | SettlementAdjustmentSubmitted; proposed notice to assigned approver | Rejected/invalid command has no effect on original settlement. |
| Settlement Adjustment Pending to Approved/Rejected | Assigned distinct approver with proposed SettlementAdjustment.Approve; requester cannot approve; decision and amount valid | Approved refund/correction changes Net Actual Cost only; never restores lifetime capacity | SettlementAdjustmentApproved/Rejected; proposed notice to requester | Original settlement remains immutable. Further correction is another linked record. |
| Replacement Requested to Awaiting Resolution | Current PM; proposed PlannedPayment.ReplaceRemaining; predecessor locked and remaining amount computed | No release or successor reserve until required Payment Requests resolve | ReplacementStarted with blocker list; proposed notice to impacted requesters/approvers | Cancel the replacement attempt without changing the predecessor; retain audit of abandoned attempt. |
| Awaiting Resolution to Replacement Committed | Current PM; all Draft/Pending requests formally cancelled/returned and all Approved requests formally released through authorized reversal; concurrency and ceiling checks pass | Atomic release of eligible original unpaid Plan reserve and one-time reserve of successor amount | ReplacementCommitted with predecessor/successor, paid history, release and new reserve amounts | Serializable/idempotent command; any failure rolls back all writes; post-commit repair is forward-only. |
| Annual Budget Closing Draft to Pending Approval | Authorized project finance governance actor; ProjectAnnualBudget.Close; current FY, no unsafe requests/reversals, valid snapshot | Freeze funding period for review; no lifetime reservation change | AnnualBudgetClosingSubmitted; proposed notice to assigned closing approvers | Submission retry is idempotent; validation failure leaves Draft unchanged. |
| Annual Budget Closing Pending to Rejected | Assigned approver; ProjectAnnualBudget.CloseApprove; immutable snapshot and correct assignment | No carry-forward transfer; source budget remains open according to policy | AnnualBudgetClosingRejected with reason; proposed notice to requester | Rejected closing is preserved. Corrected close requires a new auditable submission per approved workflow. |
| Annual Budget Closing Pending to Closed | Assigned approver; ProjectAnnualBudget.CloseApprove; all checks pass | Freeze closing snapshot; create one transfer per eligible unpaid approved request; no additional lifetime reservation | AnnualBudgetClosed and FundingTransferCreated; proposed notice to requester/PM | Commit atomically; closed snapshots/transfers are immutable. Recovery uses approved forward correction, not deleting a transfer. |
| Annual Budget Closing Draft/Pending to Cancelled | Authorized initiator; ProjectAnnualBudget.Close; only before final closure | No transfer; release closing-only hold | AnnualBudgetClosingCancelled with reason; proposed notice to assigned approvers | Idempotent; closed period cannot be cancelled through this path. |

Installment progress is derived from these events: Scheduled to Partially/Fully Requested on active request allocation, then Partially Paid/Paid on settlement; rejected, returned, cancelled or reversed request allocation returns capacity for another request. Installments have no separate approval decision or direct settlement mutation.

## 5. Reporting interpretation

1. Keep lifetime ceiling, lifetime reserved/consumed, annual authorization, annual reservation, Budget Line availability, gross paid, and net actual cost as separate measures.
2. Show both gross settlement cash and net actual cost after approved adjustments. Label paid amounts by settlement date and currency.
3. Cash-Based CPI/CV/EAC use the stated settlement-based formula and a stable Earned Value/BAC source. They must not be labeled standard EVM or blended with Cost Entry actuals.
4. Preserve every previously published status-report snapshot exactly. Future reports record the formula version and use the unified settlement source after cutover.
5. Program and Portfolio totals remain read-only Project rollups and grouped by exact currency code. They do not own Project cash or budgets and do not perform FX.
6. Reconcile Cost Entries to settlements explicitly. Do not combine both into one Actual Cost total unless a separate approved recognition and de-duplication rule exists.

## 6. Decisions still needed before implementation

The business rules are settled. These remaining implementation choices must not be silently invented:

1. Confirm the proposed CPI-only EAC formula or choose a different documented Cash-Based EAC convention.
2. Set the settlement-currency precision/rounding policy and supported currency catalog; use the Project's authoritative currency and no fallback meanwhile.
3. Define permissions, approval threshold, and segregation of duties for refunds and settlement corrections.
4. Define how an approved Project Change Request may reduce the lifetime ceiling below current consumed plus reserved exposure; recommended behavior is to block the reduction.
5. Define how a Project Manager handover treats the former manager's Draft/Pending Payment Requests. Current source leaves draft submission with the original requester after handover.
6. Confirm the funding-resolution sequence for active approved requests inside the Replace Remaining Commitment flow; no replacement may silently bypass approval of an existing reversal.
7. Confirm whether stored derived status fields are needed, versus calculating from the append-only source records.
8. Approve notification recipients, channel, timezone, reminder/escalation schedule, and delivery behavior for each financial domain event before enabling delivery. Domain events may be recorded earlier, but no recipient is inferred.

