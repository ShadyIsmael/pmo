# Financial Module Consolidation Audit — Sprint 0 / Task 01

**Status:** Audit complete — awaiting Product Owner review of the target model.
**Scope:** Read-only audit of Planned Payments, Budget Lines, Cost Entries, Payment Requests, Annual Budgets, settlements, approvals, and reporting in `C:\Projects\pmo`.
**Date:** 2026-11-21
**Restrictions honored:** No code, schema, data, grant, or workflow changes were made. No migrations applied. No database writes. No UAT executed.

---

## 1. Executive summary

The repository currently contains **three partially overlapping financial subsystems** that evolved across sprints:

| Subsystem | Front-end name | Backend anchor | Budget authority | Approval | Actual payment record |
|---|---|---|---|---|---|
| **A. Legacy Cost Management** | "Budget Lines", "Actuals", "Planned Payments" (`/costs/*`) | `Budget`, `BudgetRevision`, `CostEntry`, `PaymentObligation`, `PaymentSettlement`, `PaymentNotificationDispatch` (`NEXUS.Modules.CostManagement`) | `Project.Budget` lifetime ceiling via `BudgetAuthority` | `BudgetRevision` workflow only; obligations have **no approval** | `PaymentSettlement` (obligation-owned) |
| **B. Project Annual Budget + Payment Requests** | Built but **unmounted** (`ProjectFinancialWorkspace`, `ProjectPaymentRequests`) | `ApprovalRequest` + `ProjectPaymentRequestDetail`, `PaymentRequestApproverAssignment`, `ProjectAnnualBudget`, `ProjectAnnualBudgetClosing`, `ProjectPaymentRequestFundingTransfer` (`NEXUS.Api.Finance`) | `ProjectAnnualBudget.TotalAnnualBudget` per fiscal year | Full sequential/parallel approval with frozen assignee snapshots | `PaymentSettlement` (request-owned) |
| **C. Project financial summary / reporting** | Project Finance tab, status reports, EVM | `ProjectFinancialLookup`, `ProjectReportingService`, `ProjectStatusReportService`, `ProjectFinancialFoundationController` | Reads both A and B inconsistently | n/a | Obligation settlements only in most reports |

**Core finding:** Subsystem B already implements most of the target lifecycle — *register → reserve budget → approve → record actual settlements (partial/multiple) → release via reversal* — but it is **not reachable in the UI**, and Subsystem A's "Planned Payments" (`PaymentObligation`) bypasses budget reservation and approval entirely. `PaymentSettlement` is already a shared settlement ledger with an exactly-one-owner check constraint, which is the strongest consolidation asset in the codebase.

**Recommended direction (details in §7):** make the Payment Request workflow the single "payable" pipeline, introduce a thin **Planned Payment** aggregate that groups an optional milestone, a due date, and one or more approved payable amounts/installments settled through `PaymentSettlement`, migrate `PaymentObligation` rows into it, and retire or formally fence the legacy Budget/CostEntry surface.

---

## 2. Task A — Entity & database audit

### 2.1 Entity inventory

#### Subsystem A — Legacy Cost Management (`NEXUS.Modules.CostManagement/Domain/`)

| Entity | Table | Purpose | Key fields | Status fields |
|---|---|---|---|---|
| `Budget` | `Budgets` | **Budget Line** — allocation of the project lifetime ceiling across category/fiscal period | `PlannedAmount`, `ApprovedAmount`, `ActualAmount`, `CommittedAmount`, `ForecastAmount`, `ProjectId?`, `PortfolioId?`, `Category`, `FiscalYear`, `FiscalQuarter?`, `Status` | `Status` free text (default `"Draft"`); `ActualAmount`/`CommittedAmount` **never written by any service** |
| `BudgetRevision` | `BudgetRevisions` | Approval workflow to change a Budget line's `ApprovedAmount` | `PreviousApprovedAmount`, `ProposedApprovedAmount`, `Status` enum `Draft/Submitted/Approved/Rejected/Cancelled` | Enum; maker-checker enforced |
| `CostCategory` | `CostCategories` | Lookup for cost classification | `Name`, `Code?`, `Type`, `IsBillable`, `SortOrder` | — **no service/controller; dead** |
| `CostEntry` | `CostEntries` | **Cost Entry / actual incurred cost** | `Amount`, `TaxAmount?`, `Category`, `SubCategory?`, `Type` (default `"Actual"`), `Status` (default `"Posted"`), `IncurredDate`, `ProjectId?`, `BudgetId?`, `ResourceId?`, `VendorName?`, `Reference?`, `IncurredBy?` | `Type`/`Status` **free text, unvalidated**; consumed by EVM only when `Type=="Actual"` and `Status ∈ {Posted, Verified}` |
| `CostApproval` | `CostApprovals` | Legacy cost-approval record | `RequestedAmount`, `ApprovedAmount?`, `Type`, `Status` | **No service/controller; dead** |
| `Invoice` | `Invoices` | Vendor invoice record | `InvoiceNumber`, `Amount`, `TaxAmount?`, `PurchaseOrderNumber?` (orphan string — PO entity removed), `BudgetId?`, `ProjectId?` | **No service/controller/relationships; dead** |
| `PaymentObligation` | `PaymentObligations` | **"Planned Payment" / Due Payment** | `ProjectId`, `ProjectName` (denormalized), `MilestoneId?` (scalar, no FK), `PaymentReference`, `Purpose`, `Description`, `Amount`, `Currency` (default `"GBP"` — overridden by service), `DueDate`, `Notes?` | **No stored status** — all derived from `Settlements` sums |
| `PaymentSettlement` | `PaymentSettlements` | **Actual payment execution record** — shared by obligations and payment requests | `Amount`, `SettlementDate`, `PaymentReference`, `IdempotencyKey` (unique), `Currency?`, `Description?`, owner FKs: `PaymentObligationId?` XOR `ProjectPaymentRequestId?`, optional `FundingTransferId?` | No status/type field — settlement is an append-only fact |
| `PaymentNotificationDispatch` | `PaymentNotificationDispatches` | Idempotency ledger for due/overdue notifications | `PaymentObligationId`, `UserId`, `NotificationType`, `NotificationDate`, `NotificationId` | — |

