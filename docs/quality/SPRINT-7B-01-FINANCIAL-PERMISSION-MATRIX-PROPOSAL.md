# Sprint 7B-01 — Financial Permission Matrix Proposal (preparation only)

Status: **PROPOSED — not assigned.** This document prepares the Development UAT permission model for the financial capabilities named in Sprint 7B-01. No role, user, or permission grant was created, changed, or removed while producing it. No financial workflow was executed in Sprint 7B-01.

Prepared: 2026-11-01 · Environment target: Development tenant `11111111-1111-1111-1111-111111111111` · Owner approval required before any grant is applied.

## 1. Verified current state (read-only)

| Fact | Verified value | Source |
|---|---|---|
| Sensitive financial permission keys exist in the catalog | `ProjectAnnualBudget.Approve`, `ProjectAnnualBudget.Close`, `ProjectAnnualBudget.CloseApprove`, `ProjectAnnualBudget.CarryForward`, `PaymentSettlement.Record`, `ProjectPaymentReversal.Create`, `ProjectPaymentReversal.Approve`, `ProjectApproval.Override` | `SeedDataProvider` permission catalog; `Permissions` table |
| Role grants for those keys | **None.** Every listed key has zero role assignments | `RolePermissions` join (read-only) |
| Tenant `admin` exclusion | The seeder explicitly excludes these keys (plus `ProjectApproval.Override`) from the `admin` role | `SeedDataProvider` admin exclusion block |
| `finances.read` / `finances.write` holders | `admin`, `executive`, `finance.controller`, `pmo.admin`, `project.manager` | `RolePermissions` (read-only) |
| API policy composition | Each financial action requires `finances.write` **and** its operation-specific claim (see §2) | `Program.cs` authorization policies |
| Delegation-authority enforcement | Role updates are protected by `users.write` only; the role service does not verify that the editor holds the permissions being granted | `RoleAdministrationService` |

Consequence: financial operations are currently unreachable by any role. That is the intended "no automatic sensitive grant" posture, and it must be closed by an explicit, approved Development UAT delegation — not by widening a system role.

## 2. Capability → policy → proposed UAT permission set

Every row below is a proposal. `finances.read` is required on all read routes and `finances.write` on all write routes; the operation key is additive, never a substitute.

| # | Capability | API policy | Required claims | Proposed Development UAT identity | Proposed role (new, UAT-only) |
|---|---|---|---|---|---|
| 1 | Read budgets, requests, settlements, reversals, closing statements | `Finances.Read` | `finances.read` | `uat7c.reader@nexus.com` | `uat7c.finance.reader` |
| 2 | Annual Budget approval (decision on a submitted budget) | `ProjectAnnualBudgets.Approve` | `finances.write` + `ProjectAnnualBudget.Approve` | `uat7c.budget.approver@nexus.com` | `uat7c.budget.approver` |
| 3 | Record actual settlement | `PaymentSettlements.Record` | `finances.write` + `PaymentSettlement.Record` | `uat7c.settlement@nexus.com` | `uat7c.settlement.recorder` |
| 4 | Create/submit Payment Request reversal | `ProjectPaymentReversal.Create` | `finances.write` + `ProjectPaymentReversal.Create` | `uat7c.reversal.requester@nexus.com` | `uat7c.reversal.requester` |
| 5 | Approve/reject reversal | `ProjectPaymentReversal.Approve` | `finances.write` + `ProjectPaymentReversal.Approve` | `uat7c.reversal.approver@nexus.com` | `uat7c.reversal.approver` |
| 6 | Prepare/submit/cancel Year-End Closing statement | `ProjectAnnualBudgets.Close` | `finances.write` + `ProjectAnnualBudget.Close` | `uat7c.closing.preparer@nexus.com` | `uat7c.closing.preparer` |
| 7 | Approve/reject closing statement | `ProjectAnnualBudgets.CloseApprove` | `finances.write` + `ProjectAnnualBudget.CloseApprove` | `uat7c.closing.approver@nexus.com` | `uat7c.closing.approver` |
| 8 | Authorize free/commitment Carry Forward amounts | `ProjectAnnualBudgets.CarryForward` | `finances.write` + `ProjectAnnualBudget.CarryForward` | `uat7c.carry.authorizer@nexus.com` | `uat7c.carry.authorizer` |
| 9 | One-submission approval override (only for AB-APR-04) | service-level check | `finances.write` + `ProjectApproval.Override` | `uat7c.override.tester@nexus.com` | `uat7c.override.tester` |

