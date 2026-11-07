# Financial Management — Planned Payments

## Scope

Planned payments represent expected outgoing obligations associated with a project. They are separate from approved budgets, purchase commitments, and actual expenditures. An optional milestone link references a milestone in the project's active Working plan for information only; rescheduling a milestone does not change the payment due date.

## Lifecycle and accounting

- An authorized finance user can create, edit, list, view, and soft-delete a planned payment. Payments with settlements cannot be deleted.
- Each payment records its project, reference, purpose, optional description, amount, project currency, due date, optional milestone, and notes.
- Payments use the project's currency and monetary precision of four fractional digits. Amounts with additional precision are rejected rather than silently rounded.
- A planned payment has zero or more actual settlements; each row in the shared `PaymentSettlements` table references exactly one owner: either a planned `PaymentObligation` or an approved project Payment Request. The relationship is scoped by Tenant and Project. A settlement is a payment against its owner, not an independent obligation or a second ledger.
- Settlement amounts must be positive and cannot exceed the outstanding balance. Idempotency keys prevent retry submissions from recording the same settlement twice; a reused key with a different payload is a conflict. Concurrent writes use an obligation concurrency token so competing settlements cannot consume the same outstanding balance.
- Total paid is the sum of valid, non-deleted settlements. Outstanding balance is the planned amount less total paid. A planned amount cannot be reduced below total paid, and a payment with settlements cannot be deleted.
- Settlement progress (`Not Paid`, `Partially Paid`, `Fully Paid`) is separate from due-date status (`Upcoming`, `Due`, `Overdue`, `Settled`). Due and overdue status only applies while an outstanding balance remains.
- Actual settlements do not create `CostEntry` records or reduce budget. Their accounting relationship to budget consumption remains unapproved; record budget expenditures separately through the existing CostEntry workflow.
- The planned payment lifecycle does not change or replace existing budget, cost, commitment, report, or approval functionality.

## Authorization and notifications

The API uses the existing finance read/write policies, tenant query filters, project lookup, management project scope, and read-only perspective checks. Recording any actual settlement additionally requires `PaymentSettlement.Record`; the Project Payment Request endpoint also requires `finances.write`. The dedicated key is registered but is not granted automatically. Optional milestone IDs are checked against a Milestone activity in an active Working plan for the same project.

The hourly notification worker checks that the payment creator is still an active tenant user with effective `finances.read` or `finances.write` permission. It sends due or overdue events through the existing notification service, which applies user notification preferences. When linked, the notification identifies the milestone by its current active Working-plan name. Dispatch records deduplicate notifications per payment, recipient, event type, and UTC date. The current recipient rule is deliberately limited to the creator; product confirmation is still required if project finance managers or other authorized users should also be notified.

## Persistence and API

The isolated implementation adds `PaymentObligations`, `PaymentSettlements`, and `PaymentNotificationDispatches` through migration `20261026160000_AddPlannedPaymentLifecycle`. Follow-up migration `20261026170000_AddPaymentTenantScopedRelationships` adds tenant-scoped required foreign keys from settlements and notification dispatches to their parent payment. Neither migration has been applied to a Development database. The model snapshot must be reconciled with concurrent Project Planning/Working Calendar changes before integration or migration execution.

Endpoints are under `/api/payment-obligations`:

- `GET` list (optional project, milestone, and due-only filters), `GET /{id}`, and `GET /{id}/settlements`
- `POST` create, `PUT /{id}` update, and `DELETE /{id}` soft-delete
- `POST /{id}/settlements` record a partial or full settlement

The React/Vite Financial Management page provides project and due filters, payment creation/editing, details, settlement history, and settlement entry. English and Arabic text is provided.

## Verification status and open items

The focused lifecycle tests cover creation and outstanding balance, partial/multiple settlements, overpayment rejection, idempotent replay, concurrent settlement protection, tenant-filtered listing, database rejection of missing and cross-tenant parents, active Working-plan milestone validation, CRUD, and due-notification deduplication including the milestone name. These tests do not constitute authenticated live UAT or Development database verification.

Before integration/UAT:

1. Reconcile the migration and model snapshot with the concurrent Working Calendar migration and current shared `AppDbContext`.
2. Confirm the authorized notification-recipient policy.
3. Run authorization/project-scope, migration, and browser/API integration checks against an isolated test database and authenticated environment.
4. Coordinate any Development migration only after schema review, verified database identity, backup, and the established validation procedure.

Planned payments and settlements are not automatically mapped to budget drawdown or actual expenditure because the budget-accounting rule remains unapproved.

## Project Payment Request commitment reversals - Financial Sprint 4

The Sprint 4 reversal workflow applies only to Approved Project Payment Requests and only releases unpaid commitment. A reversal is a separate ApprovalRequest linked to the original request and its same Project Annual Budget/Fiscal Year. The existing sequential/parallel approval policy, approver assignments, ApprovalActions, and shared AuditEntries are reused. Only an Approved reversal reduces active allocation; Draft, Pending, Rejected, and cancelled drafts do not.

