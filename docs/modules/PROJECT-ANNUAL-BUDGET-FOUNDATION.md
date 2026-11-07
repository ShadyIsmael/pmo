# Project Annual Budget Foundation — Sprint 1

> **Authorization supersession (2026-11-03):** This record preserves the original Sprint 1 decisions. `finances.read` and `finances.write` are retired and unsupported by current API/UI authorization; current read and operation checks use canonical permissions. `ProjectFinancialFoundation.View` is the current read-only permission for the Financial Overview foundation. See the [permission model](../architecture/permission-model.md) and [permission audit matrix](../security/permission-audit/ACTION-PERMISSION-MATRIX.csv). Persisted grants and runtime authorization remain unverified.

**Scope authorization:** 2026-10-30, Project Finance only. This scope is additive to Project Financial Management Part 1/Sprint 1 and Payment Request/Sprint 2. It does not resolve or rewrite D1–D17.

Annual budget records are owned only by Projects. Programs and Portfolios have no independent budgets; any future Program or Portfolio financial values are calculated aggregations of their descendant Projects. Year-End Closing remains a Project-level capability and is outside this Sprint.

**Lifecycle update (2026-10-31):** The original Sprint 1 direct Draft-to-Approved description below is historical. The current Product Owner-approved lifecycle is Draft → explicit submission → assigned review → Approved or Returned for Changes, with separately reviewed revisions. See [Project Lifetime Ceiling and Annual Budget Lifecycle](FINANCIAL-PROJECT-LIFETIME-CEILING-ANNUAL-BUDGET-LIFECYCLE.md) for the current status mapping and permissions.

## Delivered source scope

`ProjectAnnualBudget` is a Tenant- and Project-owned record with one row per Project and fiscal year. Its fiscal-year label and boundaries are derived from the Tenant fiscal-year configuration using `ITenantFiscalYearService` and `IFiscalYearCalendarService`. The label uses the fiscal-year ending year. Boundaries are persisted as date-only values.

The row stores an immutable financial reference, project currency, new approved funding, approved carry forward, approved adjustments, lifecycle status, approver, approval timestamp, standard metadata, and SQL rowversion. The total is calculated as:

```text
NewApprovedFunding + ApprovedCarryForward + ApprovedAdjustments
```

The total must remain non-negative. Funding and carry-forward are non-negative; an adjustment may be negative if the total remains non-negative. Amounts follow the existing persistence-wide `decimal(19,4)` monetary convention and the API validates the same scale. Carry forward is stored as zero through this Sprint's API and is not populated automatically.

Drafts can be edited with an optimistic concurrency token. Approval transitions a row from Draft to Approved, captures actor and UTC time, and makes the row immutable. A unique Tenant/Project/FiscalYear index prevents duplicates. Create uses a serializable transaction and the unique database constraint as the final concurrent-create safeguard. Approval and its audit record share one transaction. There is no delete endpoint.

## API and authorization

All endpoints are project-scoped under `api/projects/{projectId}/annual-budgets`. Current Annual Budget permissions supersede the original Sprint 1 authorization row:

| Operation | Permission |
|---|---|
| Read Annual Budget records and history | `ProjectAnnualBudget.View` |
| Create a Draft | `ProjectAnnualBudget.Create` |
| Edit a Draft | `ProjectAnnualBudget.EditDraft` |
| Submit a Draft for review | `ProjectAnnualBudget.SubmitForReview` |
| Prepare a revision of an approved budget | `ProjectAnnualBudget.ReviseApproved` |
| Decide a review | `ProjectAnnualBudget.Approve`, exact pending assignment, eligible active Project Team reviewer, and workflow-state/order checks |
| Read Project approval defaults and eligible reviewer choices | `ProjectAnnualBudget.View` + `ProjectSettings.View`; eligible choices require active linked Project Team membership and `ProjectAnnualBudget.Approve` |
| Save Project approval defaults | `ProjectSettings.View` + `ProjectSettings.Edit` + `ProjectAnnualBudget.ManageApprovalConfiguration` (which depends on `ProjectAnnualBudget.View`) |
| Customize one submission | `ProjectAnnualBudget.SubmitForReview` + `ProjectApproval.Override`; approved-budget revisions use `ProjectAnnualBudget.ReviseApproved` + `ProjectApproval.Override` |