Notes:

- Rows 6–8 are deliberately split. A single actor holding all three would make the separation-of-duties scenarios untestable.
- The Carry Forward authorizer still needs `ProjectAnnualBudget.Close` or `CloseApprove` to reach the step where carry is decided; grant only the one required by the chosen flow and record the decision.
- `finances.write` is broad. Where the API requires it, the operation key remains the discriminating control; the UAT roles must not be copied into production roles.

## 3. Separation-of-duties constraints (must hold in UAT)

| Constraint | Enforcement point | UAT proof |
|---|---|---|
| Budget submitter ≠ budget approver | Approver assignment + approval request rules | Same actor attempts self-approval → denied; assigned approver succeeds |
| Annual Budget approver must be an assigned, active Project Team member with effective permissions | Submission-time eligibility + decision-time recheck | Unassigned, inactive, cross-tenant, and permission-less actors denied |
| Reversal requester ≠ reversal approver | Workflow assignment | Requester attempts direct approve → denied |
| Closing preparer ≠ closing approver | Approval plan + preparer identity check | Preparer cannot approve; assigned approver can |
| Carry Forward amounts are explicit, never defaulted | Closing service | Zero/partial/full carry produce distinct persisted facts |
| No role name grants capability | Policy + scope middleware | An actor with only `users.write` or only membership is denied |

## 4. Grant procedure (when approved)

1. Owner approves this matrix and names the authorizing actor (the only actor allowed to perform the grants).
2. Create the UAT-only roles in Roles & Permissions with **only** the listed claims; no system role is edited.
3. Assign the UAT roles to the listed Development identities; keep `.env`/password handling outside reports.
4. Capture before/after evidence: `GET /api/roles`, `GET /api/users/{id}`, and the audit entries for role creation and assignment.
5. Run the financial UAT scenarios, then execute the revocation step in §6.
6. Record every step in the Master UAT Checklist execution record.

Prohibited during UAT: editing `admin`, `executive`, `finance.controller`, `pmo.admin`, or `project.manager` to add sensitive keys; direct SQL inserts into `RolePermissions`/`UserRoles`; sharing one identity across two duty roles; committing credentials or tokens to the repository.

## 5. Required evidence per financial capability

| Capability | Minimum evidence |
|---|---|
| Annual Budget approval | Draft → submitted assignment snapshot → approver decision API 200, unauthorized decision 403, balances before/after, audit rows |
| Settlement | Settlement API 2xx, `PaymentSettlements` row, paid/outstanding before/after, closed-period rejection |
| Reversal | Requester 201 / approver 200, released amount, original request unchanged, requester-approve denial, unreleased-balance rejection |
| Year-End Closing | Prepared statement totals reconciling budget/allocations/paid/outstanding, closed-period lock, reject path leaves balances unchanged, single closing per Project/FY |
| Carry Forward | Explicit amount persisted, source balance reduction, destination opening row, no double deduction, carry does not consume lifetime ceiling twice |
| Negative/delegation | Actor lacking `finances.write` or the operation key denied on UI and direct API; no role-name bypass |

## 6. Cleanup and rollback

- Revoke the UAT role assignments from the UAT identities, then remove the UAT-only roles (or mark them inactive if audit retention requires the role row).
- Keep audit entries, financial documents, and scenario data; do not delete financial history.
- Re-verify `RolePermissions` for the eight sensitive keys returns to zero grants and that no system role changed.
- Publish the revocation evidence alongside the execution record.

## 7. Open decisions before execution

| # | Decision | Why it blocks UAT |
|---|---|---|
| 1 | Approve or reject the split of preparer/approver identities in §2 | Without the split, separation-of-duties cases are unprovable |
| 2 | Name the authorizing actor for the grants | No delegation-authority check exists in the role service; the grant must be traceable |
| 3 | Confirm the Year-End Closing flow's required claim (`Close` vs `CloseApprove`) for the Carry Forward step | Determines whether the carry authorizer also needs a closing claim |
| 4 | Decide whether `ProjectApproval.Override` is in UAT scope | It is a ninth sensitive key and is excluded from `admin` as well |
| 5 | Decide the follow-up for the project.manager `Program\|View` divergence (F-01 in the Master UAT Checklist) | Affects who can open Program financial rollups during UAT |