`Effective Active Allocation = Original Approved Request Amount - Approved Reversal Amounts` and `Remaining Payable = Effective Active Allocation - Valid Actual Settlements`. Settlements remain immutable historical cash-paid facts and do not trigger reversal, refund, budget release, or a CostEntry. Budget availability subtracts the effective allocation once; settlements are not deducted again. The workflow rejects a cumulative release that would exceed the unpaid amount, serializes settlement and reversal decisions through the same Project financial lock, and allows only one Pending reversal per original request.

Legacy requests without verified Annual Budget/Fiscal Year provenance and reversals tied to a non-current fiscal period fail closed. Sprint 4 does not implement settlement correction, cash refund, bank transfer, Year-End Closing, or Carry Forward. The source model adds a same-Tenant/same-Project self-reference from reversal details to the original Payment Request. Migration generation, model snapshot reconciliation, builds, database deployment, and runtime UAT are deferred to Sprint 7; no database changes were made for Sprint 4.

## Project Payment Request settlements — Sprint 3 source integration

Sprint 3 extends the existing `PaymentSettlements` table for actual payments against Approved Project Payment Requests. The new owner link stores the authenticated Tenant and Project directly and has a composite foreign key to `ProjectPaymentRequestDetails`; historical planned-payment settlements remain linked to `PaymentObligations`. Exactly one owner is allowed. The existing Planned Payments UI and API remain available.

Project Payment Request settlements require an Approved request, positive amount with at most four decimal places, a unique request reference, matching request currency, payment date, and description. The API derives reporting Fiscal Year from the Tenant calendar. Partial payments are supported; full settlement occurs when the derived outstanding amount reaches zero. Posted settlement rows have no edit/delete operation in this Sprint.

The request's approved amount stays reserved in its Annual Budget through partial and full payment. Settlement does not reserve or release budget again and does not create `CostEntry`. Settlement history reuses the same ledger and shared `AuditEntries`; no duplicate obligation, reservation, or settlement table is introduced. The new API endpoints are `GET` and `POST /api/projects/{projectId}/payment-requests/{requestId}/settlements`.

Cross-year reporting preserves two separate facts: the request and its allocation stay linked to the original Annual Budget Fiscal Year; the actual settlement date resolves to the Tenant Fiscal Year containing that date. The current model does not yet contain a formally closed-period record or Year-End Closing state. Consequently, the source can label the settlement period but cannot currently prove or enforce a closed-period rejection. That dependency and the exact outstanding-obligation posting policy remain for Sprint 5; do not claim closed-period enforcement from this Sprint.

The source requires an additive migration to add Project Request ownership, ProjectId, currency, description, composite scope constraints, and indexes. Existing settlement rows retain their PaymentObligation owner. Their ProjectId can be derived through the existing required Tenant/PaymentObligation relationship, subject to a migration guard that aborts if any parent is unresolved. The legacy table never stored settlement currency, and the Payment Obligation's current currency is not a reliable historical value because it could previously change. Therefore legacy settlement currency remains `NULL` and the UI displays “currency not recorded”; no guessed currency is backfilled. New Payment Request settlements require and store the approved request currency. New planned-payment settlements store the obligation currency when recorded, and the service now prevents changing an obligation's currency after a settlement exists.

The Sprint 3 migration has **not been generated** because the authorized build was blocked by automatic review of an observed Visual Studio telemetry request. The review did not disclose the endpoint, process, or payload and prohibited retrying through another route; no authorized alternate runner was provided. No migration was applied. Database identity/history/backup and physical schema are not verified for this task.

## Program/Portfolio reporting consumer - Sprint 6

Parent read-only rollups count Payment Request allocations from Project Annual Budgets and the active funding assignment. Actual Payment Request Settlements are grouped by the Tenant Fiscal Year containing each actual payment date, and their funding-transfer link must match that payment year. They are not subtracted from Annual Budget availability. Legacy Payment Obligation settlements are excluded from the Payment Request cash metric so one payment fact is not counted through both source types. Reports group results by exact currency code and use current hierarchy; incomplete legacy or funding provenance produces a data gap. The rollup is not a new ledger and emits no artificial financial audit event.

## Sprint 5 cross-year funding assignment

For a Payment Request carried forward, preserve its original Fiscal Year and Annual Budget association. An append-only funding transfer identifies the current Annual Budget that funds its outstanding payable amount. Settlements use the active transfer destination, remain dated in their actual Tenant Fiscal Year, and do not create another reservation. Reversal releases target the current funding assignment and cannot credit a closed source year. Each request must have one valid consecutive transfer chain; settlement/reversal/closing operations fail closed when provenance is inconsistent. See [Year-End Closing](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md).
## Project Finance workspace placement

The existing Planned Payments experience is available inside Project Details > Finance > Settlements & Reversals. The embedded view is scoped to the current Project and retains planned payment, settlement, details, validation, and authorization behavior.

The standalone `/costs/payments` route remains available for existing deep links. The old application-wide Finance navigation and legacy global cost, budget, actual-cost, and forecast screens have been removed. Planned Payments continues to use the existing PaymentObligation and shared PaymentSettlement relationships; this cleanup does not change settlement ownership, accounting rules, or persistence.

See [Project Finance Workspace](PROJECT-FINANCE-WORKSPACE.md) and [Finance Legacy Cleanup](FINANCIAL-LEGACY-CLEANUP.md) for the section map and retirement boundary.
