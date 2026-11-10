# P0 Annual Budget Approval Inbox Recovery

**Date:** 2026-11-10  
**Status:** Assignment-only inbox and reviewer checks implemented; final API compilation remains unverified.

## Confirmed root causes

1. Annual Budget submission persisted an `ApprovalRequest` and `PaymentRequestApproverAssignment` rows, but no Annual Budget source was connected to the My Work approvals page. The shared generic inbox is keyed by `ApprovalRequest.CurrentApproverId`; parallel plans intentionally leave that field null, so the shared query cannot represent all assigned parallel reviewers.
2. The project-scope resolver omitted direct Sponsor assignments. The management middleware could reject the assigned Sponsor before the secured Annual Budget review workflow ran. See [Project Sponsor Visibility Recovery](../project/P0-PROJECT-SPONSOR-VISIBILITY-RECOVERY-2026-11-10.md).
3. The canonical `ProjectAnnualBudget.Approve` permission did not declare its `ProjectAnnualBudget.View` dependency.

The submission source stores `selection.UserId` in `PaymentRequestApproverAssignment.ApproverUserId`; this is the System User ID and does not require a Resource identity. Live Development assignment rows could not be inspected from this execution environment.

## Change

- Added `GET /api/my-work/annual-budget-approvals`, returning only pending actionable assignments for the authenticated user. It rebuilds that user's native assignment scope independent of a selected project, then checks each project through the canonical scope resolver.
- The query requires effective `ProjectAnnualBudget.View` and `ProjectAnnualBudget.Approve`, an active eligible reviewer, an exact pending assignment with no action recorded, and no requester/reviewer self-approval. Sequential future steps are excluded until prior steps approve; parallel reviewers with pending assignments are shown independently.
- Assigned-review reads and decisions use the normal project-scope gate; no assigned-review scope bypass remains. The existing workflow verifies the assigned reviewer and state/order again before the decision.
- Sponsor and Manager candidates resolve from `Project.SponsorId` / `ProjectManagerId` as System User IDs without Resource rows. Reviewer eligibility requires an active same-Tenant project participant, Sponsor, or Manager with effective View + Approve; role category no longer excludes an otherwise eligible reviewer. Project visibility is also category-independent.
- Added the canonical `ProjectAnnualBudget.Approve` requires `ProjectAnnualBudget.View` dependency. No role grants are automatically added. Category-independent custom-role grant behavior and protected Admin/Active Deny rules remain in force.
- Added an assignment-only My Work card with public Project ID navigation to Finance → Annual Budgets and the actionable fiscal year. Existing Finance layout and Project Overview behavior are unchanged.

## Regression coverage

Added focused tests for Sponsor review without a Resource, sequential current/future steps, parallel assignment-only inbox results, Active Deny, missing View, requester separation, project-scope rejection, and decision progression. Tests were included in the final filtered API test command, but no test bodies ran because the API build failed first.

## Verification and database status

| Check | Result |
|---|---|
| API build/test attempt | One filtered attempt. API compilation failed because the new notification publisher lacked the namespace import for `PaymentRequestStatuses`; the import was added afterward. The role-category eligibility filter was also removed afterward. Neither follow-up change was recompiled; no retry was run. |
| Focused API tests | Not run; test host was not reached. |
| Frontend focused tests | Vite stopped before loading tests with Windows `EPERM` while renaming its temporary dependency cache. No assertions ran. Locale JSON files parse. |
| Development database identity and affected records | Not verified; read-only SQL access was blocked by the execution environment's socket policy. No database writes or manual approval edits occurred. |
| EF model / pending migrations | Not run / live count unavailable. No persistence model change or migration was introduced by this recovery. |
| Backup / migrations / seed / physical schema / SQL Server persistence tests | Backup not requested; no migrations applied; seed not run; physical schema not verified; SQL Server persistence tests not run. |

## Handoff