#### Subsystem B — Project finance (`NEXUS.Api.Finance` + CostManagement domain)

| Entity | Table | Purpose | Key fields |
|---|---|---|---|
| `ApprovalRequest` (shared, `NEXUS.Modules.Approvals`) | `ApprovalRequests` | Generic approval root reused by finance | `Type` discriminator (`ProjectPaymentRequest`, `ProjectPaymentRequestReversal`, `ProjectAnnualBudgetReview`, `ProjectAnnualBudgetRevision`, `ProjectAnnualBudgetClosing`), `Status` (`Draft/Submitted/Approved/Rejected/Cancelled`), `Amount?`, `Currency?`, `RequestedBy?`, `CurrentApproverId?`, `ApprovalLevel` |
| `ApprovalAction` | `ApprovalActions` | Per-approver decision rows | `ApproverId`, `Action`, `Comments?`, `ApprovalLevel`, `ApproverAssignmentId?` |
| `ProjectPaymentRequestDetail` | `ProjectPaymentRequestDetails` | 1:1 extension of `ApprovalRequest` (PK = `ApprovalRequestId`) | `ProjectId`, `PlanActivityId?` (**milestone link**), `ProjectAnnualBudgetId?`, `AnnualBudgetFiscalYear?`, `AnnualBudgetReference?`, `ApprovalMode?` (`Sequential`/`Parallel`, frozen at submit), `RequestKind` (`PaymentRequest`/`ReversalRequest`), `ReversesApprovalRequestId?`, `FundingTransferId?`, `IdempotencyKey?`, `RequestFingerprint?`, `SubmittedAt?`, `CompletedAt?`, `Settlements` nav |
| `PaymentRequestApproverAssignment` | `PaymentRequestApproverAssignments` | Frozen approver snapshot at submit | `ApprovalRequestId`, `ApproverUserId`, `Position?`, `Requirement` (`Mandatory`/`Optional`), `State` (`Pending`/`Approved`/`Rejected`) |
| `ProjectAnnualBudget` | `ProjectAnnualBudgets` | **Annual budget authorization** per project+fiscal year | `FiscalYear`, `FiscalYearStartDate/EndDate`, `FinancialReference`, `Currency`, `NewApprovedFunding`, `ApprovedCarryForward`, `ApprovedReservedCarryForward`, `ApprovedAdjustments`, `Status` (`Draft`/`Approved` only), closing fields, `DraftApprovalConfigurationJson?`; computed `TotalAnnualBudget`, `ApprovedFreeCarryForward` |
| `ProjectAnnualBudgetApprovalConfiguration` + `…Approver` | `…Configurations`, `…Approvers` | Project-level default approval plan for annual budgets | `ApprovalMode`, ordered `ApproverUserId` list |
| `ProjectAnnualBudgetClosing` | `ProjectAnnualBudgetClosings` | Fiscal-year close with frozen financial snapshot | `Status` (`Review/PendingApproval/Rejected/Closed/Cancelled`), `SnapshotJson`, `SnapshotFingerprint`, ~20 monetary measures incl. `OutstandingCommitments`, `ActualSettlementsInFiscalYear`, carry-forward fields, `ApprovalRequestId?` |
| `ProjectPaymentRequestFundingTransfer` | `ProjectPaymentRequestFundingTransfers` | Append-only re-funding of unpaid commitment to next FY | `PaymentRequestId`, `Source/DestinationAnnualBudgetId`, `Source/DestinationFiscalYear`, `ClosingId`, `SupersedesFundingTransferId?`, `Amount`, `IdempotencyKey` |

#### Project-level fields

- `Project.Budget` — lifetime financial ceiling (via `IProjectReferenceLookup.CurrentApprovedProjectBudget`).
- `Project.OriginalProjectBudget` — added by migration `20261121020524_AddProjectOriginalBudget`.
- `Project.FinanceApprovalMode` — `Sequential`/`Parallel`/`Both`/null; gates payment-request approval setup.

### 2.2 Persistence mapping notes

- Only the 9 newer finance entities have dedicated `IEntityTypeConfiguration` classes. `Budget`, `BudgetRevision`, `CostEntry`, `CostCategory`, `CostApproval`, `Invoice` are **convention-mapped** with no explicit indexes/FKs beyond `BudgetRevision→Budget` (Cascade) and optional `CostEntry/CostApproval→Budget`.
- `Invoice` has **zero relationships** — `BudgetId`/`ProjectId` are scalar columns.
- `PaymentObligation.MilestoneId` and `PaymentObligation.ProjectId` are scalar Guids (no FK).
- `ProjectPaymentRequestDetail` uses a **shared primary key** with `ApprovalRequest` (1:1, cascade).
- `PaymentSettlement` has a DB check constraint enforcing **exactly one owner** (`PaymentObligationId` XOR `ProjectPaymentRequestId`) and `FundingTransferId` only with a request owner; unique `(TenantId, IdempotencyKey)` and filtered unique `(TenantId, ProjectId, ProjectPaymentRequestId, PaymentReference)`.

### 2.3 Entity relationship map (evidence-based)

```
Projects (TenantId,Id)
 ├─< ProjectAnnualBudgets ──< ProjectAnnualBudgetClosings (ClosedByClosingId)
 │        ▲                        │ Source & Destination FKs
 │        │                        ▼
 │        └────────────── ProjectPaymentRequestFundingTransfers ──> ProjectAnnualBudgetClosings
 │                                     │ (Supersedes self-chain)
 ├─< ProjectAnnualBudgetApprovalConfigurations ──< …Approvers ──> Users
 ├─< ProjectPaymentRequestDetails ═══(shared PK)═══ ApprovalRequests
 │        │ 1:1                            ▲                │
 │        ├─> ProjectAnnualBudgets         │                └─< ApprovalActions ──> PaymentRequestApproverAssignments
 │        ├─> PlanActivities (milestone)   │                     (ApproverAssignmentId)
 │        ├─> ProjectPaymentRequestFundingTransfers (current funding)
 │        ├─> self (ReversesApprovalRequestId)
 │        └─< PaymentSettlements (ProjectPaymentRequestId, Restrict)
 ├─< PaymentObligations ──< PaymentSettlements (PaymentObligationId, Restrict)
 │        └─< PaymentNotificationDispatches
 ├─< PaymentRequestApproverAssignments ──> ApprovalRequests (Cascade), Users
 └─< Budgets ──< BudgetRevisions (Cascade)
            ├─< CostEntries (optional)
            └─< CostApprovals (optional, dead)
 CostEntries.ProjectId / ResourceId / Invoice.* : scalar, no FK
```