Backend controller and workflow both validate the authenticated Tenant, actor, management project scope, effective permission, and read-only View-As state. The server derives period dates and currency from Tenant/Project sources; it does not accept dates or currency from the browser and never falls back to GBP.

`ProjectAnnualBudget.Approve` is registered in the authoritative permission catalog with Roles & Permissions labels in English and Arabic. No role receives it automatically; the administrator's general all-permissions seed explicitly excludes it while preserving existing assignments. The current role-management delegation rule only offers permissions already held by the delegator. Product Owner authorization for the initial approver/delegator policy remains unresolved, so there is no default approver grant and approval is unavailable until an explicitly authorized assignment mechanism grants this permission. This authorization gap prevents a claim of complete operational approval workflow.

## Financial Activity and compatibility

Create, meaningful Draft update, and approval events are appended to the shared `AuditEntries` store in the same EF unit of work as the budget mutation. Events carry Tenant/Project, actor, UTC timestamp, financial reference, currency, total amount, and before/after funding, carry-forward, adjustment, status, and approval values. The Project Financial Activity timeline maps them to business-readable labels.

At Sprint 1 delivery, the annual budget was a parallel planning record and did not change `Project.Budget` or Payment Request reservations. That compatibility statement is superseded for new Payment Requests by the Sprint 2 integration below. Existing unassociated Payment Requests remain historical and are not backfilled or reassigned.

The Tenant fiscal-year change guard now includes this record type, including soft-deleted records. It first checks whether `dbo.ProjectAnnualBudgets` exists, so Tenant settings updates remain compatible with databases where the additive migration has not yet been deployed.

## Persistence and deployment

EF changes add the entity, configuration, DbSet, and fiscal-history guard check. Migration `20261030122718_AddProjectAnnualBudgetFoundation` and its Designer/snapshot were generated as an additive migration. The idempotent SQL script was generated at `C:\Users\shady\AppData\Local\Temp\FopxPmProjectAnnualBudgetFoundation.sql` and reviewed: it creates the new table, constraints, indexes, and EF history row; it contains no drop, truncate, or business-data update/delete. The script and migration have not been applied. Any running database without the table cannot execute these API endpoints until deployment is separately authorized and the migration is applied through the approved gate.

At Sprint 1 delivery, no database was opened or modified; the offline EF gate reported four repository migrations and `EF Model synchronized: Yes`, and the then-current persistence/design-time, API, and frontend builds succeeded. That verification predates the Sprint 2 Payment Request annual-budget association source changes below and does not establish the current snapshot or build status. The previous NU1900 warning was about NuGet vulnerability metadata availability.

| Database gate item | Sprint 1 status |
|---|---|
| Development database identity | Not verified in this source-only task |
| Pending migration status against the actual database | Unknown; source contains the new migration, but actual history was not inspected |
| Independent backup | Not verified |
| Migration application | Not applied |
| Physical schema | Not verified |
| Persistence/runtime tests | Not run; deferred to Sprint 7 |
## Verification boundary and UAT

Runtime API/database, SQL Server concurrency, browser UAT, and permission-positive/negative/cross-scope tests are deferred to the consolidated Sprint 7 UAT per the project-wide execution policy. This source implementation must not be described as end-to-end verified. Initial approver delegation also remains blocked pending an approved authorization policy; no default grant is made. The permission scenarios are listed in the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md#project-annual-budget-foundation-security).

## Annual Budget Sprint 2 handoff

Future work may add separately approved carry-forward/reconciliation behavior and approved adjustments history without changing immutable approved records in place. Year-end closing and carry-forward execution remain Sprint 5. Settlement and CostEntry semantics remain unchanged.

