# Project Lifetime Ceiling and Annual Budget Lifecycle

**Decision/status:** Product Owner approved financial model; source implementation recorded 2026-10-31; Owner runtime UAT pending. This document is the current implementation addendum to the Financial Management target model and Annual Budget Foundation. Historical sprint reports remain historical.

## Approved model

A Project's `Project.Budget` and `Project.Currency` represent its **Total Project Financial Ceiling** across the whole Project lifecycle. It is a Project-owned baseline recorded during Project creation. The current model has one Project budget field rather than a separate estimate and approval state; Project creation therefore retains its existing ceiling-baseline semantics. Subsequent ceiling changes require a completed, authorized Project Change Request. This value is not an Annual Budget and grants no fiscal-year spending authority.

Each `ProjectAnnualBudget` is a separate Project/Fiscal Year allocation in the Project currency. Creating a Project does not create an Annual Budget unless the creator explicitly enters the optional Initial Annual Budget amount. That optional creation flow writes a **Draft** row for the Tenant's current Fiscal Year in the same Project-creation transaction. It does not submit or approve it, and it is not available for Payment Requests.

| Financial concept | Authoritative source | Meaning / inclusion |
|---|---|---|
| Lifetime ceiling | `Project.Budget`, `Project.Currency` | Current Project-wide maximum across fiscal years. Initial baseline is recorded at Project creation; changes take effect only after final Project Change Request approval. |
| Annual allocation | `ProjectAnnualBudget.NewApprovedFunding` and `ApprovedAdjustments` | Proposed while Draft or submitted; only included as authorization after approval. Approved new allocations are counted toward the lifetime ceiling. |
| Carry forward | Approved closing decision and linked `ProjectAnnualBudget` values | Separate annual authorization component. It does not consume or increase the lifetime ceiling a second time. |
| Annual authorized amount | Approved Annual Budget values and approved carry-forward/revisions | Project/Fiscal Year amount used by existing Payment Request funding and closing services. Draft and pending records are excluded. |
| Available annual balance | Existing Project Payment Request budget-availability contract | Server-computed value for its supported current/open fiscal context, after the existing allocation and obligation rules. The UI never recomputes it. |
| Actual settlements | Settlement records and finalized annual closing snapshot | Cash actually paid, reported by actual fiscal date. In Finance summaries, a fiscal-year total is shown only where the existing finalized closing snapshot provides it. |
| Outstanding obligations | Existing financial foundation / Payment Request calculations | Unsettled active payable commitments; never added to paid totals or subtracted twice. |
| Pending ceiling reservation | Submitted Annual Budget review/revision Approval Requests | Submitted initial allocation amount, or only the positive increase proposed by a revision. Drafts do not reserve lifetime ceiling. |

Program and Portfolio financial views remain currency-separated, read-only calculated rollups from Project sources. They do not own ceilings, annual allocations, requests, settlement ledgers, or approvals.

## Annual Budget lifecycle

The row remains in its existing persistence statuses (`Draft` and `Approved`). The shared approval records provide the review lifecycle without adding another status column or approval subsystem:

1. An authorized user with `ProjectAnnualBudget.Create` creates a Draft or `ProjectAnnualBudget.EditDraft` edits one. Project, Fiscal Year, currency, amount scale, non-negative total, and ceiling capacity are checked server-side.
2. A submitter with `ProjectAnnualBudget.SubmitForReview` explicitly submits the immutable Draft snapshot for review. The request stores the proposed amount, currency, submitter, timestamp, approval mode, and explicit assignments. Duplicate pending review is rejected.
3. Reviewers are resolved from active, linked Project Team users with effective `ProjectAnnualBudget.Approve`. The requester cannot review their own submission. Existing sequential/parallel payment approval policy governs ordering and decisions.
4. Approval requires all configured approval conditions. A first-time allocation then changes the Annual Budget from Draft to Approved. A revision updates the approved values only after final approval; the prior approved values remain active during review.
5. Return for Changes requires a reason. It is recorded as an approval action `Returned`; the assignment uses the existing `Rejected` persistence value and the generic Approval Request status is `Returned`. A first-time budget remains Draft. A revision retains its last approved values. The requester needs `ProjectAnnualBudget.EditDraft` to edit a returned initial Draft and `ProjectAnnualBudget.SubmitForReview` to resubmit it; a new revision request requires `ProjectAnnualBudget.ReviseApproved`.
6. All mutation decisions and Annual Budget audit records are written in the same transaction. Approval history is retained through the shared Approval Request/Action/Assignment records and Financial Audit Trail.

