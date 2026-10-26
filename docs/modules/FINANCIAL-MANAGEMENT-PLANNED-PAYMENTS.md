# Financial Management — Planned Payments

## Scope

Planned payments represent expected outgoing obligations associated with a project. They are separate from approved budgets, purchase commitments, and actual expenditures. An optional milestone link references a milestone in the project's active Working plan for information only; rescheduling a milestone does not change the payment due date.

## Lifecycle and accounting

- An authorized finance user can create, edit, list, view, and soft-delete a planned payment. Payments with settlements cannot be deleted.
- Each payment records its project, reference, purpose, optional description, amount, project currency, due date, optional milestone, and notes.
- Payments use the project's currency and monetary precision of four fractional digits. Amounts with additional precision are rejected rather than silently rounded.
- A payment may have multiple settlement records. Settlement amounts must be positive and cannot exceed the outstanding balance. Idempotency keys prevent retry submissions from recording the same settlement twice; a reused key with a different payload is a conflict.
- Outstanding balance is the planned amount less non-deleted settlements. Due and overdue status is based on the UTC calendar date and only applies while an outstanding balance remains.
- Settlements do not create `CostEntry` records, reduce budget, or otherwise count as actual expenditure. Record actual expenditure separately through the existing CostEntry workflow.
- The planned payment lifecycle does not change or replace existing budget, cost, commitment, report, or approval functionality.

## Authorization and notifications

The API uses the existing finance read/write policies, tenant query filters, project lookup, management project scope, and read-only perspective checks. Optional milestone IDs are checked against a Milestone activity in an active Working plan for the same project.

The hourly notification worker checks that the payment creator is still an active tenant user with effective `finances.read` or `finances.write` permission. It sends due or overdue events through the existing notification service, which applies user notification preferences. When linked, the notification identifies the milestone by its current active Working-plan name. Dispatch records deduplicate notifications per payment, recipient, event type, and UTC date. The current recipient rule is deliberately limited to the creator; product confirmation is still required if project finance managers or other authorized users should also be notified.

## Persistence and API

The isolated implementation adds `PaymentObligations`, `PaymentSettlements`, and `PaymentNotificationDispatches` through migration `20261026160000_AddPlannedPaymentLifecycle`. The migration has not been applied to a Development database. Its model snapshot must be reconciled with concurrent Project Planning/Working Calendar changes before integration or migration execution.

Endpoints are under `/api/payment-obligations`:

- `GET` list (optional project, milestone, and due-only filters), `GET /{id}`, and `GET /{id}/settlements`
- `POST` create, `PUT /{id}` update, and `DELETE /{id}` soft-delete
- `POST /{id}/settlements` record a partial or full settlement

The React/Vite Financial Management page provides project and due filters, payment creation/editing, details, settlement history, and settlement entry. English and Arabic text is provided.

## Verification status and open items

The focused lifecycle tests cover creation and outstanding balance, partial/multiple settlements, overpayment rejection, idempotent replay, concurrent settlement protection, tenant-filtered listing, active Working-plan milestone validation, CRUD, and due-notification deduplication including the milestone name. These tests do not constitute authenticated live UAT or Development database verification.

Before integration/UAT:

1. Reconcile the migration and model snapshot with the concurrent Working Calendar migration and current shared `AppDbContext`.
2. Confirm the authorized notification-recipient policy.
3. Run persistence, authorization/project-scope, concurrent-settlement, migration, and browser/API integration checks against an isolated test database and authenticated environment.
4. Coordinate any Development migration only after schema review, verified database identity, backup, and the established validation procedure.

Planned payments and settlements are not mapped to actual expenditure because the accounting rule for that relationship remains unconfirmed.