### 2.4 Duplicated / overlapping responsibilities

| Overlap | Evidence |
|---|---|
| Two "planned payment" concepts | `PaymentObligation` (due-date-driven, no approval, no budget link) vs `ProjectPaymentRequestDetail` (approval-gated, annual-budget-bound, milestone optional). Neither references the other. |
| Two settlement ledgers | Same `PaymentSettlements` table, mutually exclusive owner FKs. Reporting reads only the obligation side (§5). |
| Two budget authorities | `Budget.ApprovedAmount` lines capped by `Project.Budget` (`BudgetAuthority`) vs `ProjectAnnualBudget.TotalAnnualBudget` consumed by payment requests. They never reconcile. |
| Two "actual cost" definitions | `ProjectFinancialLookup.ActualCost` = Σ **all** `CostEntry.Amount`; EVM = `CostEntry` filtered `Type=="Actual"`, `Status ∈ {Posted,Verified}`, non-labor, `ResourceId` null; status-report snapshot prefers `Budget.ActualAmount` (never maintained). |
| Two approval stacks | `BudgetRevision` uses the WorkflowEngine adapter (`FinanceWorkflowIntegration`); payment requests/annual budgets/closings manipulate `ApprovalRequest` directly in `NEXUS.Api.Finance`. The generic `ApprovalService` inbox is a third, unused-by-finance surface. |
| Dead financial persistence | `Invoice`, `CostApproval`, `CostCategory` have DbSets and audit-scope mappings but zero services/controllers. `PurchaseCommitments` table was dropped by migration `20261121004908_RemovePurchaseCommitmentsFeature`; `Invoice.PurchaseOrderNumber` survives as an orphan string. |

### 2.5 Independently required financial records (must be preserved)

- `PaymentSettlement` rows — immutable cash-outflow facts for **both** rails.
- `ApprovalRequest`/`ApprovalAction`/`PaymentRequestApproverAssignment` — approval history for requests, reversals, annual budgets, closings.
- `ProjectAnnualBudgetClosing` + `ProjectPaymentRequestFundingTransfer` — fiscal-year close audit trail and cross-year funding provenance.
- `AuditEntry` rows (`EntityType` = `ProjectPaymentRequest`, `PaymentSettlement`, `ProjectFinanceConfiguration`, `Budget`, `CostEntry`, `BudgetRevision`, …) — append-only history.
- `PaymentObligation` + settlements — existing planned-payment history.
- `CostEntry` — incurred-cost records feeding EVM/reporting.

---

## 3. Task B — Workflow audit

### 3.1 Current end-to-end flows

#### Flow A1 — Budget Line lifecycle (legacy)

1. **Create/edit/delete** `Budget` via `POST/PUT/DELETE /api/budgets` (`BudgetController`, policies `Budget.*`). `BudgetService` validates currency = project currency and `BudgetAuthority.ValidateAllocationAsync` caps Σ`ApprovedAmount` at `Project.Budget`.
2. **Revision** via `/api/budget-revisions`: Draft → Submitted → Approved/Rejected/Cancelled; maker-checker (approver ≠ requester); approval re-validates ceiling and writes `Budget.ApprovedAmount`.
3. No link to payments, obligations, or annual budgets.

#### Flow A2 — Cost Entry (actuals)

`POST/PUT/DELETE /api/costs` (`Cost.*` policies). Free-text `Type`/`Status`. Downstream consumers: `ProjectFinancialLookup` (sums all), `ProjectEvmDirectCostLookup` (filters `Actual`+`Posted/Verified`+non-labor), status-report snapshots.

#### Flow A3 — Planned Payment / Due Payment (`PaymentObligation`)

1. `POST /api/payment-obligations` (`PaymentObligation.Create`): PM-or-delegated user registers reference, purpose, amount, due date, optional `MilestoneId` (validated active in working plan). **No budget check, no approval.**
2. `PUT/DELETE` with RowVersion-style concurrency (actually `varbinary(max)` token — see §8 risks).
3. `POST /api/payment-obligations/{id}/settlements` (`PaymentObligationSettlement.Record`): partial settlements allowed, `IdempotencyKey` dedupe, amount ≤ outstanding.
4. Hourly `PaymentDueNotificationScheduler` notifies the obligation **creator** on due/overdue dates.
5. Derived statuses: `Not Paid / Partially Paid / Fully Paid`, `Upcoming / Due / Overdue / Settled`.

#### Flow B1 — Annual Budget

`POST /api/projects/{id}/annual-budgets` (`ProjectAnnualBudget.Create`) → edit draft → submit for review → ApprovalRequest-driven review (`ProjectAnnualBudgetReview`) with configured/default approvers → `Approved`. Revisions of approved budgets via `POST …/revisions` (`ProjectAnnualBudgetRevision`). Unique `(TenantId, ProjectId, FiscalYear)`.

#### Flow B2 — Payment Request (the target-shaped flow)

