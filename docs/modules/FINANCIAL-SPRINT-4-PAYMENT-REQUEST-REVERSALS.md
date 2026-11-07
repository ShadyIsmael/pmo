# Financial Sprint 4 — Project Payment Request Reversals

> **Authorization supersession (2026-11-03):** This record preserves the original Sprint 4 decision and implementation notes. `finances.read` and `finances.write` are retired and unsupported by current API/UI authorization. Current permissions are operation-specific; see the [permission model](../architecture/permission-model.md) and [permission audit matrix](../security/permission-audit/ACTION-PERMISSION-MATRIX.csv). Persisted grants and runtime authorization remain unverified.

**Scope:** Project Details → Financial Tab only
**Status:** Source implementation present; compile not verified; EF migration/snapshot deferred; database unchanged; runtime UAT deferred to Sprint 7B.

## Approved behavior

An Approved Payment Request cannot be directly cancelled. A separate Reversal Request requires explicit approval and may release only unpaid commitment. It never alters the original approved request, approval decisions, Annual Budget association, actual settlements, or historical audit entries. Cash refunds, settlement corrections, bank transfers, and CostEntry creation are excluded.

For each approved request:

```text
Maximum Reversible = Original Approved Amount - Valid Settlements - Approved Reversals
Effective Active Allocation = Original Approved Amount - Approved Reversals
Remaining Payable = Effective Active Allocation - Valid Settlements
Available Annual Budget = Approved Annual Budget - Sum(Effective Active Allocations)
```

All values are calculated from persisted source rows. Draft, Pending, Rejected, and cancelled Draft reversals do not release budget. Cumulative paid and released values may not exceed the original approved amount. Multiple approved partial reversals are allowed; no more than one reversal may be Pending for an original request. Legacy requests without verified annual-budget provenance fail closed. Reversal release is blocked when the original budget fiscal year is not the Tenant's current fiscal year; Sprint 5 closing/carry-forward policy is still required for cross-year behavior.

## Lifecycle and approval

The source workflow supports Draft → Pending → Approved/Rejected and Draft cancellation. Draft edits are limited to the requester and use the request row version. Submission freezes the amount, reason, approval method, and assignments. Create retries with the same key and payload return the original result to its requester even if later approvals have reduced the remaining reversible amount; changed payloads conflict. Reversal approvers are selected from active Project Team members linked to active users who hold both `finances.write` and `ProjectPaymentReversal.Approve`; the service rechecks this eligibility at submission and decision time.

The existing `ApprovalRequest`, `ApprovalAction`, `PaymentRequestApproverAssignment`, `PaymentRequestWorkflowPolicy`, and `AuditEntries` infrastructure is reused. Sequential requests enforce the assigned order and stop on rejection. Parallel requests require at least one Mandatory approver; every Mandatory approver must approve, while Optional responses do not block final approval. No independent approval or reservation ledger is added.

Commands run in a serializable transaction and take the same SQL Server Project row lock used by Payment Request approval and settlement recording. Final reversal approval rereads paid and released amounts while protected, validates the original approved Annual Budget, and persists the status and audit events atomically. Settlement recording applies the approved-release amount to its maximum payable validation and uses the same lock. This is source inspection only; SQL Server concurrency behavior is not runtime verified.

## Permission matrix

| Action | Required authorization | Additional service rule |
|---|---|---|
| View request and reversal history | `finances.read` | Authenticated Tenant/Project scope |
| Create reversal | `finances.write` + `ProjectPaymentReversal.Create` | Authorized active user in the authenticated Tenant/Project scope; approved original and current approved Annual Budget |
| Edit, submit, or cancel a Draft | `finances.write` + `ProjectPaymentReversal.Create` | Requester owns the Draft; amount remains within current unpaid commitment |
| Select reversal approvers | `finances.write` + `ProjectPaymentReversal.Create` | Candidate must be an active Project Team member linked to an active user with `finances.write` and `ProjectPaymentReversal.Approve` |
| Approve or reject | `finances.write` + `ProjectPaymentReversal.Approve` | Current assigned approver, active eligible Project Team member; approval method and decision state validated |