Implementation is in place. The single final API build/test attempt compiled the shared kernel, modules, and Persistence, but failed while copying output assemblies into `NEXUS.Api/bin/Debug/net10.0`: MSBuild reported `MSB3021`/`MSB3027` because the destination DLLs were locked by another process. Focused API tests therefore did not execute, and no retry was run. Product Owner browser UAT remains pending: verify the assigned Admin/Sponsor sees only their own actionable sequential or parallel Annual Budget tasks, can open and decide the current step, and cannot see or act on an unassigned, future-step, self-requested, denied, or out-of-scope approval.

## P1 Annual Budget review details and lifetime-ceiling verification - 2026-11-10

### Review details and security contract

- The Annual Budget approval list remains an assignment-filtered My Work category. Its actionable list is separately protected by `ProjectAnnualBudget.Approve`; the service also requires effective `ProjectAnnualBudget.View` and scope.
- The review details and assigned history reads use `ProjectAnnualBudget.View`. They return only a request with a persisted assignment for the authenticated user, reject the requester/self-review path, and verify current project scope and Tenant identity. Reviewer names, order, states, actions, timestamps, and comments come from that request's stored assignment/action rows.
- `CanDecide` is computed independently from read access. It requires effective `ProjectAnnualBudget.Approve`, a currently active exact assignment with no prior action, an active eligible project participant, the correct Sequential/Parallel turn, and a writable context. Decision submission continues through the existing secured POST; the workflow rechecks assignment, row versions, scope, eligibility, separation of duties, and decision state before recording an action. Read-only/completed history cannot be acted on.
- The responsive Side Sheet distinguishes the lifetime Project ceiling from the Annual Budget under review and shows fiscal period, requester, status, the new-funding/carry-forward/adjustment breakdown, and reviewers' persisted progress. It supports completed assigned history in read-only mode. The existing `ProjectAnnualBudget.Approve` key and grants are unchanged; its catalog display copy now covers approve/reject/return. No schema change was made.
- Canonical permission matrix: assigned detail/history = `ProjectAnnualBudget.View` + same-Tenant assigned-user/project scope; actionable inbox = View + `ProjectAnnualBudget.Approve` + exact active assignment; Approve/Reject = the same checks plus current writable scope, current step, row versions, and requester separation. Active Deny remains authoritative. No role-name bypass or broad visibility fallback was introduced.

### Lifetime versus Annual Budget finding

- Source inspection found no Annual Budget approval assignment to `Project.Budget`. Initial approval updates the Annual Budget's status and approval metadata; revision approval updates the Annual Budget's annual funding and adjustments. The source assignment to `Project.Budget` is in the Project Change Request workflow after the ceiling change reaches its approved state; Project update mapping does not accept a direct Budget replacement.
- `ProjectOverviewService`, Project summary mappings, and the Finance foundation read the Project lifetime ceiling from `Project.Budget`; their annual allocation measures remain separate. Program/Portfolio financial rollups aggregate fiscal-year Annual Budget values and are not the Project lifetime ceiling.
- The reported live display replacement is therefore **not confirmed as a persistence mutation** from source. The Development database and authenticated browser could not be inspected in this workstream, so a bad live value or a browser consumer mapping remains unconfirmed. No existing financial history was rewritten, no Development row was repaired, and no migration was applied.

### Focused regression coverage and remaining verification

- Added backend workflow regressions for assigned active/future/completed details, View-only read access with actions disabled, Active Deny, requester/unassigned, foreign Tenant and out-of-scope access, terminal Reject versus Returned behavior, and preserving `Project.Budget` after Annual Budget approval.
- Added API authorization-contract coverage proving history/detail use canonical View without inheriting Approve, while the actionable list requires Approve, Approve depends on View, and Active Deny removes its effective authorization.
- The single final API build/test attempt did not reach test execution because API output assemblies were locked during copy (`MSB3021`/`MSB3027`); the added API test bodies remain unverified. Focused frontend checks passed 34/34 across seven files, and `npm run typecheck` passed. No configured Development database connection or authenticated browser session was available to verify the affected live ceiling value or rendering.
- Product Owner UAT must verify assigned active and completed requests in the sheet, sequential/parallel reviewer order, independent actions and notification refresh, and compare the Project ceiling before/after Annual Budget approval to the persisted Project and Change Request history. Only an approved Change Request may change the lifetime ceiling.