| Step | Endpoint / method | Permission | Effect |
|---|---|---|---|
| Create | `POST …/payment-requests` → `CreateAsync` | `ProjectPaymentRequest.Create` + **must be current Project Manager** | Creates `ApprovalRequest` (Draft) + `ProjectPaymentRequestDetail`; requires current-FY `ProjectAnnualBudget` to exist (Draft ok); binds `ProjectAnnualBudgetId/FiscalYear/Reference`; optional milestone `PlanActivityId`; idempotency key + fingerprint |
| Edit | `PUT …/{id}` | `ProjectPaymentRequest.EditDraft` | Draft + requester only |
| Submit | `POST …/{id}/submit` | `ProjectPaymentRequest.Submit` | Validates approval setup vs `Project.FinanceApprovalMode`, approver eligibility (active project team + `.Approve`), budget approved/open/current-FY, `amount ≤ TotalAnnualBudget − activeAllocations` (**Pending+Approved both reserve**); freezes `ApprovalMode` + `PaymentRequestApproverAssignment` rows; status → `Pending` |
| Decide | `POST …/{id}/decisions` | `ProjectPaymentRequest.Approve` + assigned reviewer | Sequential: position-ordered; Parallel: mandatory approvers decide; any rejection → `Rejected`; final approval re-validates budget availability → `Approved` + audit `BudgetReserved` |
| Cancel | `POST …/{id}/cancel` | `ProjectPaymentRequest.Cancel` | Requester only; Draft/Pending only |
| Settle | `POST …/{id}/settlements` | `PaymentSettlement.Record` | Serializable tx + project row lock; only `Approved` requests; partial amounts allowed; idempotency key; per-request reference uniqueness; amount ≤ `RemainingPayable`; funding budget must be approved/open/same-FY/currency |
| Reverse | `POST …/{id}/reversals` … full sub-workflow | `ProjectPaymentReversal.Create/.Approve` | New `ReversalRequest` approval; releases only unpaid commitment (`MaximumReversibleAmount = original − settled − released`) against the *current* funding budget |

**State machine (actual):**

```
Draft ──submit──> Pending ──all approvers approve──> Approved ──(settlements accrue)──> partially/fully settled (computed)
  │                  │                                    │
  │                  └──any reject──> Rejected          └──approved reversal(s)──> effective allocation reduced
  └──cancel──> Cancelled (Draft/Pending only; Approved cannot be cancelled — reversal required)
```

`PaymentRequestStatuses.Reversed` exists in the DTO constants but **is never assigned** — reversal is a separate request kind, not a status.

#### Flow B3 — Fiscal-year closing

`POST …/annual-budget-closings` (`ProjectAnnualBudget.Close`) → submit → `CloseApprove` decisions → on `Closed`: freezes snapshot, creates one `FundingTransfer` per outstanding approved request (`Amount = RemainingPayable`, supersede-chained), sets destination `ApprovedCarryForward`/`ApprovedReservedCarryForward`, stamps source budget closed. Carry-forward requires `ProjectAnnualBudget.CarryForward`.

### 3.2 RBAC & approval authority

- Payment requests: action keys `ProjectPaymentRequest.{View,Create,EditDraft,Submit,Approve,Cancel}`, `ProjectSettlement.View`, `PaymentSettlement.Record`, `ProjectPaymentReversal.{Create,Approve}`. Create additionally requires **current Project Manager**. Approvers must be active project-team members holding `.Approve`.
- Annual budgets: `ProjectAnnualBudget.*` granular keys + `ProjectAnnualBudgetClosing.*`.
- Obligations: `PaymentObligation.{View,Create,Edit,Delete}`, `PaymentObligationSettlement.Record` — enforced **only at controller policy level**; `PaymentObligationService` has no `FinancePermissionGuard` call (inconsistent with sibling services).
- Budget/cost: `Budget.*`, `BudgetRevision.*`, `Cost.*` via `FinancePermissionGuard` + controller policies.
- **No seeded role** holds `ProjectPaymentRequest.Approve`, `PaymentSettlement.Record`, `ProjectPaymentReversal.*`, `ProjectAnnualBudget.*` action keys — fresh tenants cannot approve payment requests until an admin delegates them (`BootstrapPermissionDelegationPolicy` covers only annual-budget keys).

### 3.3 Audit trail

- Payment requests/reversals/closings write rich `AuditEntry` rows in the same transaction (`Created/Edited/Submitted/ApproversAssigned/ApproverApproved/ApproverRejected/Approved/Rejected/Cancelled/BudgetReserved/BudgetAllocationReleased/ActualSettlementRecorded/SettlementStatusChanged/Reversal*/YearEndClosing*/FiscalYearClosed/…`).
- Obligations use plain `IAuditService.LogAsync`; Budget/Cost/BudgetRevision use `FinancialAuditEntryFactory` (normalized `financialReference/amount/currency`).
- Read surfaces: `ProjectFinancialActivityController` + `ProjectAuditController` (`ProjectFinancialAuditTrail.View`).

### 3.4 Notifications

- Obligations: hourly due/overdue notifications to creator only (`PaymentNotificationDispatch` dedupe).
- Annual budgets: `ProjectAnnualBudgetApprovalNotificationPublisher` notifies actionable assignees (in-app; push/email flags set but `NotificationTypes.Supported` omits the type).
- **Payment requests, reversals, settlements, closings send no notifications.**

---

## 4. Task C — Financial calculation audit

### 4.1 Calculation dependency matrix

