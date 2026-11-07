# Entity Assignment Recovery

## Purpose

The React Administration → Governance workspace lets authorized tenant administrators discover and recover Projects, Programs, and Portfolios whose manager or Sponsor assignment is missing, inactive, inconsistent, or awaiting acceptance. It uses the existing handover and Project Sponsor domain services; it is not a second manager-assignment system.

## Security boundary

- Every recovery query uses the authenticated tenant claim and entity `PublicId`. Internal entity keys are resolved only inside that tenant and are never accepted as the route identifier.
- The three `Governance.AssignmentRecovery.*` permissions are separate grants: `View`, `Initiate`, and `Manage`. Reads require any of the three; manager candidate lookup needs `Initiate` or `Manage`; state changes require their specific permission and, for manager initiation, `Handover.AdminInitiate`.
- The selected Project's read-only initial-manager summary requires effective `Governance.AssignmentRecovery.Initiate` and `Handover.AdminInitiate`. The service reloads effective permissions, so an Active Deny is honored; this does not require `Project.Edit` or broad `Project.View`.
- Effective permissions are reloaded from persistence by the recovery service. The authenticated actor and selected manager/Sponsor must be active in the same tenant.
- The Management Context exception is limited to `/api/governance/assignment-recovery` in Native mode and supports deliberate tenant-wide recovery discovery and authorized recovery actions there. It does not bypass tenant validation, the controller/service permission checks, or object authorization on ordinary project/program/portfolio routes. `ReadOnlyViewAs` cannot use the Native recovery exception or mutate recovery state.
- Query cache entries are namespaced under the administration/recovery key and mutations invalidate recovery results, My Approvals, management perspectives, and the authenticated-user permission query.

## Assignment behavior

- **Initial manager:** a request is created through the existing Project, Program, or Portfolio handover service. The manager field and management scope change only when the incoming manager accepts through My Approvals. A pending request is visible with its proposed manager and a direct link to approvals.
- **Project summary:** after a Project is selected for initial-manager assignment, the sheet shows a read-only projection of Project name/code, description or purpose, planned start/end dates, and Sponsor name. It exposes no Project edit controls and does not alter assignment validation or the existing handover/acceptance action. Missing values use localized unavailable/not-assigned labels; a summary is shown only when its `PublicId` matches the selected Project.
- **Administrative handover:** an existing manager can be replaced only through the existing request/acceptance flow. It does not overwrite the current manager when the request is submitted.
- **Project manager membership alignment:** acceptance updates `Project.ProjectManagerId`, the current `ProjectManagerAssignment`, request state, and audit/history in one serializable transaction. The required `ProjectManagerMembershipSynchronizer` runs for both Initial Assignment and handover. When the assigned User has a linked same-tenant Resource, one active Project Manager `ProjectMember` is maintained; a previous PM-only membership is ended with its history retained, while independent active team roles remain intact. When no Resource is linked, no User or Resource is created and no invalid ProjectMember is written; the Project People read model represents the assigned User as a Governance Project Manager row. Project access continues to derive from the authoritative manager assignment and existing effective permissions, including Active Deny.
- **Project Sponsor:** an active same-tenant user is written to `Project.SponsorId`, the existing Sponsor membership projection is synchronized, and the change is audited in the same transaction. Recovery only fills a missing Sponsor; replacement remains outside this feature.
- **Reconciliation:** for each entity, an active same-tenant direct manager is preferred; otherwise a valid active current assignment is retained; if neither is valid the entity is left unassigned. Existing current assignment rows are closed, history is preserved, audit is written, and for Projects stale PM memberships are ended while non-PM team roles are retained. Concurrent conflicts return a refresh/retry response.
- **Hierarchy:** discovery flags missing Project parent hierarchy; this screen does not invent parent Portfolio/Program relationships or change Management Scope behavior elsewhere.

## Persistence

`Programs.PublicId` and `Portfolios.PublicId` are generated UUIDs with database unique indexes, matching the existing Project public identifier. Migration `20261021160000_AddGovernanceRecoveryPublicIds` adds the columns with SQL Server `NEWID()` defaults so existing rows receive values. The migration was applied to the local Development database on 2026-10-21 after a verified backup; existing rows received PublicIds and both unique indexes were confirmed.

## Management scope behavior

- The recovery screen does not bypass ordinary management scope. When a user has inherited Portfolio scope and also directly manages a Program or Project outside that portfolio, the Program and Project perspective context lists union the inherited scope with the direct manager assignments. This keeps directly managed entities selectable after a higher perspective is added.
- Portfolio manager candidates, and portfolio handover initiation, require `Portfolio.View` in addition to `Handover.Approve`/`Handover.AdminInitiate`. A project manager without Portfolio.View cannot be assigned as a Portfolio Manager through recovery or the standard handover flow.

## API

