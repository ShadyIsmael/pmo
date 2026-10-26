# Entity Assignment Recovery

## Purpose

The React Administration → Governance workspace lets authorized tenant administrators discover and recover Projects, Programs, and Portfolios whose manager or Sponsor assignment is missing, inactive, inconsistent, or awaiting acceptance. It uses the existing handover and Project Sponsor domain services; it is not a second manager-assignment system.

## Security boundary

- Every recovery query uses the authenticated tenant claim and entity `PublicId`. Internal entity keys are resolved only inside that tenant and are never accepted as the route identifier.
- The three `Governance.AssignmentRecovery.*` permissions are separate grants: `View`, `Initiate`, and `Manage`. Reads require any of the three; manager candidate lookup needs `Initiate` or `Manage`; state changes require their specific permission and, for manager initiation, `Handover.AdminInitiate`.
- Effective permissions are reloaded from persistence by the recovery service. The authenticated actor and selected manager/Sponsor must be active in the same tenant.
- The Management Context exception is limited to `/api/governance/assignment-recovery` in Native mode and allows deliberate tenant-wide administrative discovery there only. It does not bypass tenant validation, the controller/service permission checks, or object authorization on ordinary project/program/portfolio routes. ReadOnlyViewAs is not allowed to mutate recovery state.
- Query cache entries are namespaced under the administration/recovery key and mutations invalidate recovery results, My Approvals, management perspectives, and the authenticated-user permission query.

## Assignment behavior

- **Initial manager:** a request is created through the existing Project, Program, or Portfolio handover service. The manager field and management scope change only when the incoming manager accepts through My Approvals. A pending request is visible with its proposed manager and a direct link to approvals.
- **Administrative handover:** an existing manager can be replaced only through the existing request/acceptance flow. It does not overwrite the current manager when the request is submitted.
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

## Administration navigation and permission refresh

- The entry is shown at **Administration → Governance → Entity Assignment & Recovery** and links to `/administration/governance/entity-assignment-recovery`.
- Both the Governance index and the recovery route refresh `/api/auth/me` through the existing `AuthProvider.refreshUser` flow before evaluating their effective-permission guards. This prevents an old in-memory permission snapshot from hiding the card or incorrectly denying a direct route after a role grant changes. Refresh failure is visible and retryable; the route remains denied until refreshed permissions authorize it.
- The `admin` role's seed configuration attaches all permissions in the catalog, including newly registered recovery permissions. This is provisioning behavior, not proof of the currently stored database grants.

## Verification status

Live verification completed on 2026-10-21 against the local API (`http://localhost:5000`), React development server (`http://127.0.0.1:4100`), and SQL Server database `NexusPmo`.

### Functional acceptance

- **PASS — Administrator access:** stored admin grants include `Governance.AssignmentRecovery.View/Initiate/Manage` and `Handover.AdminInitiate`.
- **PASS — Discovery:** missing-manager and rejected filters return the expected Project/Program/Portfolio data with PublicIds.
- **PASS — Project initial manager:** the incoming manager accepted through My Approvals; `ProjectManagerId`, current `ProjectManagerAssignments`, project access, and the Project Team governance row were synchronized.
- **PASS — Rejection:** the request was rejected with a reason; the project remained unassigned and appeared in the rejected filter.
- **PASS — Program initial manager:** the incoming manager accepted; direct `ProgramManagerId` and current assignment were persisted and Program access succeeded.
- **PASS — Portfolio handover:** initial assignment and administrative handover both used the request/acceptance flow; the old assignment was closed and the new owner/current assignment persisted.
- **PASS — Project Sponsor:** Sponsor recovery updated `Project.SponsorId`, synchronized the governance membership projection, rejected duplicates with 409, and wrote the recovery audit entry.
- **PASS — React navigation and approvals:** the admin entry route renders; a pending recovery request appears in My Approvals; approving from the browser completes the request. Recovery is hidden and denied for a non-recovery user.
- **PASS — Security:** unauthenticated 401, non-recovery user 403, ReadOnlyViewAs recovery mutation 403, invalid entity 400, cross-tenant 404, duplicate/invalid state 409/400, and a non-addressee acceptance attempt 403.

### Regression checks

- **PASS** — focused backend recovery suite: 11/11.
- **PASS** — focused backend portfolio/program handover suite: 21/21, including `Portfolio.View` candidate and initiation rejection coverage.
- **PASS** — focused backend management-scope suite: 1/1 for direct Program/Project context union with inherited Portfolio scope.
- **PASS** — focused React recovery, governance navigation, approvals, and approval-review tests: 17/17.
- **PASS** — React `tsc -b`, ESLint (0 errors, 2 existing hook warnings), and production build.
- **PASS** - React i18n EN/AR parity (5,238 leaf keys) and used-key validation (3,418 literal keys).
- **PASS** — full React suite: 127/127 files and 626/626 tests.
- **PASS** — full backend suite: 434/434 tests.

### Residual data

Verification entities and requests remain in the Development database and are recorded in `artifacts/recovery/recovery-test-entities.json` and `recovery-test-requests.json`. No production data or production configuration was changed.