| Metric | Formula (source) | Inputs | Consumers |
|---|---|---|---|
| Approved Annual Budget | `NewApprovedFunding + ApprovedCarryForward + ApprovedAdjustments` (`ProjectAnnualBudget.TotalAnnualBudget`) | ProjectAnnualBudget | Submit/approve gates, availability DTO, closing snapshot, rollups |
| Available Annual Budget | `TotalAnnualBudget − activeAllocations` (`GetBudgetAvailabilityAsync`, `GetActiveApprovedAllocationAsync`) | Approved+Pending payment requests net of funding transfers and approved reversal releases | Payment-request submit/approve gates; `ProjectPaymentRequestBudgetAvailabilityDto` |
| Reserved / Active Allocation | `Σ original request amounts on budget + transferIn − transferOut − approvedReversalReleases` (same method, lines 1134–1287) | ApprovalRequests + Details + FundingTransfers + Reversals + Closings | Same as above; closing `approvedAllocations` |
| Outstanding Commitments | `Σ RemainingPayable` over approved requests whose *current* funding budget = closing source (`BuildClosingSnapshotAsync`) | Settlements + reversals + transfers | Closing snapshot → carry-forward reserved amount |
| Remaining Payable (per request) | `original − settled − released` (`GetReversalBalanceAsync`) | PaymentSettlements (request-owned) + approved ReversalRequests | Settlement cap, reversal cap, closing |
| Actual Paid (request rail) | `Σ PaymentSettlements.Amount` for request in FY window | PaymentSettlements | Closing snapshot only |
| Actual Paid (obligation rail) | `Σ obligation settlements` (`ProjectFinancialBalanceCalculator.CalculateActualPaid`) | PaymentObligations + settlements | `financial-foundation` endpoint |
| Actual Paid (reports) | `Σ settlements joined via PaymentObligationId` in period (`ProjectReportingService.GetFinancialSummaryAsync`) | **Obligation-owned settlements only** | Status reports, project reporting, exports |
| Outstanding Planned Payments | `Σ max(0, obligation.Amount − settled)` all-time / due-in-period variants | Obligations | Reporting + status-report snapshots |
| Actual Cost | `Σ CostEntry.Amount` (all types/statuses — `ProjectFinancialLookup`) vs filtered subset (EVM) vs `Budget.ActualAmount`/`project.ActualCost` (status reports) | CostEntries / Budgets | Project overview, EVM, status reports — **three divergent definitions** |
| Budget Utilization | `actualCost / approvedBudget × 100` where approvedBudget = `Project.Budget` | CostEntries + Project.Budget | `ProjectFinancialLookup` → project overview |
| Remaining Project Budget | `Project.Budget − actualCost` | same | same |
| Allocated / Unallocated Budget | `Σ Budget.ApprovedAmount` / `Project.Budget − allocated` | Budgets | `ProjectFinancialLookup`, CostsOverview |
| Cash Forecast | **Does not exist** — nearest: `Budget.ForecastAmount`/`ForecastAtCompletion`/`ForecastVariance` | Budgets | Forecasts page |
| Portfolio/Program rollup | `approvedBudget − effectiveAllocation`; post-close uses frozen closing measures | AnnualBudgets + Closings | `ProgramPortfolioFinancialRollupService` |

### 4.3 Double-counting / overlap findings

1. **Settlements do not reduce annual-budget availability.** `GetActiveApprovedAllocationAsync` counts Approved+Pending request *amounts*; an Approved request fully settled still consumes its full allocation until an approved **reversal** releases the unpaid remainder. Actual spend therefore never frees budget — by design (commitment model), but must be documented as intentional.
2. **Two payable ledgers can coexist.** A project may have `PaymentObligation` payables and approved payment requests simultaneously; obligation payables are invisible to annual-budget math, and request payables are invisible to obligation-based "Actual Paid"/"Outstanding Planned Payments" reports.
3. **Reporting blind spot:** `ProjectReportingService` and `ProjectStatusReportService` join settlements through `PaymentObligationId` only — settlements recorded against approved payment requests (`ProjectPaymentRequestId`) are **excluded** from Actual Paid in reports/status snapshots/exports. **Resolved 2026-11-21 (Sprint 1 Task 02):** all consumers now use the shared `ActualPaidSettlementCalculator` covering both rails; see `SPRINT-1-ACTUAL-PAID-REPORTING-REPORT.md`.
4. **`Budget.ActualAmount`/`CommittedAmount` are write-never fields** yet status-report snapshots prefer them over `CostEntry` sums — snapshots can report stale/zero actuals.
5. **`CostEntry` actual cost is counted three different ways** (§4.1 Actual Cost row).
6. **Pending requests reserve budget** (`ReservesBudget` = Approved|Pending) — correct for over-submission prevention, but "Reserved Amount" and "Committed Amount" are the same number in this model; the target lifecycle's distinct *reservation → approval → payable* stages are collapsed into status on one row.
7. `ProjectFinancialFoundationController` returns `approvedPaymentRequestAllocations`/`availableBudget` as **hardcoded null placeholders** (lines 63–64) — the endpoint does not yet expose the real allocation math.

---

## 5. Task D — Business scenario coverage

| # | Scenario | Current support | Gap / evidence |
|---|---|---|---|
| 1 | Planned payment with/without milestone | ✅ Both rails support optional milestone (`PaymentObligation.MilestoneId`, `ProjectPaymentRequestDetail.PlanActivityId`) | Milestone validated only at create/update; no revalidation if plan/milestone later changes or is deleted |
| 2 | Single payment & multiple planned installments | ⚠️ Partial | Obligations support multiple *settlements* (partial payments) but there is **no installment entity** — one obligation = one amount; one payment request = one approved amount. No scheduled-installment schedule exists anywhere |
| 3 | Registration immediately reserving budget | ❌ for obligations / ✅ for requests | `PaymentObligation` create does no budget check; `PaymentRequest` reserves at **submit** (Pending counts), not at draft registration |
| 4 | Submit complete amount or specific installment for approval | ❌ | Approval is all-or-nothing per request; no partial-amount approval (`ApprovalRequest.Amount` is the request amount; no approved-amount field on the request path — `CostApproval.ApprovedAmount` exists but is dead) |
| 5 | Partial approval, rejection, cancellation, resubmission | ⚠️ | Rejection is terminal (must create new request); cancel only pre-approval; **no resubmission of a rejected request**; no partial approval |
| 6 | Approval without payment execution | ✅ | `Approved` status persists indefinitely; settlements optional |
| 7 | Full and partial settlements | ✅ | `PaymentSettlement` partial amounts, cap = remaining payable, per-request unique payment reference |
| 8 | Multiple settlements against one approved amount | ✅ | Supported with idempotency keys, serializable tx, project row lock |
| 9 | Failed, reversed, or refunded payments | ⚠️ | Reversal releases **commitment** (unpaid balance) via a second approval — it is not a payment refund/void. `PaymentSettlement` has no status/type; a recorded settlement cannot be corrected except soft-delete (no API) or reversal-of-unpaid only. **No failed-payment state** |
| 10 | Contract/PO commitments and invoices before payment | ❌ | `PurchaseCommitments` feature removed (migration `20261121004908`); `Invoice`/`PurchaseOrderNumber` dead; no PO/contract entity in finance |
| 11 | Advance payments and unplanned expenses | ⚠️ | Milestone optional covers "unplanned" loosely; no advance-payment flag, no unplanned-expense workflow distinct from standard request |
| 12 | Cross-fiscal-year reservations and settlements | ✅ | Closing + `FundingTransfer` chain moves unpaid commitment; settlement must fall in the *current funding* FY (enforced) |
| 13 | Budget revisions, insufficient budget, overpayment | ✅ mostly | `BudgetRevision` for budget lines; `…/annual-budgets/{id}/revisions` for annual budgets; insufficient blocked at submit+approve; settlement > remaining payable blocked. Over-**reservation** of annual budget blocked; over-payment of a settled request blocked |
| 14 | Concurrent requests & duplicate settlement prevention | ✅ strong | Serializable transactions, `UPDLOCK/HOLDLOCK` on project + budget rows, idempotency keys + fingerprints, unique constraints, RowVersion on request path |
| 15 | Changes to linked milestones | ⚠️ | Optional link validated at write time only; `milestoneName` resolved live for display; plan changes don't affect the request |
| 16 | Existing financial data & historical approvals | ⚠️ | "Legacy" approved requests without FY association **block all new submissions** until reconciled (`CountLegacyApprovedRequestsAsync` gates); obligations have no migration path into the request model |

