# FOPX PM — Financial Design Gaps and Risks

**Date:** 2026-11-21  
**Status:** Evidence-based Sprint 0 register for Product Owner review. Approved decisions are separated from unresolved choices.  
**Evidence boundary:** Current working-tree source and existing reports only; no Development database or runtime state was verified.

## 1. Approved decisions — not open questions

These decisions are binding and are not repeated as choices in the register:

- A Project lifetime ceiling is separate from Annual Budgets. Approved Project Change Requests are the only way to change the ceiling.
- Registering a Planned Payment reserves its full amount immediately. Its Payment Request must not reserve lifetime budget again.
- Planned Payment has one or more Installments whose amounts reconcile to the parent. Partial requests are allowed against an installment.
- Annual funding is independent and must be secured before execution. Budget Lines are children of Annual Budget, hard caps, and do not have a separate approval engine. One request uses exactly one line.
- Approval outcomes are full approval, rejection, or return for revision. Return releases annual and line reservation but leaves lifetime reservation active; resubmission starts a new auditable cycle.
- Payment Settlements are immutable cash facts. The Project Manager records external completed payments. Refunds/corrections are separate linked authorized records, and they do not automatically release lifetime capacity.
- Cash-based CPI/CV/EAC must be labeled Cash-Based Performance Indicators, not standard incurred-cost EVM.
- The Project Manager creates Plans/Payment Requests and records completed payments. Assigned Approvers approve. There is no Finance Operations or payment executor role. The requester may not approve their own request.
- Keep Organization-owned Roles. No new System Role, protected permission, or automatic sensitive permission grant is authorized.
- Replacement uses the remaining-commitment method: preserve historical paid/requests, formally resolve active requests, release only eligible unpaid lifetime reservation, and create a linked successor with a new reservation exactly once.
- Cutover is one-time and Organization-wide after successful reconciliation and readiness approval. Preserve history and isolate uncertain matches.

Earlier unresolved questions in docs/finance/FINANCIAL-CONSOLIDATION-AUDIT.md about installment semantics, reservation timing, partial approval, and cash-based settlement treatment are superseded by these decisions.

## 2. Current source gaps and risks

