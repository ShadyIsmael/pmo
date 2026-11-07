# Project Finance Workspace

> **Authorization supersession (2026-11-03):** This record preserves the original workspace decision. `finances.read` and `finances.write` are retired and unsupported by current API/UI authorization; the current shell and each workspace section use canonical permissions, and each operation has its own action key. See the [permission model](../architecture/permission-model.md) and [permission audit matrix](../security/permission-audit/ACTION-PERMISSION-MATRIX.csv). Persisted grants and runtime authorization remain unverified.

## Purpose

The Finance workspace stays inside **Project Details → Finance**. Its primary navigation is a vertical sidebar on large screens and a section selector on narrow screens. It does not create an application-wide Finance module or change the Project-owned financial architecture.

## Information architecture and feature mapping

| Workspace section | Responsibilities and preserved functionality |
|---|---|
| Overview | Selected-year financial KPIs, finalized Budget vs Actual comparison when source data is reliable, financial term definitions, and the existing project financial foundation. Legacy budget-line, cost, commitment, and forecast calculations are not presented here. |
| Annual Budgets | Project Annual Budget records, approved totals and balances, status, approval actions, and closing/carry-forward context. Legacy `Budget` allocation lines and revision requests are not presented as Annual Budgets. |
| Payment Requests | Request list, create/edit, submit, approval configuration, approval decisions, and request approval history. Requests remain associated with their original Annual Budget fiscal year. |
| Settlements & Reversals | Actual settlements and settlement history for approved Project Payment Requests, reversal creation and approval history, and legacy Planned Payments with their existing settlement history. Payment-request approval remains in Payment Requests. |
| Fiscal Year Closing | Closing readiness, dependencies, blocking conditions, the year-end workflow, carry-forward, and closing history. The header fiscal year is the source year used by the closing workspace. |
| Financial Audit Trail | Chronological Project Financial Activity from the existing audit API, including event/action, available record reference or description, actor, timestamp, outcome/details, filters, and pagination. |

Program and Portfolio financial views remain read-only rollups. This workspace does not move financial ownership above the Project.

Legacy project budget lines, cost entries, commitments, forecast summaries, and budget-revision forms have been removed from the Finance UI. Their records and backend APIs are preserved for existing project read models, workflow integrations, audit history, and compatibility; see [Finance Legacy Cleanup](FINANCIAL-LEGACY-CLEANUP.md). The standalone `/costs/payments` compatibility route remains available but is not a top-level navigation module; Planned Payments is also embedded under Settlements & Reversals.

## Fiscal-year context and source rules

The header selector is the shared fiscal-year context for Overview, Annual Budgets, and Fiscal Year Closing. Annual Budget record selection and closing-history selection update that context. The selected year persists while navigating between sections.

Payment Requests are not filtered by the header selector: requests stay tied to their original budget year, and the overview request counts explicitly include requests across years. Settlement records are not implicitly filtered because the request allocation year and the settlement reporting year are different facts. Audit events are filtered only when the user applies the existing event, actor, date, or reference filters.

Financial values are read from existing APIs. Approved budget is shown only for an Approved Annual Budget. Actual settlement totals and the Budget vs Actual chart require a matching finalized closing statement. Available Budget is shown only from the existing current-year availability result; the frontend does not calculate it. Missing source values remain unavailable instead of being replaced with zero or a guessed currency.

## Responsive and localized behavior

- At large desktop widths, the Finance navigation occupies a fixed 15rem column and remains visible beside the workspace.
- At tablet and mobile widths, navigation becomes a labeled section selector so the financial content keeps the available width.
- Logical inline borders, spacing, and text alignment support right-to-left Arabic layout; navigation moves to the inline-start side automatically.
- Every section label and new Finance explanation has English and Arabic localization.
- Navigation supports keyboard focus and a visible active section state.

## Permission handling

Finance now uses workspace-level View/Manage permissions. The parent permission pair controls only the Finance shell; each section keeps its own data View and ordinary Manage permission. Existing approval and financial action permissions remain independent:

| Permission | Finance workspace use |
|---|---|
| `ProjectFinance.View` / `ProjectFinance.Manage` | Open/manage the shell only. Neither permission grants section data access. |
| `ProjectAnnualBudget.View` / `ProjectAnnualBudget.Manage` | View the Annual Budgets section; Manage covers draft creation, editing, and submission. |
| `ProjectPaymentRequest.View` / `ProjectPaymentRequest.Manage` | View requests; Manage covers ordinary draft create/edit/submit/cancel. Assigned reviewers retain assigned-record access only. |
| `ProjectSettlement.View` / `ProjectSettlement.Manage` | View Settlements & Reversals; Manage covers legacy Planned Payment create/edit/delete. |
| `ProjectAnnualBudgetClosing.View` / `ProjectAnnualBudgetClosing.Manage` | View closings; Manage covers prepare, submit, and cancel. |
| `ProjectFinancialAuditTrail.View` | View immutable financial audit records. No Manage permission exists. |
| `ProjectAnnualBudget.Approve` | Approve an Annual Budget. |
| `ProjectAnnualBudget.ReviseApproved` | Revise an approved Annual Budget. |
| `ProjectPaymentRequest.Approve` | Approve or reject a Payment Request. |
| `PaymentSettlement.Record` | Record actual settlements. |
| `ProjectPaymentReversal.Create` / `ProjectPaymentReversal.Approve` | Create/submit a reversal; independently approve or reject it. |
| `ProjectAnnualBudget.Close` | Prepare and submit a Year-End Closing. |
| `ProjectAnnualBudget.CloseApprove` | Decide on a Year-End Closing. |
| `ProjectAnnualBudget.CarryForward` | Perform permitted carry-forward actions. |
| `ProjectApproval.Override` | Override approval configuration for one submission. |
| `ProjectAnnualBudget.ManageApprovalConfiguration` | Manage persistent approval configuration. |
| `ProjectFinancialCeiling.ApproveChange` | Approve a Project lifetime ceiling change only through an approved Change Request. |

Each Manage key requires its matching View key. Manage does not grant sibling-section access or any separately listed sensitive action. Legacy `finances.read/write` remains a compatibility bridge for mapped existing operations; it is not copied into the new grants. Parent shell access does not fetch child financial data. The API and services enforce these rules with Tenant/Project and workflow scope. Assigned reviewers retain narrow record access without general Finance management. Unauthorized access and API failures render an error/permission state, separate from an empty result.

The Project creation `Budget` field is the Project lifetime financial ceiling, not the current-year Annual Budget. Creating the Project does not create an allocation. The optional Initial Annual Budget field creates only a current-Fiscal-Year Draft and requires `finances.write`. Annual Budget submission/review/revision and ceiling reservations are detailed in [Project Lifetime Ceiling and Annual Budget Lifecycle](FINANCIAL-PROJECT-LIFETIME-CEILING-ANNUAL-BUDGET-LIFECYCLE.md). When the current Fiscal Year has no approved allocation, the authoritative availability API reports zero spendable balance with a distinct missing/Draft status; an Approved zero allocation remains a separate state.

## Source limitations

- The existing budget-availability endpoint provides a current-fiscal-year balance; other selected years show it as unavailable.
- The current trustworthy selected-year aggregate for actual Project Payment Request settlements comes from a finalized Annual Budget Closing snapshot. Before finalization, the workspace does not show a guessed aggregate.
- Pending request counts are across fiscal years because the existing request list is not year-scoped for this summary.
- The existing Financial Activity API supports filters and paging. No export/download capability was found in the Project Finance Activity or Planned Payments views.
- The legacy global `/api/costs/summary` calculation and global cost/budget/forecast React pages have been retired. The approved Program/Portfolio financial views use their dedicated read-only calculated rollup APIs.

## Owner UAT scenarios

1. Open Finance from Project Details and confirm that no additional application-level Finance sidebar or duplicate route was added.
2. Navigate all six sections by mouse and keyboard; confirm the active item, focus ring, and one visible primary workspace.
3. Switch fiscal years in Overview, Annual Budgets, and Fiscal Year Closing; confirm the context stays selected. Confirm Payment Requests remain across years and settlement/audit records are not incorrectly filtered.
4. Compare an Approved, finalized Annual Budget with its matching finalized closing statement; then check a draft, unclosed, or currency-mismatched year shows unavailable values and no fabricated chart.
5. Create/edit/submit a request and verify approval history, decision actions, and pending counts against effective permissions.
6. Record partial and full settlements; confirm actual paid totals and approval status remain distinct. Inspect reversal reasons/history and verify existing unpaid-only limits still apply.
7. Review closing prerequisites, blocking warnings, approval, carry-forward, and closing history using authorized users.
8. Filter audit activity by event, actor, date, and reference; verify record descriptions/references, actor, timestamp, restricted details, and empty/error states.
9. Test English desktop, Arabic RTL, keyboard navigation, and narrow mobile layouts. Confirm all financial tables remain usable and actions are available only to authorized users.
10. Confirm Program and Portfolio rollups remain read-only and unchanged.

Owner runtime UAT is pending. Browser UAT, service restarts, and database operations are not part of this source cleanup; source builds and targeted regression results are recorded in [Finance Legacy Cleanup](FINANCIAL-LEGACY-CLEANUP.md).