**Unresolved policy questions (do not invent rules):** installment scheduling semantics; whether approval can authorize a partial amount; treatment of failed/voided/refunded settlements; whether obligations must reserve budget on registration; relationship between Budget lines and Annual Budgets; advance-payment recognition.

---

## 6. Task E — Target architecture proposals

### Option A — "Planned Payment" aggregate over the existing request pipeline (recommended)

Introduce one new aggregate and demote the rest:

```
PlannedPayment (new)
 ├─ ProjectId, optional MilestoneId (PlanActivity), Payee/Vendor ref?, Purpose, Currency
 ├─ DueDate?, Status: Draft → Submitted → Approved → PartiallyPaid → Paid | Rejected | Cancelled | Closed
 ├─ BudgetLineId? / CostCategoryId? (optional classification)
 ├─< PlannedPaymentInstallment (optional; else single implicit installment = full amount)
 │      Amount, DueDate?, Status
 ├─< PaymentApproval (wraps existing ApprovalRequest+Detail per installment or per payment)
 └─< PaymentSettlement (existing table; gains PlannedPaymentId owner or links via installment)
```

- **Retain:** `ApprovalRequest`/`ApprovalAction`/`PaymentRequestApproverAssignment` (approval engine), `ProjectAnnualBudget` ecosystem, `PaymentSettlement`, `ProjectPaymentRequestFundingTransfer`, `ProjectAnnualBudgetClosing`.
- **Merge/migrate:** `PaymentObligation` → `PlannedPayment` header (map `PaymentReference→reference`, `Purpose`, `DueDate`, `MilestoneId→PlanActivityId`, `Amount→single installment`). Obligation-owned `PaymentSettlement` rows re-point to the new owner (or keep `PaymentObligationId` as a legacy FK during transition).
- **Deprecate:** `PaymentObligation` write APIs after migration; keep table read-only for history.
- **Keep but fence:** `Budget`/`BudgetRevision` as the *allocation-planning* layer (lifetime ceiling distribution), explicitly documented as non-authorizing; `CostEntry` as incurred-cost actuals, optionally gain `PaymentSettlementId`/`PlannedPaymentId` link.
- **Remove (after data check):** `Invoice`, `CostApproval`, `CostCategory` (dead), `PaymentObligation` (post-migration), `PaymentRequestStatuses.Reversed` constant.
- **Reservation rule:** registering a Planned Payment does **not** reserve; submitting an installment for approval creates the `Pending` reservation (existing semantics). Optionally allow "reserve on registration" policy flag — PO decision.
- **Settlement rule:** unchanged — `PaymentSettlement` against approved payable, partial allowed, idempotent.
- **State machine:**

```
PlannedPayment: Draft → Submitted → Approved → PartiallyPaid → FullyPaid
                     → Rejected / Cancelled (pre-approval) / Superseded
Installment/Payable: PendingApproval → Approved → PartiallySettled → Settled | Released(by reversal)
```

- **Due Payments integration:** due/overdue derived from `DueDate` + remaining payable (same computation as today); notifications extended to the new aggregate.
- **RBAC:** reuse `ProjectPaymentRequest.*`, `PaymentSettlement.Record`, `ProjectPaymentReversal.*`; add `PlannedPayment.*` keys only if a distinct create/edit surface is needed (PO decision). Register new keys in `PermissionCatalog` per permission-first rules.
- **Frontend:** mount the existing (currently dead) `ProjectPaymentRequests`/`ProjectFinancialWorkspace` components or rebuild `PlannedPaymentsPage` on the new aggregate; fix `?section=` deep links; add `Budget.*`/`Cost.*` checks in `ProjectFinanceTab`.
- **Migration:** one-way backfill of `PaymentObligations` (+their settlements) into `PlannedPayments`; obligations become read-only. Historical `PaymentSettlements` keep working via preserved owner columns.

### Option B — Extend `PaymentObligation` in place (lighter touch)

Keep `PaymentObligation` as the planned-payment record; add optional `ProjectPaymentRequestId`/approval linkage and budget-reservation fields; route settlement through the existing dual-owner `PaymentSettlement`.

- **Pros:** smallest schema delta; preserves due-date/notification logic; no new aggregate.
- **Cons:** obligation has no approval lifecycle of its own — you'd bolt on `ApprovalRequest` linkage anyway, converging toward Option A with a worse name; milestone link is a scalar without the plan-activity validation path used by requests; two "planned payment" UIs remain unless consolidated anyway.

### Option C — Status-extend `ApprovalRequest` only (minimal)