The catalog registers the two sensitive permissions under `ProjectPaymentReversal`, with EN/AR resource and action labels plus permission-specific EN/AR descriptions shown in Roles & Permissions. The Roles & Permissions page is catalog-driven. The reversal approver selector exposes only active Project Team members with both required write and reversal-approval permissions. Audit assignment events retain approver names and IDs plus the chosen approval method. Seeded/default roles do not receive these grants. The existing role-delegation policy requires a grantor to hold a permission before delegating it, so the initial authority for granting these permissions remains an explicit Product Owner decision. Until granted, reversal writes fail closed. Runtime permission testing is deferred to Sprint 7B.

## API and user interface

Project-scoped reversal routes are added under `/api/projects/{projectId}/payment-requests/{requestId}/reversals`:

- `GET` reversal history and balance summary.
- `POST` create Draft with amount, reason, and idempotency key.
- `PUT /{reversalId}` edit Draft using row version.
- `POST /{reversalId}/submit` freeze selected Sequential/Parallel approvers.
- `POST /{reversalId}/decisions` record an assigned approver decision.
- `POST /{reversalId}/cancel` cancel a Draft only.

The sibling route `/api/projects/{projectId}/payment-requests/reversal-approvers` returns only active in-scope team members authorized to decide reversals.

The Financial tab presents original approved amount, paid total, previously released amount, maximum reversible amount, active allocation, remaining payable, reversal history, approver decisions, and audit activity. It explicitly describes commitment release and does not expose a refund action. EN/AR, RTL, light/dark, and permission-based rendering use the existing project financial workspace patterns.

## Audit

Events are appended to the existing `AuditEntries` store inside the financial transaction:

`ReversalRequested`, `ReversalEdited`, `ReversalSubmitted`, `ReversalApproverAssigned`, `ReversalApproverApproved`, `ReversalApproverRejected`, `ReversalApproved`, `ReversalRejected`, `BudgetAllocationReleased`, and `ReversalCancelled`.

Events identify Tenant/Project, reversal and original request references, Annual Budget/Fiscal Year, actor, UTC timestamp, amount/currency, reason/comment, and relevant before/after paid, released, active-allocation, and payable values. Financial Activity maps these event types into business-facing entries. Failed financial mutations do not commit success events.

## Persistence / EF owner handover

This sprint adds no new ledger table. It reuses the existing shared approval and payment-detail records. The required source-model integration includes a same-Tenant/same-Project self-reference from reversal `ProjectPaymentRequestDetail` to the original request detail using `(TenantId, ProjectId, ReversesApprovalRequestId)` → `(TenantId, ProjectId, ApprovalRequestId)`, with restrictive delete behavior. The request-kind check constraint also disallows self-reversal and requires an original link for reversal-kind details. Existing reversal kind/reference/idempotency fields are used; the approved release amount remains the approved `ApprovalRequest.Amount` and is aggregated from canonical Approved status.

Do not edit historical migrations or the concurrently modified model snapshot during this sprint. The designated EF owner must reconcile the Sprint 3 shared `PaymentSettlements` changes and this Sprint 4 self-FK/check-constraint change against the final current model, generate coherent migration artifacts/SQL, and check the baseline strategy in Sprint 7A. No database migration was generated or applied here.

## Verification and deferred work

Static source review confirms command routes, permission policies, project/tenant filters, request-kind checks, shared-lock usage, approved-release formula, settlement protection, audit event producers, and UI/locale wiring. No API/frontend build, EF migration, SQL Server test, runtime permission test, or browser UAT was run in this sprint. The Development database was not accessed or changed.

The consolidated tests in [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md) cover authorization, cumulative release, sequential/parallel approvals, duplicate retries, concurrency with settlements, annual-budget availability, legacy data, cross-year restrictions, and EN/AR/RTL behavior. Initial sensitive-permission delegation and Sprint 5 closed-period/carry-forward treatment remain policy dependencies.

## Sprint 5 dependency update

An approved closing may move only the remaining payable commitment to the next Annual Budget through the funding-transfer record. Future unpaid-commitment reversals resolve the current transfer destination and may release only against that active, approved, open funding period. They never credit a closed historical budget, reverse a paid settlement, or create a refund. The original request and reversal budget provenance remains historical. See [Sprint 5](FINANCIAL-SPRINT-5-YEAR-END-CLOSING.md); compilation and runtime behavior are pending consolidated Sprint 7 closure.