| ID / severity | Finding | Evidence and consequence | Required response |
|---|---|---|---|
| R-01 / Critical | No target PlannedPayment or Installment aggregate exists. | PaymentObligation is one amount/date/milestone with no reservation or request link (Domain/PaymentObligation.cs:5–20). PaymentRequestDetail has no Plan, Installment, or BudgetLine FK (Domain/ProjectPaymentRequestDetail.cs:5–50). Immediate reservation and installment reconciliation are not implemented. | Implement the approved aggregate and one lifetime reservation per registered Plan; enforce total/installment conservation and idempotency. |
| R-02 / Critical | Payment Request self-approval is not blocked in the ordinary request workflow. | Submit validates team/permission assignment but does not exclude the requester; Decide and IsAssignedPendingApproverAsync validate assignment/order without requester comparison (Finance/ProjectPaymentRequestWorkflow.cs:601–604, 699–714, 1468–1483). This violates the explicit maker-checker rule. | Enforce requester != approver inside the service and prove positive/negative outcomes. Do not rely on UI or assignment configuration. |
| R-03 / Critical | Settlement actor is not explicitly required to be current Project Manager. | RecordSettlementAsync validates PaymentSettlement.Record, active actor, project scope, request state, funds and balance, but has no current-PM check (Finance/ProjectPaymentRequestWorkflow.cs:246–275). Scope may come from Program/Portfolio context (ManagementScopeResolver.cs:124–125, 151–155, 222–231). | Add the approved functional PM eligibility check at mutation time in addition to permission and scope. |
| R-04 / High | Payment Requests do not implement Return for Revision/resubmission. | Decision contract accepts only Approved/Rejected (CostManagement/Application/DTOs/PaymentRequestDtos.cs:243–249). Annual Budget return does not satisfy the payment-request lifecycle. | Add a ReturnForRevision action, release annual/line reservation once, preserve old cycle, and create a fresh immutable cycle on resubmission. |
| R-05 / High | No Annual Budget-owned hard-cap Budget Line model is present. | Legacy Budget is Project/Portfolio scoped and lacks AnnualBudget relationship; BudgetRevision is a separate approval flow (CostManagement/Domain/Budget.cs:5–24; BudgetRevision.cs:5–28). | Add target line children under Annual Budget and require approved Annual Budget Revision for cap increases. Fence the legacy approval path. |
| R-06 / High | Current reporting reads inconsistent settlement/cost sources. | Project/status reports select PaymentObligation settlements (ProjectReportingService.cs:505–515; ProjectStatusReportService.cs:557–582); program/portfolio reads request-owned settlements (ProgramPortfolioFinancialRollupService.cs:219–222); EVM/ProjectFinancialLookup use CostEntry (ProjectEvmDirectCostLookup.cs:7–49; ProjectFinancialLookup.cs:90–120). | Unify new reports on settlement facts for cash performance; retain CostEntry as distinct until recognition policy is separately approved; preserve published snapshots. |
| R-07 / High | Current navigation exposes separate and conflicting finance surfaces. | Project route mounts ProjectFinanceTab while request/annual-budget components have no production callers (AppRoutes.tsx:139; ProjectFinanceWorkspace.tsx:18–47). PlannedPaymentsPage settles directly against PaymentObligation and subtracts Plan and requests from the same lifetime metric (PlannedPaymentsPage.tsx:112–159, 344–351, 558–610). | Use one Project finance workspace, one settlement route through approved requests, and one lifetime exposure calculation. Retire or fence duplicate legacy writes. |
| R-08 / High | Development migration/schema synchronization remains incomplete. | The 2026-11-21 read-only gate verified `Shady/NexusPmo` (database ID 5), 33 applied of 34 repository migrations, and one pending precision migration. Current physical schema has exactly the two closing-column precision differences; row counts and financial balances were not queried. | Preserve the verified target and history. Do not claim synchronized or deployed until the pending correction is separately authorized, applied, and the physical schema rechecked. |
| R-09 / High | Closing precision correction is source-ready but not deployed. | Current source maps both monetary fields as (19,4); Development stores them as decimal(7,4) NOT NULL with no defaults. The corrective migration is the sole pending migration. Target SQL was reviewed with SHA-256 `5AC262122EEAE08331200113479E8448149AF608CBAA62738D50F19028A646BE`; no database write or financial row-value read occurred. | Apply only through the approved identity-verified workflow when separately authorized. Do not narrow fields through Down unless stored values are proven to fit. |
| R-10 / High | Existing PaymentObligation write path has weaker service safeguards. | Service has no FinancePermissionGuard, falls back to DemoTenantId in CurrentTenant(), and writes settlement before its audit call (PaymentObligationService.cs:14–37, 249–277, 317). | Fence legacy writes before cutover; preserve source data and add no new target dependency on this service. |
| R-11 / High | Annual reservation and lifetime reservation are different ledgers but current UI/model overlap. | Annual request allocation counts Pending and Approved requests. Planned Payment has no reservation; target rule requires full lifetime reserve at registration. Existing UI subtracts both planned and approved request values from the same budget. | Define two separate formulas and data contracts; test transition conservation and displayed totals. |
| R-12 / High | Financial approval permissions/role grants are not established by source grants. | PermissionCatalog and Program policies define canonical keys, but seed role mapping does not grant request approval, PaymentSettlement.Record, or reversals; persisted grants were not read. | Do not infer actual grants or grant automatically. Product Owner/governance selects role grants; publish an explicit matrix and test it. |
| R-13 / Medium | Project Manager handover leaves stale draft authority. | CreateAsync requires current PM (ProjectPaymentRequestWorkflow.cs:394–409, 1496–1499), but SubmitAsync checks the original requester without rechecking current PM (:560–604). | Define audited draft transfer/cancellation and recheck PM at submit. |
| R-14 / Medium | PaymentSettlement currency is nullable for legacy rows. | Shared settlement record stores nullable Currency; report says some historical obligation settlements have no persisted currency. | Preserve null; never infer from today's Project/obligation currency. Isolate for reconciliation and keep unknown rows out of cross-currency totals. |
| R-15 / Medium | The one-off and replacement workflows are not present in active finance UI/API. | Existing PlannedPaymentsPage has no installment, replacement, or Quick One-off request interface; request UI is orphaned. | Implement as single guided flows with blocker presentation and idempotent backend commands. |
| R-16 / Medium | Cost Entry and cash settlement are currently both described as Actual-like data. | Legacy reporting uses CostEntry while settlement reports use PaymentSettlement; combining them would duplicate or misstate amounts. | Keep separate fields and labels; do not auto-create CostEntry from a settlement. Decide future incurred-cost recognition independently. |
| R-17 / Medium | New refund/correction record and controls are not defined in current source. | No Settlement Adjustment entity or workflow was found. Approved rule requires separate linked records but not exact approval roles/thresholds. | Resolve the remaining authorization/approval decision before implementation; use an append-only linked adjustment and no direct settlement mutation. |