Treat each Payment Request as the planned payment; add installments as child `ProjectPaymentRequestDetail` rows or a new `PaymentRequestInstallment` table; deprecate `PaymentObligation` entirely and migrate its data into requests.

- **Pros:** reuses the most complete workflow; no new root entity.
- **Cons:** loses the due-date/planning semantics of obligations (requests have `RequiredBy` but no due-date workflow or due notifications); "planned but not yet requested" payments have no home; milestone is on the detail, fine, but payee/purpose modeling is weaker.

### Comparison

| Criterion | A (recommended) | B | C |
|---|---|---|---|
| Matches target lifecycle | ✅ explicit stages | ⚠️ approval bolted on | ⚠️ planning stage homeless |
| Reuses existing engine | ✅ | ✅ | ✅ most |
| Schema churn | Medium (1 new aggregate + backfill) | Low | Medium |
| Preserves obligation history | ✅ via migration | ✅ native | ⚠️ forced into request shape |
| UI consolidation | ✅ single surface | ⚠️ still two | ✅ |

---

## 7. Task F — Migration & risk assessment

### 7.1 Data-migration risks

- **`PaymentSettlements` XOR owner constraint:** a settlement can belong to an obligation *or* a request, never both. Migrating obligations into planned payments that settle via requests requires either re-pointing `PaymentObligationId → new owner` (new column/FK) or keeping a legacy owner column. Any dual-write period must respect the check constraint.
- **Orphaned scalar references:** `PaymentObligation.ProjectId/MilestoneId`, `CostEntry.ProjectId/ResourceId`, `Budget.ProjectId/PortfolioId`, `Invoice.*` have no FKs — backfills must validate referential integrity manually.
- **Legacy unassociated approved requests** already block new submissions (`LegacyReconciliationRequired`); consolidation must include a reconciliation path for them *and* for obligations.
- **`decimal(7,4)` precision bug:** `ProjectAnnualBudgetClosings.ApprovedPaymentRequestAllocations` and `EffectiveActiveAllocations` are `decimal(7,4)` (max ~999.9999) because `AppDbContext.GetDecimalPrecision` name-matching overrides the explicit `(19,4)` configuration. Any consolidation touching closing snapshots must fix this first — it is a live overflow risk.
- **Concurrency tokens:** `PaymentObligation`, `PaymentSettlement`, `PaymentNotificationDispatch` use `IsConcurrencyToken()` on non-store-generated `varbinary(max)` — optimistic concurrency is effectively absent; `Budget`/`BudgetRevision`/`CostEntry`/`Invoice` have no token at all. Migration should not rely on RowVersion for these tables.
- **Free-text statuses:** `Budget.Status`, `CostEntry.Type/Status`, `Invoice.Status`, `CostApproval.Status` are unvalidated strings; EVM depends on the convention `Actual`+`Posted|Verified`. Consolidation should introduce enums/check constraints only after data profiling.
- **`Invoice.PurchaseOrderNumber`** survives the dropped `PurchaseCommitments` table; decide archive vs. drop.
- **Reporting contract risk:** status reports and exports persist snapshot fields (`OutstandingPlannedPaymentsSnapshot`, `ActualPaidSnapshot`, etc.). Changing settlement ownership will change these numbers; snapshots are historical records — do not rewrite them; document the definitional change going forward.

### 7.2 Applied-migration conflicts

All finance migrations through `20261121020524_AddProjectOriginalBudget` are applied-history; new work must be **new migrations only** — no edits to applied migrations. `RemovePurchaseCommitmentsFeature.Down()` recreates `PurchaseCommitments`, so a rollback of that migration is possible but would resurrect a dead feature — avoid.

### 7.3 Rollback considerations

- Keep `PaymentObligations`/`PaymentSettlements` obligation-owner path functional during transition (read-only legacy rail).
- Feature-flag the new planned-payment write path; the existing payment-request endpoints already coexist.
- No destructive drops until PO sign-off and data archival.

### 7.4 Live database inspection

Not required for this audit — all findings are from source. If row counts/referential integrity checks are needed in Sprint 1, use the approved read-only identity-verified Database Gate process; **no writes were performed or are planned in this sprint**.

---

## 8. Additional defects & risks discovered (feed into sprint planning)

| # | Finding | Severity |
|---|---|---|
| 1 | `GET /api/costs/summary` has **no backend endpoint** — `CostsOverviewPage` calls it and will fail | High (broken page) |
| 2 | Payment-request/annual-budget/closing/reversal UI is **entirely unmounted**; notification deep-links (`?section=annualBudgets`) land on the legacy tab | High |
| 3 | No seeded role can approve payment requests / record settlements / approve reversals | High (operational blocker) |
| 4 | `decimal(7,4)` overflow on two closing snapshot columns | High (data integrity) |
| 5 | Reports/status snapshots ignore request-owned settlements → Actual Paid under-reported | High (financial reporting) — **fixed 2026-11-21**, see `SPRINT-1-ACTUAL-PAID-REPORTING-REPORT.md` |
| 6 | `CostsPage` hardcodes `currency: "USD"` and fabricates approved/forecast amounts from planned | Medium (data integrity rule violation) |
| 7 | `ProjectFinanceTab` manages Budget/Cost without `Budget.*`/`Cost.*` UI checks | Medium |
| 8 | `PaymentObligationService` lacks service-level permission checks; `CurrentTenant()` falls back to `DemoTenantId` | Medium |
| 9 | `PaymentObligation.Currency` defaults `"GBP"` (service overrides, but DTO/entity default violates no-fallback rule) | Low |
| 10 | `PaymentRequestStatuses.Reversed` never assigned; `financeKeys.commitments` dead query key; retired `finances.read/write` constants linger | Low |
| 11 | Payment requests/reversals/settlements/closings send **no notifications** | Medium |
| 12 | `ProjectFinancialFoundation` returns null placeholders for allocation/availability | Medium |

---

## 9. Product Owner decisions required