## Payment Request annual-budget integration - Sprint 2, 2026-10-30

New Payment Requests are associated at creation with the Annual Budget for the Fiscal Year containing the server-derived current financial date, using the authenticated Tenant's fiscal calendar. Clients cannot choose a Tenant or Fiscal Year. The detail record stores the Annual Budget ID, Fiscal Year, and financial-reference snapshot; a composite Tenant/Project/Budget FK prevents cross-scope association. Draft creation requires a current-year Annual Budget. Submission additionally requires it to be Approved, in the Project currency, and within the persisted fiscal period.

The reservation formula for newly associated requests is `Approved Annual Budget - active Approved Payment Request allocations linked to that Annual Budget`. Draft and Pending requests reserve nothing; partial or full settlements do not release the allocation. Final approval re-reads the approved Annual Budget and linked approved allocations under the existing serializable SQL Server transaction, with a lock on both the Tenant/Project and Annual Budget rows. Approval, actions, reservation event, and audit entries use the same transaction. No separate reservation ledger or automatic CostEntry is introduced.

Legacy Payment Requests are left unassociated and retain their existing approvals, settlements, references, and audit history. There is no automatic mapping from `Project.Budget` and no automatic Annual Budget creation. Until approved requests without a Fiscal Year association are reconciled, the service fails closed for new spending actions that could otherwise exceed the annual limit. No historic allocation is counted against an invented Fiscal Year. The UI no longer exposes `Project.Budget` as an available-to-spend balance; it remains visible as a historical project ceiling.

Reads reuse `finances.read`; creation, editing, submission, cancellation, and individual approval decisions reuse `finances.write`. Creation also requires the authorized Project Manager policy; an approval decision additionally requires an active eligible Project Team member who is assigned to that request. No new Payment Request permission is added and no role grants are changed. Annual Budget approval remains governed by the separate `ProjectAnnualBudget.Approve` permission.

The focused association migration `20261030161223_AddProjectPaymentRequestAnnualBudgetAssociation` is generated from current source, and the EF snapshot reports no pending model changes. Its incremental SQL is saved in [`pending-migration-20261030161223.sql`](../../scripts/database/pending-migration-20261030161223.sql) and reviewed as additive; it adds nullable association fields and scoped constraints/indexes, and preserves unassociated historical requests. The migration has not been applied. Actual Development database identity/history, physical schema, and independent backup remain unverified. API/design-time and React TypeScript/production build results are recorded in the [Sprint 2 implementation report](FINANCIAL-SPRINT-2-IMPLEMENTATION-REPORT.md). Runtime, SQL Server concurrency, browser, and permission scenarios remain deferred to Sprint 7 UAT. Requests may retain their original approved Annual Budget across fiscal years; there is no automatic transfer, cancellation, carry-forward, or closed-period behavior in this Sprint.

## Sprint 5 — Closing and next-year opening extension

Project-level closing now produces an immutable statement, uses shared approval/audit infrastructure, and explicitly carries free balance and outstanding unpaid commitments to the next Project Annual Budget. `ApprovedReservedCarryForward` is a subset of `ApprovedCarryForward`, so reserved carried obligations remain part of the budget total and are simultaneously included in the active allocation calculation. A destination budget remains non-spendable while Draft and cannot be edited after a completed closing opened it. Approved values are not silently overwritten. Current funding assignment is represented by append-only request-to-budget transfer history; the original request-year association is preserved. Implementation and verification boundaries are in [Sprint 5 delivery record](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md).

## Annual Budget Project approval defaults and override — source extension, 2026-11-01

Project Settings are authoritative for Annual Budget approval method and ordered mandatory approvers. The submission Side Sheet loads and previews those settings automatically; invalid configuration blocks default submission and identifies unavailable approvers. An explicitly authorized user with `ProjectApproval.Override` may change the method, approvers, or order for one submission after entering a reason. It does not update Project Settings. Every submission snapshots the defaults and effective plan in the append-only Financial Audit Trail and persists routing assignments on the Approval Request. Resubmission resolves current Project defaults again; already-pending assignments remain unchanged. Payment Requests and Year-End Closing retain their own existing workflows.