### Project defaults and one-submission exceptions

The Project's Annual Budget approval defaults are authoritative. A Project-scoped `ProjectAnnualBudgetApprovalConfiguration` stores the allowed-by-Project method, ordered approvers, and mandatory requirements for this workflow only. The submission panel loads those defaults and labels their source as Project Settings. Missing, invalid, inactive, out-of-scope, or no-longer-eligible approvers block use of defaults with actionable errors. There is no fallback approver or implicit method selection.

An authorized submitter may explicitly choose **Customize for this submission** only with `ProjectAnnualBudget.SubmitForReview` plus `ProjectApproval.Override`, and must give a reason; a revision requires `ProjectAnnualBudget.ReviseApproved` plus `ProjectApproval.Override`. Backend validation still applies the Project's `FinanceApprovalMode`, active linked Project Team eligibility and `ProjectAnnualBudget.Approve` for selected reviewers, mandatory stages, sequential ordering, current rowversions, Project scope, and submitter separation. The exception never writes Project Settings. Each submission re-resolves the current defaults; returning and resubmitting creates a new approval request and plan, while existing approval assignments continue unchanged.

The submitted plan is retained by the immutable audit event (`defaultApprovalPlan`, `effectiveApprovalPlan`, and override actor/time/reason) and the Approval Request's explicit assignment rows. Sequential order is stored on assignments; parallel assignments have no position. The shared approval policy requires every selected mandatory approver to approve. The separate `ProjectApproval.Override` permission is catalogued with EN/AR labels and is excluded from automatic role seeding.

Annual Budget approval is independent of Payment Request approval. An approved request is not a settlement; only a settlement records cash paid. A Payment Request and its resulting settlement are not double-counted as separate budget deductions.

## Ceiling and balance controls

The ceiling check includes all approved new Annual Budget allocations plus approved adjustments, and submitted initial allocations plus positive increases from pending revisions. It excludes carry-forward. Approved and submitted amounts in any currency other than the Project currency block ceiling changes until the records are reconciled; no exchange rate is inferred. A ceiling increase or decrease proposal takes effect only when the governing Project Change Request reaches its final Approved state.

The Project currency cannot be changed after a lifetime ceiling or financial records exist. This protects the denomination of approved ceilings, annual allocations, obligations, requests, settlements, and legacy Project financial records; there is no implicit FX conversion path.

When a lifetime-ceiling decrease is approved, the final Project Change Request decision revalidates the submitted ceiling baseline, current approved annual allocations, pending annual reservations, legacy Budget Line allocations returned by the financial lookup, and actual plus committed obligations. It then changes `Project.Budget` and appends a before/after audit record within the decision transaction. If the validation fails, the Change Request decision is rolled back.

Annual availability remains sourced from the existing Finance API. Draft or submitted budgets are never spendable. Settlements affect paid/outstanding reporting under existing settlement and closing rules; they do not create a second deduction from availability. Closing and carry-forward formulas are unchanged. Closed-year correction/reopening remains governed by the existing closing procedures.

The Finance first-use state distinguishes “No Annual Budget exists” from an Approved Annual Budget of zero. It shows the Project ceiling separately from annual authorization, and offers an Annual Budget creation action only to an authorized user. The availability API returns an authoritative zero for the missing and Draft allocation states, with a distinct status; mismatched, legacy-reconciliation, closed, or otherwise unsupported availability remains unavailable. The Project ceiling is never shown as available-to-spend.

## Permission matrix

