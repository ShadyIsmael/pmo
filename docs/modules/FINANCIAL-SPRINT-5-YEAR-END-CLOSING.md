# Financial Sprint 5 — Year-End Closing and Budget Carry Forward

> **Authorization supersession (2026-11-03):** This record preserves the original Sprint 5 decision and implementation notes. `finances.read` and `finances.write` are retired and unsupported by current API/UI authorization. Current permissions are operation-specific; see the [permission model](../architecture/permission-model.md) and [permission audit matrix](../security/permission-audit/ACTION-PERMISSION-MATRIX.csv). Persisted grants and runtime authorization remain unverified.

**Status:** Source implemented; compile verification, EF migration reconciliation, database delivery, and runtime UAT are deferred to Financial Sprint 7A/7B. No migration was generated or applied for Sprint 5, and no database records were changed.

## Scope and ownership

Each Project closes its own Fiscal Year. Programs and Portfolios do not own budgets and are not closed by this workflow. The period and ending-year label come from the authenticated Tenant fiscal calendar and timezone. Closing is an explicit, approval-gated action; reaching a period boundary does not close it automatically.

## Closing lifecycle

`Review → PendingApproval → Closed` or `Rejected`; a preparer may cancel a Review/Pending closing before final approval. The API stores a closing statement snapshot and fingerprint, selected approval mode, approver assignments and decisions through the shared `ApprovalRequest`, `ApprovalAction`, and assignment tables, plus row-version and idempotency fields. Final approval recalculates the statement inside a project-scoped serializable transaction and rejects if the source financial state changed. The preparer cannot approve their own closing. Rejection/cancellation does not close the year or create transfers.

The statement separates:

- Approved Annual Budget, new funding, incoming carry forward, and adjustments.
- Gross approved allocations after source/destination transfers, approved reversal releases, and effective active allocations.
- Cash settlements by actual payment date in the Tenant fiscal period.
- Outstanding payable commitments, free available budget, total unspent budget, proposed free carry forward, reserved commitment carry forward, and non-carried free balance.

`Free Available = Approved Annual Budget − Effective Active Allocations`.
`Total Unspent = Approved Annual Budget − valid cash paid in this fiscal period`.
These are different measures. Settlements do not reduce the allocation a second time. For the illustrative $1,000,000 budget / $200,000 allocation / $30,000 paid case, free available is $800,000, outstanding payable is $170,000, and total unspent is $970,000.

## Carry forward and funding assignment

Free balance carry forward is explicit and may be zero, partial, or full. An approved closing with unresolved pending requests, pending reversals, unknown historical settlement currency, unsafe legacy associations, inconsistent ownership/currency, or over-allocation fails closed. Every outstanding approved request must be explicitly carried to the next Fiscal Year for this MVP; the UI defaults that choice off and the API blocks closing if the commitment is not selected.

`ProjectPaymentRequestFundingTransfer` is an append-only assignment history. Each transfer links the original request, source and destination annual budgets/Fiscal Years, approved closing, amount, currency, and optional superseded transfer. Original request, approval, settlement, reversal, and audit identities remain unchanged. At each point, the active transfer-chain destination (or the original budget when no transfer exists) is the single current funding assignment. The transfer amount is the current unpaid payable amount, not the original approved amount. Later-year transfers chain consecutively; source active allocation retains already-paid amounts while destination reserves only the transferred unpaid amount.

The next-year budget total is `New Funding + Approved Free Carry Forward + Approved Reserved Carry Forward + Approved Adjustments`. The carried reserved portion is included in budget total and also in active allocations, leaving it unavailable for new requests. A draft destination remains non-spendable until separately approved. Existing destination budgets must match the proposed opening values; approved values are not overwritten. Transfers are blocked if the destination lacks enough unallocated approved capacity.

Settlements keep the original request association and their actual payment date. A settlement after carry-forward must use the currently approved, open funding budget and fall within that Tenant Fiscal Year. A reversal after carry-forward releases against the active funding budget only; closed historical budgets are not credited. Cash refunds and reversal of paid settlements remain out of scope.

The legacy Planned Payments / `PaymentObligation` settlement path also resolves the Tenant Fiscal Year from its actual payment date and rejects a new settlement when that Project period is closed. It uses the same serializable transaction and Project-row lock as the close, Payment Request settlement, approval, and reversal commands, so the close cannot race past a legacy payment write. This remains source-level until Sprint 7 runtime verification.

## Permissions

| Action | Permissions | Additional enforcement |
|---|---|---|
| Read statements/history | `ProjectAnnualBudgetClosing.View` | Authenticated Tenant and in-scope Project; no legacy finance alias |
| Read eligible closing approvers | `ProjectAnnualBudget.Close` | Active linked Project Team users with the effective closing-approval permission |
| Prepare, submit, cancel | `ProjectAnnualBudget.Close` | Scoped Project access; preparer owns draft; active eligible approvers selected |
| Approve/reject | `ProjectAnnualBudget.CloseApprove` | Assigned active Project Team member; preparer separation-of-duties and approval-order checks |
| Approve free/reserved carry | `ProjectAnnualBudget.CarryForward` in addition to the relevant close permission | Rechecked at submission and final approval |
| Approve destination Annual Budget | `ProjectAnnualBudget.Approve` | Separate Annual Budget review after a completed source closing opens its Draft |