1. **Target aggregate:** approve Option A (new `PlannedPayment` aggregate) vs. B/C.
2. **Installments:** are installments first-class scheduled records (amount + due date each), or is "one approved amount settled by many payments" sufficient?
3. **Partial approval:** may an approver approve less than the requested amount (requires `ApprovedAmount` on the request path)?
4. **Reservation timing:** reserve at registration (draft) or at submission (current Pending semantics)?
5. **Payment failure handling:** do settlements need `Failed/Voided/Refunded` states, or is reversal-of-unpaid-commitment sufficient?
6. **Budget Lines vs Annual Budgets:** keep `Budget`/`BudgetRevision` as a parallel allocation layer, merge into annual budgets, or deprecate?
7. **CostEntry linkage:** should settlements auto-create/link cost entries (incurred cost), and should EVM consume settlements instead of manual entries?
8. **Obligation migration:** backfill all `PaymentObligations` into planned payments, or leave legacy obligations read-only alongside?
9. **RBAC:** which roles receive `ProjectPaymentRequest.Approve`, `PaymentSettlement.Record`, `ProjectPaymentReversal.*`, `ProjectAnnualBudget.*` — and should any be added to protected Admin baseline?
10. **Notifications:** extend due/overdue + approval notifications to the consolidated flow?
11. **PO/invoice scope:** is vendor invoice/PO tracking in scope for the financial module, or permanently out?

---

## 10. Proposed sprint plan (Sprints 1–6)

| Sprint | Theme | Candidate tasks |
|---|---|---|
| **1** | Foundation & correctness | Fix `decimal(7,4)` closing columns; implement or remove `/api/costs/summary`; fix `CostsPage` hardcoded USD/fabricated amounts; add `Budget.*`/`Cost.*` UI checks; decide+document dead entities (`Invoice`, `CostApproval`, `CostCategory`); profile live Dev data for obligations/requests (read-only gate) |
| **2** | Consolidated payable model | Implement `PlannedPayment` aggregate + installments per approved Option; migrate `PaymentObligation` schema (additive: new table + owner column on settlements); state machine + domain tests |
| **3** | Workflow integration | Wire reservation/approval/settlement/reversal onto planned payments; reuse `ApprovalRequest` pipeline; permission keys registration (`PermissionCatalog`, EN/AR labels, Roles & Permissions); backend+frontend enforcement |
| **4** | Data migration & cutover | Backfill obligations → planned payments; settlement re-pointing; legacy reconciliation path for unassociated requests; feature-flag cutover; read-only legacy rail |
| **5** | Reporting unification | Single "actual paid / outstanding payables" calculation across both (now one) rails; update `ProjectReportingService`, `ProjectStatusReportService`, `financial-foundation` (remove null placeholders); status-report snapshot semantics decision; portfolio/program rollup verification |
| **6** | UX consolidation & hardening | Mount/rebuild planned-payments UI; approval inbox entries for payment requests/reversals; notifications; deep-link fixes; UAT checklist updates; deprecation of obligation write APIs; final data-retention/archive decision |

Each sprint must comply with AGENTS.md: permission-first registration, EF migration workflow (reviewed SQL, verified target, no Production), frontend-data-integrity rules, and Master UAT Checklist updates for new permission scenarios.

---

## 11. Evidence index

| Area | Key files |
|---|---|
| Payment request workflow | `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestWorkflow.cs`, `ProjectPaymentReversalWorkflow.cs`, `ProjectPaymentRequestController.cs` |
| Annual budgets & closing | `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs`, `ProjectAnnualBudgetWorkflow.cs`, `ProjectAnnualBudgetClosingWorkflow.cs`, `ProjectAnnualBudgetAssignedApprovalsController.cs` |
| Financial foundation | `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` |
| Domain entities | `apps/api/src/NEXUS.Modules.CostManagement/Domain/*.cs`, `apps/api/src/NEXUS.Modules.Approvals/Domain/*.cs` |
| Services/policies | `apps/api/src/NEXUS.Modules.CostManagement/Application/{BudgetService,BudgetRevisionService,CostService,PaymentObligationService,PaymentRequestWorkflowPolicy,BudgetAuthority,ProjectFinancialLookup,ProjectFinancialBalanceCalculator,FinanceWorkflowIntegration,FinancialAuditEntryFactory,PaymentDueNotificationService,PaymentDueNotificationScheduler,ProjectEvmDirectCostLookup}.cs` |
| Persistence | `apps/api/src/NEXUS.Persistence/{AppDbContext.cs,Configurations/*,Migrations/*,SeedDataProvider.cs}` |
| Permissions | `apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`, `apps/api/src/NEXUS.Api/Program.cs`, `Authorization/FinancialPermissionPolicyRegistration.cs`, `NEXUS.Persistence/SeedDataProvider.cs` |
| Reporting | `apps/api/src/NEXUS.Modules.ProjectManagement/Application/{ProjectReportingService,ProjectStatusReportService}.cs`, `apps/api/src/NEXUS.Api/ProgramManagement/ProgramPortfolioFinancialRollupService.cs` |
| Frontend | `apps/web-react/src/features/finance/*`, `features/projects/{ProjectFinanceTab,ProjectFinancialWorkspace,ProjectPaymentRequests,ProjectAnnualBudgetWorkspace,ProjectAnnualBudgetYearEndClosing,ProjectFinanceOverview,ProjectGovernanceTabs,ProjectSettingsTab}.tsx`, `features/my-work/MyWorkApprovalsPage.tsx`, `api/endpoints.ts`, `features/finance/api/finance.ts`, `features/projects/api/{project-financials,project-annual-budgets}.ts`, `routes/AppRoutes.tsx` |
| Docs | `docs/finance/*.md`, `docs/modules/FINANCIAL-*.md`, `docs/modules/PROJECT-*.md`, `docs/notifications/PROJECT-ANNUAL-BUDGET-APPROVAL-NOTIFICATIONS-20261110.md`, `docs/development/financial-consolidated-technical-closure-backlog.md` |
| Tests | `apps/api/tests/NEXUS.Api.Tests/{CostManagement,Finance,Integration,Security,Unit}/` (see §4 of persistence audit for coverage map) |

*End of audit. Sprint 1 implementation must not begin until the Product Owner approves the target model in §6/§9.*