`ProjectApproval.Override` appears in the permission catalog with English/Arabic labels and descriptions and is excluded from all automatic role grants, including the seeded administrator permission set. Customizing an initial or returned submission requires `ProjectAnnualBudget.SubmitForReview` plus `ProjectApproval.Override`; customizing a revision to an approved budget requires `ProjectAnnualBudget.ReviseApproved` plus `ProjectApproval.Override`. Selected reviewers must still be active linked Project Team users with `ProjectAnnualBudget.Approve`; mandatory-stage, method, ordering, scope, rowversion, and submitter-separation checks continue to apply.

The source extension adds `ProjectAnnualBudgetApprovalConfiguration` and `ProjectAnnualBudgetApprovalApprover` EF entities and mappings. At the initial source-only handoff, their snapshot and migration were not yet generated. The diagnostic and remediation status below supersedes that initial handoff state.

### Development schema diagnosis and deployment — 2026-11-01

The host-connected gate verified SQL Server `Shady`, database `NexusPmo` (ID `9`). The current EF model initially differed from the snapshot by the two approval-configuration entities. The additive migration `20261101114229_AddProjectAnnualBudgetApprovalConfiguration` was generated, and the fresh offline EF gate reported `EF Model synchronized: Yes`. The reviewed migration was then applied through the Development gate: eight migrations are now recorded and none are pending.

Reviewed SQL is saved as [`pending-migration-20261101114229.sql`](../../scripts/database/pending-migration-20261101114229.sql), SHA-256 `54F2A6ED4FAE3772CC4B78AC21A219ADBB7F25FAFA7CAAEE0D78B9C7B4387291`. It created only the two approval-configuration tables, their constraints/indexes, and the EF history entry; it contained no updates/deletes/backfills to existing business or financial rows. Before application, the gate created and verified `C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\Backup\NexusPmo-Development-20261101-114641-c8db4bdbd7cc4203aa223637a722362a.bak` using `RESTORE VERIFYONLY`.

The API log supplied by the owner confirmed the original HTTP 500 was `Microsoft.Data.SqlClient.SqlException`, error 208/state 1/class 16, in `GetApprovalConfigurationAsync` at `ProjectAnnualBudgetWorkflow.cs:300`, returned from controller line 38. The log showed no separate inner exception. After migration, direct SQL verified both tables, their two check constraints, three foreign keys, and indexes; both new tables contain zero rows, so no Project defaults were fabricated. The endpoint can now handle a missing configuration row and return its controlled actionable state, but that post-migration API response has not been runtime-tested.

The gate reports the post-apply schema matches the current model after excluding the acknowledged baseline drift. **Overall physical schema verification remains No** because the 13 pre-existing planning differences remain: eight nullable text-format columns across `PlanActivities`/`PlanWbsNodes`, three nullable `PlanningCalendars` columns, and two missing planning self-FKs. The three overlapping active ProjectMember rows totaling 120% also remain a separate, unreconciled condition. None was changed by this migration. Persistence tests, endpoint runtime verification, and UAT remain incomplete.

## Sprint 6 reporting source contract

Annual Budgets remain Project-owned records. Program and Portfolio views query authorized descendant Projects and derive currency-separated totals; no parent budget, copied balance, or rollup table is created. For one Fiscal Year, budget availability is the approved Annual Budget less effective active allocations. Transfers and approved reversals affect the current funding budget once. Actual Settlements are shown by actual payment Fiscal Year and do not reduce availability again. Parent summaries use the current hierarchy and remain provisional when budgets, source associations, or financial provenance are missing or inconsistent. See the [Sprint 6 implementation report](FINANCIAL-SPRINT-6-PROGRAM-PORTFOLIO-FINANCIAL-ROLLUP.md).