## 3. Remaining decisions and recommended resolutions

| ID | Unresolved decision | Recommended direction | Why it remains open |
|---|---|---|---|
| D-01 | Cash-Based EAC convention | Use CPI-only EAC = BAC / CPI when CPI > 0; publish formula/version and return unavailable when denominator is zero. | The approved rule identifies settlement-based CPI/CV/EAC but does not select a particular EAC projection formula. |
| D-02 | Currency scale, rounding, and supported codes | Store exact decimals at the existing monetary precision (19,4) and apply explicit currency-specific rounding only at defined payment/report boundaries. Use Project currency; no fallback or FX. | Source has mixed precision and historical rows may lack currency; supported-currency and rounding policy is not approved. |
| D-03 | Refund and correction authority | Require a maker plus a different assigned approver with separate canonical permission, amount cap, evidence, reason, and compensating record. Do not allow settlement owner to self-approve. | Approved rule says authorized/audited but does not identify eligible roles, thresholds, or permission grants. |
| D-04 | Ceiling decrease below active exposure | Reject the Project Change Request until current consumed plus remaining reservations fit the proposed lower ceiling; never auto-cancel Plans. | The approved ceiling change route is clear, but behavior when the new ceiling falls below existing exposure is not stated. |
| D-05 | Resolve active requests during replacement | Keep one guided replacement operation. Draft/Pending requests must be formally cancelled/returned; Approved requests must use the existing authorized unpaid reversal decision before final replacement commit. Present blockers and do not auto-approve reversals. | The Product Owner approved formal resolution and atomic close/release/create/reserve, but the interaction with separately approved request reversals needs explicit acceptance. |
| D-06 | Project Manager handover | On manager change, transfer eligible Draft/Pending request ownership to the new PM with audit and invalidate former PM mutation rights. Keep approval history unchanged. | Current source allows the original requester to submit after losing PM status. |
| D-07 | Budget Line grouping and uniqueness | Keep lines as explicit scoped allocations; require each request to use one. Add a uniqueness key only after category/code and fiscal-period grouping are approved. | The hard cap and Annual Budget ownership are approved; the category taxonomy/duplicate-line rule is not. |
| D-08 | Persisted derived states | Prefer deriving installment/Plan payment progress from immutable requests/settlements/releases; persist a cached state only if required for query performance and verify it transactionally. | Current Plan states do not exist and the brief specifies behavior, not the storage strategy. |
| D-09 | EVM earned-value/BAC baseline source | Reuse approved scope/schedule baseline for EV/PV/BAC; explicitly map this to the finance report and do not mix settlement AC with cost-entry AC. | Cash-based AC is approved; current EVM direct cost uses CostEntry, and baseline linkage should be confirmed during implementation. |
| D-10 | Financial notification recipients, channel, timezone, and escalation | Emit named domain events and route only to approved assigned approvers/requester/current PM; keep delivery disabled until the recipient and timezone policy is approved. | Existing source sends due/overdue notices for legacy obligations, but the earlier audit found no request/reversal/settlement/closing notices. A new recipient policy must not be inferred. |

No unresolved item reopens the approved multi-installment, immediate lifetime reservation, exact-one Budget Line, no-partial-approval, no-separate-executor, or Organization-wide cutover rules.

## 4. Data and operational risks

### Reservation integrity