The keys are in the Permission Catalog and Roles & Permissions labels. No existing role grant is changed or automatically expanded. Initial delegation remains an explicit authorization-policy dependency; without a valid grant, sensitive actions fail closed.

## Audit and source persistence

The workflow writes `AuditEntries` in the same business transaction for preparation, submission, decisions, rejection/cancellation, closing, free/reserved carry approval, funding transfer, and destination opening. Events carry Project/Tenant, source/destination Fiscal Years and budgets, actor/UTC time, currency, financial values, status, and references. The Project Financial Activity UI maps these event types to business labels in EN/AR.

Source model changes include `ProjectAnnualBudgetClosing`, `ProjectPaymentRequestFundingTransfer`, reserved carry/closed metadata on `ProjectAnnualBudget`, the current `FundingTransferId` on request details and settlements, DbSets, and EF configurations with scoped foreign keys, checks, indexes, uniqueness, and row-version constraints. No Designer, migration, or snapshot reconciliation was performed because Sprints 3–5 are assigned to the coordinated Sprint 7A migration owner. Existing snapshot/migration work in the shared tree was left untouched.

## Verification and remaining work

Source was reviewed statically only. No API/frontend build, test suite, browser UAT, EF tooling, migration SQL generation, database connection, deployment, or data modification was performed. Runtime concurrency safety is not claimed. Sprint 7A must reconcile Sprint 3 Settlement, Sprint 4 reversal, and Sprint 5 closing/transfer schema together; the migration owner must review chain and source-parent constraints, filtered indexes, existing-destination capacity behavior, snapshot synchronization, and incremental SQL. Sprint 7B must execute permission, fiscal boundary, transfer/reversal/settlement, concurrency, audit, historical preservation, and EN/AR/RTL UAT.

No Program/Portfolio budgets, automatic closing, refunds, currency conversion, or Sprint 6 rollups were introduced.

## Primary implementation files

- Backend model and persistence source: `ProjectAnnualBudgetClosing`, `ProjectPaymentRequestFundingTransfer`, `ProjectAnnualBudget`, `ProjectPaymentRequestDetail`, `PaymentSettlement`, `AppDbContext`, and their EF configurations.
- API/workflows: `ProjectAnnualBudgetClosingController`, `ProjectAnnualBudgetClosingWorkflow`, `ProjectAnnualBudgetWorkflow`, `ProjectPaymentRequestWorkflow`, `ProjectPaymentReversalWorkflow`, and `PaymentObligationService`.
- Permissions and composition: `SeedDataProvider`, API authorization policies, and the Cost Management reference to the Tenant Fiscal Year service.
- Frontend: `ProjectAnnualBudgetYearEndClosing`, the Project Annual Budget and Financial workspaces, finance API contracts, permission constants, Financial Activity labels, and EN/AR message catalogs.
- Supporting documentation: this record, target business model, Annual Budget Foundation, Payments/Settlements, Reversals, roadmap, closure backlog, and Master UAT Checklist.

## Acceptance status

| Area | Result | Evidence boundary |
|---|---|---|
| Source implementation | **PASS - source present** | Static review of Project closing lifecycle, approval reuse, transfer chain, budgets, authorization, audit, and UI. |
| Permission-first source | **PASS - source present; delegation pending** | Catalog and role UI keys are registered; no automatic grants. Initial delegation authority is unresolved and commands fail closed without an explicit grant. |
| Localization | **PASS - static key check** | JSON parsed for EN/AR and all 51 literal UI keys resolved in both catalogs, including sequential-order and empty-state controls. No browser/RTL rendering run. |
| Concurrency | **PARTIAL - source strategy present** | Financial writers and close take the Project SQL Server lock under serializable transactions. Runtime SQL Server contention was not run and is deferred. |
| API/frontend build | **NOT VERIFIED** | No builds were run under the Sprint 5 deferred validation policy. |
| EF migration and snapshot | **DEFERRED** | Consolidated with Sprint 3-5 in Sprint 7A; no migration generated and no snapshot modified for Sprint 5. |
| Database | **UNCHANGED / NOT ACCESSED** | No connection, schema operation, seed, or data write. |
| Runtime UAT | **DEFERRED** | Sprint 7B; no functional, browser, permission, or integration tests were executed. |

## Sprint 6 reporting consumer

The read-only Program/Portfolio rollup uses the selected Project/Fiscal Year closing record for status and the immutable closed snapshot for that period's budget, allocations, reversals, settlements, and carry-forward measures. Closing coverage is filtered to the selected Fiscal Year; a Project closed in another year does not count as closed for this reporting period. Parent views do not prepare, approve, or change a closing and do not duplicate its audit events. See the [Sprint 6 implementation report](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md).