| Operation | Existing effective permission and conditions |
|---|---|
| Read Annual Budget records and history | `ProjectAnnualBudget.View`; authenticated Tenant and management Project scope are required. Other financial data retains its own section View permission. |
| Create an Annual Budget Draft | `ProjectAnnualBudget.Create`, mutable context, and authenticated Tenant/Project scope. |
| Edit an Annual Budget Draft | `ProjectAnnualBudget.EditDraft`, mutable context, and authenticated Tenant/Project scope. |
| Submit an Annual Budget Draft for review | `ProjectAnnualBudget.SubmitForReview`, mutable context, and authenticated Tenant/Project scope. |
| Prepare a revision to an approved Annual Budget | `ProjectAnnualBudget.ReviseApproved`, mutable context, and authenticated Tenant/Project scope. |
| Read Project approval defaults and eligible approver choices | `ProjectAnnualBudget.View` + `ProjectSettings.View`; Tenant/Project scope applies. Eligible candidates must be active linked Project Team users with `ProjectAnnualBudget.Approve`. This purpose-specific lookup does not require `Users.View` or `ProjectAnnualBudget.SubmitForReview`. |
| Configure Annual Budget Project defaults | Read requires `ProjectAnnualBudget.View` + `ProjectSettings.View`. Save requires `ProjectAnnualBudget.ManageApprovalConfiguration` + `ProjectSettings.View` + `ProjectSettings.Edit`, mutable context, Tenant/Project scope, current Project/configuration rowversions, and eligible reviewers. `ProjectAnnualBudget.ManageApprovalConfiguration` depends on `ProjectAnnualBudget.View`. |
| Review, approve, or return | `ProjectAnnualBudget.Approve`, an exact pending assignment, active linked Project Team eligibility, current effective permission, approval-policy ordering/state, Tenant/Project scope, and separation from the submitter. The assigned-review route may resolve only the exact assigned Project record. |
| Customize one Annual Budget submission | `ProjectAnnualBudget.SubmitForReview` + `ProjectApproval.Override`; for an approved-budget revision use `ProjectAnnualBudget.ReviseApproved` + `ProjectApproval.Override`. A required reason, current Project/configuration rowversions, eligible distinct mandatory approvers, allowed method/order, scope, and submitter separation still apply. The override affects only that submission. |
| Revise Project ceiling | Existing Change Request management/approval permissions, explicit Project Change Request workflow assignment, multi-step completion rules, and financial ceiling guards. |
| Fiscal closing/carry-forward | `ProjectAnnualBudget.Close`, `ProjectAnnualBudget.CloseApprove`, and `ProjectAnnualBudget.CarryForward`, with their Tenant/Project scope and workflow-state checks. |

`ProjectApproval.Override` is the only new permission in this extension. It is not automatically granted to any role, including the seeded administrator role. The current permission-delegation flow only exposes permissions already held by the delegating actor; an approved initial grant path is therefore still required before an override-authorized UAT actor can be provisioned. The API workflow service enforces the canonical permissions, Tenant/Project scope, eligibility, and assignment checks independently of UI visibility. Source-level checks are documented here; authenticated runtime UAT, persisted grants, and explicit-Deny outcomes remain pending and have not been verified against the Development database.

## Compatibility and reconciliation

No existing record or database object is rewritten. The Project creation request is extended additively with optional `InitialAnnualBudgetAmount`; Annual Budget responses and workflow routes expose the review lifecycle without reinterpreting existing fields. `Project.Budget` retains its existing current approved Project-budget meaning and is not backfilled from Annual Budget rows. Existing Annual Budgets, payment requests, approvals, settlements, reversals, closings, carry-forward, and audits remain in place. Existing Project Change Request records retain their saved `BudgetBeforeChange` and `BudgetChangeAmount`.

Historical financial values whose original meaning cannot be established must be reconciled by an authorized business owner before they are used in a new ceiling or annual-allocation decision. Currency mismatches fail closed. Legacy Budget Lines and cost/commitment records are not deleted or rewritten; they remain separately preserved for existing reporting and obligation guards. No database cleanup is authorized here.

The lifecycle's original Draft/approval foundation uses the existing `ProjectAnnualBudget`, `ApprovalRequest`, `PaymentRequestApproverAssignment`, `ApprovalAction`, and `AuditEntry` persistence. The Project-defaults extension adds two EF entities, and the additive source migration `20261101114229_AddProjectAnnualBudgetApprovalConfiguration` is present. Its applied or pending state in the Development database was not verified in this task; no migration was applied here, and database synchronization is not claimed.

## Owner UAT scenarios

See the `LFC-*` cases in [MASTER-UAT-CHECKLIST.md](../quality/MASTER-UAT-CHECKLIST.md). Runtime UAT has not been performed by source implementation.