Annual authorization, Annual Budget Line limit, lifetime ceiling, lifetime reservation, approved request, settlement cash, and net cost are not interchangeable. Any implementation that counts a request as a second lifetime reservation, subtracts a settlement from annual capacity twice, or releases a refund into lifetime availability can overstate funds. Use separate calculations, one authoritative source per measure, project-level locking, idempotency, and reconciliation invariants.

### Historical integrity

PaymentObligation ProjectId/MilestoneId are scalar IDs; PaymentSettlement has mutually exclusive owner FKs; some legacy settlement currency is null; Budget and CostEntry include free-text states/categories; historical requests may have no annual-budget provenance; reports contain immutable snapshots. Every ambiguous link must remain an exception. Do not infer ownership, currency, line, or state.

### Concurrency and migration

The request workflow already uses serializable transaction/project lock patterns and idempotency. Preserve them for Plan registration, Annual Budget Lines, settlement, replacement, closing, and cutover. Do not rely on unchecked RowVersion assumptions on legacy PaymentObligation. Reconcile the dirty branch and generated migration SQL before any schema write.

### Product and usability

If users can create Plans on one screen, request payments on another, and directly settle the legacy Plan, the UI will encourage two reservation/settlement paths. A single project workspace, guided replace operation, explicit funding/line balance, and translated/RTL-safe data states are necessary to reduce operational errors.

## 5. Council review

| Review lens | Challenge raised | Review outcome |
|---|---|---|
| Council Chairperson | Do the proposed records produce one authoritative source for every amount and a safe delivery boundary? | Recommend the Planned Payment + Installment aggregate, reuse of the existing approval/settlement mechanisms, and Organization-wide cutover. Do not begin implementation until open D-01 to D-09 and permissions are accepted. |
| Business Analyst / Workflow Integrity Auditor | Can every state be entered and exited without a loop, orphan, hidden reservation, or duplicated request? | Add explicit Register, first-request lock, ReturnForRevision with immutable cycle, PM handover rule, approved reversal path, and replacement blockers. Keep Annual Budget reservation separate from lifetime reservation. |
| Financial Domain Specialist | Does the arithmetic conserve exposure through settlement, reversal, refund, carry-forward, and replacement? | Use lifetime consumed + remaining Plan reservation; request reservation only in Annual Budget/Line; cash actual from settlements; refund affects net cost only; carry-forward moves annual funding and never re-reserves lifetime. |
| Architecture / Security Reviewer | Are existing components reusable, and are actor eligibility and scoped authorization enforced in service transactions? | Reuse the approval engine, PaymentSettlement, Annual Budget, transfer chain and locks. Close self-approval, settlement-PM, Project Manager handover, legacy service, and ungranted-permission gaps before relying on current code. |
| UX Specialist | Can a PM understand which amount is ceiling, reserved, funded, requested, paid, or remaining, including in Arabic RTL? | Use one project workspace, remove direct legacy settlement, expose one currency-aware reservation preview, and show replacement blockers/impact before commit. Correct USD fallback and RTL amount alignment. |
| Data Migration / Reporting Specialist | Can source history be moved without guessing, duplicate cash, or changing old reports? | Use crosswalks, deterministic matches, reconciliation exceptions, immutable snapshots, explicit currency handling, reviewed additive migrations, and one-time Organization freeze/switch. Live data and schema are unverified. |

### Consolidated recommendation

Accept the business model as the target and approve the proposed technical architecture only after resolving D-01 through D-09 and the canonical role-grant matrix. The source gaps are actionable. The database is not ready for a deployment decision: target identity, migration history, physical schema, constraints, and financial records remain unverified.

## 6. Sprint 0 verification record

- Repository accessible and current finance source inspected.
- Existing audit and later integrity report read; stale audit alternatives reconciled against the task's binding decisions.
- Focused read-only domain, backend/persistence, frontend/UX, security/permission, and migration/reporting reviews completed.
- No code, migration, SQL, permission grant, database state, test, build, or Browser UAT was changed or executed.
- Deliverables prepared for Product Owner review: Target Financial Operating Model v1.0, Financial Technical Blueprint v1.0, Financial Migration and Cutover Plan, Financial Implementation Backlog, and this Design Gaps and Risks register.

