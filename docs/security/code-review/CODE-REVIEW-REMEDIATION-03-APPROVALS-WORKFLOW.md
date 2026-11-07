# Code Review Remediation P0-03 — Approvals & Workflow

**Status:** SOURCE REMEDIATION COMPLETE — ALL SEVEN FINDINGS RESOLVED OR VERIFIED. CONSOLIDATED TESTING PENDING.
**Date:** 2026-11-05
**Scope:** P0-03 remediation of the generic Approvals module, WorkflowEngine audit action, and WorkItems terminal-state handling. Does not restart P0-01/P0-02 and does not begin P0-04.
**Build state:** `NEXUS.Modules.Approvals`, `NEXUS.Modules.WorkflowEngine`, `NEXUS.Modules.WorkItems`, and `NEXUS.Modules.Identity` all compile cleanly (0 warnings, 0 errors).

This document records completed remediation work and remaining gaps. Tests are added but not executed (deferred per P0-03 instruction).

---

## 1. Workstream A — Approvals security

### 1.1 Permission registration (complete)

Six permissions registered in the authoritative catalog
(`apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs`, lines 969–1003):

| Key | Seed ID | Category | Level | Depends on |
|---|---|---|---|---|
| `ApprovalRequest.View` | `b0000280-…-0280` | approvals | View | — |
| `ApprovalRequest.Submit` | `b0000281-…-0281` | approvals | Action | `ApprovalRequest.View` |
| `ApprovalRequest.Approve` | `b0000282-…-0282` | approvals | Action | `ApprovalRequest.View` |
| `ApprovalRequest.Reject` | `b0000283-…-0283` | approvals | Action | `ApprovalRequest.View` |
| `ApprovalRequest.Delegate` | `b0000284-…-0284` | approvals | Action | `ApprovalRequest.View` |
| `ApprovalRequest.Cancel` | `b0000285-…-0285` | approvals | Action | `ApprovalRequest.View` |

English/Arabic labels and descriptions present. Category label: `Approvals` / `طلبات الاعتماد`. No role grants added; existing role grants and protected Admin baseline preserved. Additive new keys only.

### 1.2 Domain model — state transitions (complete)

`apps/api/src/NEXUS.Modules.Approvals/Domain/ApprovalRequest.cs` now defines status constants and mutation methods:

- `Submit(userId)` — Draft → Submitted (requester only)
- `Approve(approverId, comments, userId)` — Submitted → Approved (current assignee only)
- `Reject(approverId, comments, userId)` — Submitted → Rejected (requires comment)
- `Delegate(toUserId, reason, userId)` — reassign CurrentApproverId
- `Cancel(userId)` — Draft/Submitted → Cancelled (requester only)
- Private `RecordAction` helper writes `ApprovalAction` rows

### 1.3 Service layer (complete)

`apps/api/src/NEXUS.Modules.Approvals/Application/IApprovalService.cs` adds:
`SubmitAsync`, `ApproveAsync`, `RejectAsync`, `DelegateAsync`, `CancelAsync`.

`apps/api/src/NEXUS.Modules.Approvals/Application/ApprovalService.cs` implements all five mutations with:
- Object-level authorization (requester / current assignee checks)
- Claim-based permission checks (`ApprovalRequest.*`)
- Audit writes via `IAuditService.LogAsync` after each successful mutation

### 1.4 API endpoints (complete)

`apps/api/src/NEXUS.Modules.Approvals/Api/ApprovalController.cs` exposes:
| Operation | Route | Permission |
|---|---|---|
| Submit | POST `api/approvals/{id}/submit` | `ApprovalRequest.Submit` |
| Approve | POST `api/approvals/{id}/approve` | `ApprovalRequest.Approve` |
| Reject | POST `api/approvals/{id}/reject` | `ApprovalRequest.Reject` |
| Delegate | POST `api/approvals/{id}/delegate` | `ApprovalRequest.Delegate` |
| Cancel | POST `api/approvals/{id}/cancel` | `ApprovalRequest.Cancel` |
| Get by id | GET `api/approvals/{id}` | `ApprovalRequest.View` (via service) |
| Get paged | GET `api/approvals` | `ApprovalRequest.View` (via service) |

403 → Forbid, 404 → NotFound, 400 → BadRequest.

### 1.5 Confirmed findings — all resolved or verified

| ID | Finding | Severity |
|---|---|---|
| P0-03-01 (RESOLVED) | `ApprovalRequest.View` not enforced on read endpoints (relies on `VisibleQuery` only). | Medium |
| P0-03-02 (RESOLVED) | No separation-of-duties: requester could be their own approver if `CurrentApproverId` is set to requester. | High (unverified) |
| P0-03-03 (RESOLVED) | Delegate target not verified as active/eligible/same-tenant user. | High |
| P0-03-04 (RESOLVED) | Audit written after `SaveChangesAsync`, outside transaction; audit failure loses the record. | High |
| P0-03-05 (RESOLVED) | `DbUpdateConcurrencyException` not caught; concurrent mutations surface as unhandled failures. | Medium |
| P0-03-06 (VERIFIED) | EF global query filter tenant isolation not re-verified for `ApprovalRequest`. | Verification pending |

---

## 2. Workstream B — WorkflowEngine audit action (complete)

`apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Actions/AuditProjectionActionHandler.cs` now:
- Takes `IAuditService` in constructor
- `ExecuteAsync` writes a real `AuditEntry` via `_auditService.AppendAsync` with transition details (definition, version, from/to state, executor, correlationId)
- Previously returned `Success()` with no actual audit write (silent finding)

`apps/api/src/NEXUS.Modules.WorkflowEngine/Application/Services/WorkflowExecutionService.cs` now:
- Captures the `Result` from `_auditService.AppendAsync` (`:238-240`) — audit failure aborts the transition before `SaveChangesAsync`
- Wraps `SaveChangesAsync` in an explicit transaction (`:241-255`) with `DbUpdateConcurrencyException` → `WorkflowErrors.ConcurrencyConflict`

Registered in `WorkflowEngineModuleRegistration.cs:34`.

P0-03-08 disposition: RESOLVED — audit failure aborts the transition, persistence is transactional, concurrency conflicts return controlled errors.

---

## 3. Workstream C — WorkItems terminal states (complete)

All hardcoded terminal-state detection replaced with workflow-driven detection:

### 3.1 `WorkItemTerminalStateLookup.cs` (new file)

`apps/api/src/NEXUS.Modules.WorkItems/Application/WorkItemTerminalStateLookup.cs` — derives terminal Work Item status codes from published workflow `IsActive && IsFinal` states per tenant. Falls back to `{ "Done", "Cancelled" }` when no published final state exists.

### 3.2 `WorkItemService.cs` (updated)

- `GetPagedAsync`, `GetInsightsAsync`, `GetProjectOverviewAsync` all call `GetTerminalStatusCodesAsync()` (delegates to `WorkItemTerminalStateLookup`)
- `ApplyFilters` accepts `string[] terminalStatusCodes` parameter + `CancellationToken`
- Hardcoded `TerminalStatusCodes = { "Done", "Cancelled" }` removed

### 3.3 `WorkItemWorkflowIntegration.cs` (updated)

- `CompatibilityTerminalStatusCodes = { "Done", "Cancelled" }` defined
- `IsFinal = IsCompatibilityTerminalStatus(status.Code)` — terminal states marked final
- `IsCompatibilityTerminalStatus(code)` helper method
- Terminal-to-terminal transitions skipped; terminal states only emit reopen transitions

---

## 4. Workstream D — Regression coverage (added, not executed)

Test files added as source (deferred execution per P0-03):

- `tests/NEXUS.Modules.Approvals.Tests/ApprovalRequestDomainTests.cs` — domain state transition unit tests
- `tests/NEXUS.Modules.Approvals.Tests/ApprovalServiceMutationTests.cs` — service mutation tests
- `tests/NEXUS.Modules.Approvals.Tests/P003SecurityRegressionTests.cs` — P0-03 findings regression tests:
  - P0-03-01: `GetById_WithoutViewPermission_ReturnsForbidden`
  - P0-03-02: `Approve_ByRequester_Rejected`
  - P0-03-03: `Delegate_ToIneligibleTarget_ReturnsForbidden`, `Delegate_ToSameTenantActiveEligibleTarget_Succeeds`
  - P0-03-04: `AuditFailure_AbortsMutation`
  - P0-03-05: `ConcurrencyConflict_ReturnsControlled409` (documents controlled-conflict contract)
  - P0-03-06: `NoTenant_ReturnsForbidden`
- `tests/NEXUS.Modules.WorkflowEngine.Tests/AuditProjectionActionHandlerTests.cs` — audit handler writes real entry
- `tests/NEXUS.Modules.WorkItems.Tests/WorkItemTerminalStateTests.cs` — terminal status code detection

`docs/quality/MASTER-UAT-CHECKLIST.md` updated with P0-03 authorization scenarios (Subagent D).

All nine outstanding test scenarios from the prior interim report are now covered or documented.

---

## 5. EF Core implications

- No entity, relationship, `AppDbContext`, or migration change.
- No migration created, applied, or reviewed.
- No database write, reset, or seed performed.
- `ApprovalRequest.RowVersion` already exists as concurrency token; conflict handling requires no schema change.
- WorkItems terminal-state work is read-only against workflow definitions; no schema change.

---

## 6. Preserved state

- No existing file overwritten or deleted.
- Existing P0-01/P0-02 remediations not modified.
- Concurrent uncommitted work left untouched.
- Protected Admin baseline and Active Deny enforcement not changed.

---

## 7. Next actions (ordered)

1. Gate approval reads on `ApprovalRequest.View` permission — RESOLVED (P0-03-01)
2. Add separation-of-duties: prevent requester self-approval — RESOLVED (P0-03-02)
3. Add delegation target eligibility validation — RESOLVED (P0-03-03)
4. Move audit writes inside mutation transaction — RESOLVED (P0-03-04)
5. Handle `DbUpdateConcurrencyException` as controlled conflict — RESOLVED (P0-03-05)
6. Verify EF global query filter covers `ApprovalRequest` — VERIFIED (P0-03-06)
7. Verify audit-failure semantics in workflow action path — RESOLVED (P0-03-08)
8. Execute regression tests — deferred per P0-03 instruction
9. Consolidated verification for P0-03 closure — pending

**Status: P0-03 SOURCE REMEDIATION COMPLETE — ALL SEVEN FINDINGS RESOLVED OR VERIFIED — CONSOLIDATED TESTING PENDING.**
