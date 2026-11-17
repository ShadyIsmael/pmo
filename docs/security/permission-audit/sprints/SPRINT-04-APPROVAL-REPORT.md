# Sprint S4 — Approval Governance

**Date:** 2026-11-14  
**Result:** **PARTIAL - REQUEST/REVERSAL/CLOSING RECOVERY API AND UI SOURCE ADDED; RUNTIME VERIFICATION OPEN**

## Policy decisions

- Self-approval is denied by default for each request type. It may be permitted only through a saved, explicit request-type policy.
- Payment Request reviewers must come from the active project team and hold ProjectPaymentRequest.Approve. Submitted assignments remain the frozen approval plan.
- Duplicate actors, empty plans, invalid stage order, and plans with no eligible reviewer are rejected by existing policy and service checks.
- Annual Budget approval defaults and immutable submission snapshots remain unchanged.
- No reviewer stage is auto-approved or silently skipped. Recovery requires both `ProjectPaymentRequest.Approve` and `Governance.AssignmentRecovery.Manage`; every reassignment requires an audit reason.

## Source changes in this slice

- The Payment Request eligible-reviewer lookup omits the current requester.
- Payment Reversal eligible-reviewer lookup now omits its requester; submission rejects a crafted requester assignment, and list/detail projections keep CanDecide false for the requester. A focused lookup regression is authored but unrun.
- Submission validation rejects the requester if selected as an approver.
- The decision endpoint rejects the requester even if a historical or malformed assignment contains them.
- The response projection no longer advertises CanDecide to the requester.
- Added a policy regression test and English/Arabic validation messages.

## Recovery implementation

- Added `POST /api/projects/{projectId}/payment-requests/{requestId}/approver-reassignments`, requiring both `ProjectPaymentRequest.Approve` and `Governance.AssignmentRecovery.Manage` and a writable project scope.
- Reassignment is limited to Pending requests and one pending assignment with no recorded decision. Replacement must be a different active Project Team user with `ProjectPaymentRequest.Approve`, cannot be the requester, and cannot already appear in the approval plan. The request RowVersion and a 1–1000 character reason are required. Sequential `CurrentApproverId` follows the replacement when the active step is changed.
- The submission audit now stores the frozen initial approver IDs, positions, and requirements; the reassignment audit stores assignment ID, old/new approver IDs, and reason. No policy or key/SeedId was added and no migration is required.
- Payment Reversal has the analogous `/api/projects/{projectId}/payment-requests/{requestId}/reversals/{reversalId}/approver-reassignments` route with `ProjectPaymentReversal.Approve` and `Governance.AssignmentRecovery.Manage`; active eligible Project Team membership, requester exclusion, pending undecided assignment, version, reason, sequential state, and audit are enforced.
- Year-End Closing exposes recovery candidates, per-assignment eligibility flags, dual-permission reassignment, and dual-permission cancellation only when the assigned reviewer is ineligible and no replacement exists. Both Closing and shared ApprovalRequest row versions are required; reassignment/cancellation audit atomically.
- Request and Reversal UI recovery controls consume a per-assignment CanReassign projection and remain hidden unless a pending undecided assignee is ineligible and a distinct replacement exists. Closing recovery-options GET returns Conflict while the assignee remains eligible.
- Normal Closing preparer cancellation validates the Closing and ApprovalRequest RowVersions, marks pending assignments Cancelled, records ApprovalActions, and cancels the shared approval in the same serializable transaction. Latest API regressions remain unrun; WorkItems, Portfolio, and Persistence project builds pass, but the full API/React builds and browser behavior remain unverified.
- Annual Budget continues to use its authorized Withdraw path for pending submissions. Change Request rejects a non-Sponsor replacement for a mandatory Project Sponsor approval.

## Open work

- No persisted request-type self-approval policy or exception UI exists. The implemented behavior is deny-by-default; no exception path is configured.
- No saved request-type self-approval exception policy exists; self-approval remains denied by default where specified.
- New API recovery and sponsor-guard regression tests are authored but could not be executed. .NET SDK initialization fails with `Cannot open Service Control Manager` / `Win32Exception (5): Access is denied`.
- Payment Request/Reversal and Year-End Closing recovery controls are now present in the React finance screens. Reassignment requires current-assignee ineligibility; the Closing UI shows cancellation only for a genuinely blocked step with no replacement. Browser discoverability and RTL/accessibility behavior remain unverified.
- Approval eligibility, transitions, concurrency, and audit behavior have not been exercised against the API or database.

The focused frontend permission/access-security suites passed 39/39 and the React/Vite production build passed before the latest recovery UI/API eligibility corrections. Rerun tests/build on current source. API regression tests/build, database operations, and browser UAT were not completed.