See [API endpoints](../api/endpoints.md#entity-assignment-recovery) for the route and permission matrix.

| Operation | Effective permissions | Scope and data |
|---|---|---|
| `GET /api/governance/assignment-recovery/projects/{publicId}/summary` | `Governance.AssignmentRecovery.Initiate` + `Handover.AdminInitiate` | Existing Native-mode tenant-wide Assignment Recovery exception; authenticated tenant plus Project `PublicId`; returns only `PublicId`, name, code, description, start/end dates, and Sponsor name. Cross-tenant or unavailable IDs return non-disclosing not-found. `ReadOnlyViewAs` cannot use the tenant-wide Native exception; ordinary Project scope still applies. |
| `POST /api/governance/assignment-recovery/{entityType}/{publicId}/initial-assignment` | `Governance.AssignmentRecovery.Initiate` + `Handover.AdminInitiate` | Existing canonical handover service validates the selected entity and active same-tenant incoming manager. Initial Project manager assignment remains pending until the incoming manager accepts. No `Project.View` or `Project.Edit` is required for this recovery action. |

The initial-assignment sheet enables manager selection and submission only after the selected Project's matching summary has loaded successfully. Loading, retryable failure, empty response, and missing fields are localized. The summary request is read-only and does not change Project state.

## Administration navigation and permission refresh

- The entry is shown at **Administration → Governance → Entity Assignment & Recovery** and links to `/administration/governance/entity-assignment-recovery`.
- Both the Governance index and the recovery route refresh `/api/auth/me` through the existing `AuthProvider.refreshUser` flow before evaluating their effective-permission guards. This prevents an old in-memory permission snapshot from hiding the card or incorrectly denying a direct route after a role grant changes. Refresh failure is visible and retryable; the route remains denied until refreshed permissions authorize it.
- The `admin` role's seed configuration attaches all permissions in the catalog, including newly registered recovery permissions. This is provisioning behavior, not proof of the currently stored database grants.

## Verification status

### Project summary projection — 2026-11-04

- **PARTIAL — Focused React recovery tests:** 7/7 passed in the first run, covering read-only summary rendering, missing-value placeholders, loading gate, retryable error, and stale-summary prevention after switching Projects. A final empty-summary submit-guard test was added afterward and awaits rerun.
- **NOT RUN — Focused API tests:** the test build failed before test execution because the .NET 10.0.302 workload locator SDK directories `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` and `Microsoft.NET.SDK.WorkloadManifestTargetsLocator` are missing from the installed SDK.
- **NOT RUN — Authenticated Development UI/API UAT:** no live request or browser interaction was performed for the new projection.

### Current Project Manager consistency issue - 2026-11-04 (OPEN)

The earlier assignment-recovery checks do not close the reported inconsistency across Admin/Governance, Project Details, Project Team, the manager's My Projects list, and effective Project access after reload. The source cause and regression coverage are now recorded below; authenticated Development verification and existing-record diagnosis remain open for owner testing.

- **SOURCE COMPLETE - Root cause:** `ProjectSnapshotService.CompleteHandoverAsync` previously called the membership synchronizer only when a previous manager existed, so accepted Initial Assignment skipped Project Team membership synchronization. The authoritative manager field, current assignment, approval/acceptance lifecycle, and transaction were traced; Project Details and My Projects use the persisted manager identity/same-tenant current assignment. Project Team uses the `/people` projection, including a User-backed Governance row when the manager has no Resource.
- **REGRESSION COVERAGE ADDED - API TEST NOT RUN:** `ProjectHandoverLifecyclePersistenceTests` covers accepted Initial Assignment with and without a linked Resource, accepted handover membership/history behavior, idempotent synchronization, and merging the Project Manager relationship into an existing active Team Member row without duplicating the person in Project Team. The React handover approval test covers invalidation of Project Details, shell, overview, list, people, and team caches; the Project Team test covers the unlinked-Resource Governance row and localized label. The two focused React tests had passed 9/9 and 20/20 before the owner asked to take over testing. No tests were run after that request.
- **PENDING - Development runtime and persisted reload:** authenticated Development workflow and SQL Server reload evidence showing the same current manager across all five views were left for owner testing. The later read-only evidence recorded below establishes database identity and orphan-reference counts only, not UI/API parity.
- **PENDING - Inconsistent record diagnosis/reconciliation:** a later read-only check identified four active Projects and four current assignment rows referencing missing Users, as detailed below. Individual referenced User IDs, row-by-row diagnosis, and reconciliation remain unverified. No records were modified. Any repair must use the authorized Governance reconciliation/application flow and preserve audit and assignment history.
- **DATABASE STATUS:** latest Development identity evidence is Shady/NexusPmo, DB_ID 5, ONLINE; model synchronization, pending migration count, and physical schema were not verified; no migrations were applied; backup was not verified and no write was performed; persistence tests were left to owner testing.

The PASS results below are historical evidence from 2026-10-21. They describe the scenarios exercised at that time and must not be treated as evidence that the current five-view consistency issue passes.

### Historical live verification - 2026-10-21

The historical run used the local API (`http://localhost:5000`), React development server (`http://127.0.0.1:4100`), and SQL Server database `NexusPmo`.

#### Historical functional acceptance

- **PASS — Administrator access:** stored admin grants include `Governance.AssignmentRecovery.View/Initiate/Manage` and `Handover.AdminInitiate`.
- **PASS — Discovery:** missing-manager and rejected filters return the expected Project/Program/Portfolio data with PublicIds.
- **PASS — Project initial manager:** the incoming manager accepted through My Approvals; `ProjectManagerId`, current `ProjectManagerAssignments`, project access, and the Project Team governance row were synchronized.
- **PASS — Rejection:** the request was rejected with a reason; the project remained unassigned and appeared in the rejected filter.
- **PASS — Program initial manager:** the incoming manager accepted; direct `ProgramManagerId` and current assignment were persisted and Program access succeeded.
- **PASS — Portfolio handover:** initial assignment and administrative handover both used the request/acceptance flow; the old assignment was closed and the new owner/current assignment persisted.
- **PASS — Project Sponsor:** Sponsor recovery updated `Project.SponsorId`, synchronized the governance membership projection, rejected duplicates with 409, and wrote the recovery audit entry.
- **PASS — React navigation and approvals:** the admin entry route renders; a pending recovery request appears in My Approvals; approving from the browser completes the request. Recovery is hidden and denied for a non-recovery user.
- **PASS — Security:** unauthenticated 401, non-recovery user 403, ReadOnlyViewAs recovery mutation 403, invalid entity 400, cross-tenant 404, duplicate/invalid state 409/400, and a non-addressee acceptance attempt 403.

#### Historical regression checks

- **PASS** — focused backend recovery suite: 11/11.
- **PASS** — focused backend portfolio/program handover suite: 21/21, including `Portfolio.View` candidate and initiation rejection coverage.
- **PASS** — focused backend management-scope suite: 1/1 for direct Program/Project context union with inherited Portfolio scope.
- **PASS** — focused React recovery, governance navigation, approvals, and approval-review tests: 17/17.
- **PASS** — React `tsc -b`, ESLint (0 errors, 2 existing hook warnings), and production build.
- **PASS** - React i18n EN/AR parity (5,238 leaf keys) and used-key validation (3,418 literal keys).
- **PASS** — full React suite: 127/127 files and 626/626 tests.
- **PASS** — full backend suite: 434/434 tests.

#### Historical residual data

Verification entities and requests remain in the Development database and are recorded in `artifacts/recovery/recovery-test-entities.json` and `recovery-test-requests.json`. No production data or production configuration was changed.

## Pending request discovery and participant details — 2026-11-04

Assignment Recovery remains a discovery and initiation surface over the canonical Project handover workflow. Its Project row carries the persisted handover request's internal `RequestId`, lifecycle status, requester/proposed manager, and the authenticated actor's participant flags. The entity route continues to use the Project `PublicId`; only the nested request details route uses the handover request's internal ID.

`GET /api/handover-requests/{requestId}/details` returns the bounded existing `ProjectHandoverRequestDto` only when the authenticated same-tenant actor is the requester or proposed incoming manager and has effective `Handover.View`. The route must recheck the current effective permission, including Active Deny. A missing or denied `Handover.View` is forbidden; a nonparticipant, foreign-tenant, missing, or soft-deleted request is returned as non-disclosing not-found. The existing `/review` route remains the assigned incoming manager's approval review path and continues to require `Handover.Approve` and a pending request.

The recovery page distinguishes a request initiated by the current actor, one assigned to the current actor, an active request awaiting another manager, and terminal request history. Both `Pending` and `ApprovedScheduled` are active blockers: initial-assignment controls stay hidden and the persisted status/proposed manager remain visible until the scheduled transition completes. It offers **View Existing Request** only when the caller is an authorized participant. Only an authorized incoming manager of a pending request receives a link to the approval review route. Initiators can inspect status and request details without gaining decision authority. My Approvals remains the assigned incoming manager's pending work list; Assignment Recovery exposes participant details to authorized request initiators and reviewers.

Conflict prevention remains active. One non-deleted Project handover may be pending or approved for a future date; accepted, rejected, cancelled, and soft-deleted request history does not block a later eligible operation. A Project with an assigned manager receives `PROJECT_ALREADY_ASSIGNED` and must use administrative handover. A non-deleted current manager-assignment history row requires reconciliation and receives `PROJECT_ASSIGNMENT_RECONCILIATION_REQUIRED`; soft-deleted rows do not create that conflict. Duplicate requests return the stable `HANDOVER_REQUEST_ALREADY_PENDING` or `HANDOVER_REQUEST_ALREADY_SCHEDULED` code with localized business text. Program and Portfolio conflicts use matching type-specific assignment/reconciliation codes.

### Verification status

- **NOT RUN — Focused API regression suite:** request-visibility, stable-conflict, active-request, closed-request, and stale-assignment cases were added. `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~AssignmentRecoveryServiceTests|FullyQualifiedName~ProjectHandoverLifecyclePersistenceTests" -m:1 --verbosity minimal` compiled dependencies but failed before test execution when the API output copy hit locked `NEXUS.Modules.ProjectManagement.dll` and `NEXUS.Persistence.dll` files (MSB3027/MSB3021). The files were in use by another process. An earlier attempt stopped on the missing `StatusCodes` import; that import was then added by the backend owner.
- **NOT RUN — Focused React regression suite:** the `ApprovedScheduled` guard and regression case are present. `npm --prefix apps/web-react test -- src/test/assignment-recovery.test.tsx` failed while loading Vitest config with `spawn EPERM` from esbuild; test execution did not start. The frontend owner reports `npm run typecheck` passed.
- **NOT RUN — Authenticated Development API and row-level SQL verification:** the current request status, responsible users, and persisted/UI parity have not been verified in this run. The latest read-only identity and orphan-reference evidence is recorded below.
- **PARTIALLY VERIFIED — Development database target and backup:** the latest user-provided read-only identity check reports Shady/NexusPmo, DB_ID 5, ONLINE. An independent backup was not verified. Earlier documentation states DB_ID 9; that value conflicts with and is superseded for the latest check by the evidence below. No database write, cleanup, or history change was made.
- The 401 authentication response remains separate from the 409 business conflict. It should be attributed to this incident only if the authenticated session itself is shown to be invalid.

## Latest Development read-only evidence and regression gaps — 2026-11-04

The latest evidence supplied for this workstream is from a read-only SQL identity/data check. It reports `SERVERPROPERTY=Shady`, `DB_NAME=NexusPmo`, `DB_ID=5`, `ONLINE`. Earlier documentation cites DB_ID 9; retain that as historical/conflicting documentation only, not as the identity established by this latest check.

The same read-only evidence reports four active Project rows—PRJ-1001, PRJ-1006, PRJ-1007, and PRJ-1010—whose manager references resolve to no User row, plus four current Project Manager assignment rows that reference missing Users. No foreign key exists from `Projects.ProjectManagerId` or `ProjectManagerAssignments.ProjectManagerId` to `Users.Id`; the check also found zero Projects with multiple current assignment rows. This is an observed data-integrity condition, not evidence that recovery or handover succeeds or fails for those rows. No user identifiers or API responses were supplied for these references. No authenticated API/runtime verification, row-by-row comparison, reconciliation, repair, or database write was performed.

### Source and test inventory reviewed

- Initial assignment is expected to use the canonical handover workflow and leave the manager unset until acceptance. Relevant existing regression cases include `InitialManagerRecovery_UsesCanonicalHandoverAndLeavesManagerUnsetUntilAcceptance` and `AdministratorInitialAssignment_StaysUnassignedUntilIncomingManagerAccepts`.
- Accepted assignment/handover membership behavior has cases for one Project Manager membership when a Resource is linked, a User governance projection with no synthetic Resource when unlinked, preserving independent team membership, and idempotent replacement: `AcceptedInitialAssignmentCreatesOneProjectManagerMembershipForLinkedResource`, `AcceptedInitialAssignmentWithoutResourceUsesUserGovernanceProjectionAndCreatesNoIdentity`, `ProjectPeopleMergesGovernanceManagerWithExistingTeamMembership`, and `CompletedHandoverEndsPreviousManagerMembershipAndSynchronizesNewManagerIdempotently`.
- Duplicate initial assignment cases distinguish Pending and ApprovedScheduled conflicts and prevent duplicate persistence: `InitialAssignmentUsesDistinctPendingAndScheduledConflictCodes`. Direct management combined with inherited Portfolio scope is covered by `PerspectiveSnapshot_UnionsDirectManagerContextsWithInheritedPortfolioScope`.
- Permission/tenant tests cover the initial summary's required effective permissions, Active Deny, tenant boundaries, and handover authorization. These are source/test inventory observations only; no passing test result is claimed here.
- No matching test was identified for an already-persisted Project/current assignment row pointing to a missing User. The observed four-plus-four orphan references therefore remain an explicit regression and data-reconciliation gap. Test files already changed alongside the assignment/handover implementation were left untouched by this workstream.

Test execution result for the focused assignment/handover suites was not verified in this update. Do not treat the listed regression tests, historical 2026-10-21 PASS results, or the database identity check as authenticated Development UAT or as evidence that the orphan references were reconciled.
