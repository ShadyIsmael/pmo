# Lifecycle Bootstrap 409 and Project Team Capacity 403 Recovery

**Date:** 2026-11-17  
**Status:** Source fixes, focused API/React regressions, typecheck, and production builds passed. Authenticated Browser UAT and actor-specific 403 evidence remain open.

## Findings

### Lifecycle Options 409

`ManagementProvider` does not call `/api/project-lifecycle/options`. Its authenticated bootstrap fetches `/api/me/perspectives` and resolves management context. The request observed alongside that bootstrap came from `ProjectsListPage`: the page fetched lifecycle options without an Organization or Project solely to populate the status filter. That timing made the request appear to be part of login initialization.

The unscoped non-create service branch previously reached the protected default Pre-Initiation check with `organizationId == null`. If the catalog was configured for Organizations rather than the Tenant, the check failed and returned HTTP 409 `CONFLICT` with this source-defined message:

> The protected Pre-Initiation phase is missing or misconfigured in this scope. Lifecycle setup must be repaired before reading lifecycle options.

The incident's raw response body was not supplied or independently captured; the message above is the exact current source branch that produced the reported conflict.

The service now rejects non-create options requests that have neither an explicit Organization nor an in-scope Project with HTTP 400 `VALIDATION_ERROR` and message `Project lifecycle options require an Organization or in-scope Project context.` This check runs before the protected-phase check. `ProjectsListPage` no longer requests catalog options; it filters using status ID/code and bilingual names projected on the visible Project summary rows. Its UI does not fabricate catalog defaults or issue an unscoped catalog retry.

Create Project is unchanged in its approved scope model: before Portfolio selection the create query remains status-only and phase-empty; after selection the server derives the Organization from the authorized active Portfolio and reads that Organization's lifecycle catalog. No Tenant-catalog fallback or protected Pre-Initiation recreation was added. Existing Organization/Project-scoped requests retain the 409 safeguard when their protected phase is actually missing.

### Lifecycle options caller inventory

Active callers of the `features/projects/api/project-lifecycle.ts` hook:

- `ChangePhaseSettings.tsx` — Project ID.
- `CreateProjectSheet.tsx` — `purpose=create`, selected Portfolio and optional Program; Organization is derived server-side.
- `ProjectStatusChangeSideSheet.tsx` — Project public ID.
- `ProjectPhaseGateSideSheet.tsx` — Project ID.
- `ProjectGovernanceTabs.tsx` — Project ID.
- `ProjectSnapshotsTab.tsx` — Project ID.
- `ProjectsListPage.tsx` — removed; this was the only unscoped caller.

`features/administration/api/projectLifecycle.ts` contains an older duplicate hook definition with no active consumer found in the frontend source. It now also requires Organization or Project context for non-create requests. The separate `management.tsx` bootstrap remains a `/api/me/perspectives` request and is not a lifecycle options caller.

## Project Team Capacity 403

The Project page resolves `GET /api/projects/{publicId}/shell` before rendering its nested Overview route. That shell response comes from the persisted Project through the server's in-scope Project query. The Team Capacity request is issued only by the rendered Overview, so a normal navigation to this page means the Project existed and was readable before the capacity request. The source path establishes this ordering; the reported live request's shell response was not independently captured in this investigation.

`ProjectController.GetTeamCapacityOverview` has both `[Authorize(Policy = "Project.View")]` and `[Authorize(Policy = "Resource.View")]`. ASP.NET Core combines multiple authorization attributes, so both permissions must pass. `UserPermissionClaimsMiddleware` discards JWT permission snapshots and resolves current effective permissions per request; an Active Deny or revoked grant therefore removes the required claim before policy evaluation. Project scope middleware/service queries return 404 for out-of-scope or missing Projects. `ProjectOverviewService` resolves the Project and team before capacity calculation, and the capacity calculation itself has validation/unavailable branches but no `Error.Forbidden` branch. The other source-defined 403 path is invalid or unavailable explicit management context rejected by `ManagementContextMiddleware`.

The API policy design is consistent with least privilege; there is no source evidence that RBAC should be weakened or that an automatic grant is appropriate. A user with `Project.View` but without current `Resource.View` can correctly open the Project and receive 403 for Team Capacity. The exact reported actor's current Organization-effective permissions, Active Deny state, management headers, response body, and server request log were not provided, so this investigation cannot distinguish a correct missing-permission deny from an invalid-context runtime defect for that particular request. With both effective permissions and a valid current context, the endpoint is expected to return 200.

The frontend now starts the query only when the authenticated permission snapshot includes both keys, disables query retries and retry-on-remount, and renders a localized unavailable state when the permission is absent or the API returns 403. It hides cached capacity data after denial and offers no disruptive Retry action. The API policies and Active Deny behavior remain authoritative if the client snapshot is stale.

## Changes

- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs` — fail closed on unscoped non-create options requests before protected-phase evaluation.
- `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectLifecycleCatalogOptionsTests.cs` — assert the 400 contract and preserve scoped protected-phase conflict coverage.
- `apps/api/tests/NEXUS.Api.Tests/Security/ProjectTeamCapacityAuthorizationTests.cs` — verify both endpoint policies combine, missing either permission and effective Active Deny fail authorization, and a successful scoped service result maps to 200.
- `apps/web-react/src/features/projects/ProjectsListPage.tsx` and `components/ProjectAdvancedSearch.tsx` — remove the unscoped lifecycle query and use status values projected from visible Project rows.
- `apps/web-react/src/features/projects/api/project-lifecycle.ts` and `features/administration/api/projectLifecycle.ts` — gate non-create options hooks on explicit Organization or Project scope.
- `apps/web-react/src/features/projects/api/projects.ts` and `ProjectOverviewTab.tsx` — gate Team Capacity on both client permissions, disable retry/remount retry, and show a localized denied/unavailable state while hiding cached capacity.
- `apps/web-react/src/i18n/messages/en.json` and `ar.json` — add the bilingual Team Capacity access message.
- `apps/web-react/src/test/projects-list.test.tsx` and `project-overview-source-parity.test.tsx` — cover management bootstrap with no lifecycle request, DTO-backed status filtering, permission gating, stale-snapshot 403 handling, and scoped Project creation fixtures.
- `apps/web-react/src/test/project-handover-tab.test.tsx` — remove an unsupported `I18nProvider` prop; the test already sets the locale in local storage, and the mismatch blocked the requested frontend typecheck.
- `docs/quality/MASTER-UAT-CHECKLIST.md` — add CPL-409-07 and CAP-403-01 through CAP-403-03.

## Verification

| Check | Result |
|---|---|
| React TypeScript typecheck | PASS — `npm run typecheck` |
| Focused React regressions | PASS — `projects-list.test.tsx` and `project-overview-source-parity.test.tsx`, 34/34 |
| Focused API regressions | PASS — 20/20 in `ProjectLifecycleCatalogOptionsTests` and `ProjectTeamCapacityAuthorizationTests`; API and test assemblies compiled to an isolated temporary output because the shared API output DLL was locked by an already-running `NEXUS.Api` process |
| API build | PASS — compiled as part of the focused `dotnet test` build |
| React production build | PASS — `npm run build` with `TEMP`/`TMP` redirected to ignored `node_modules/.cache` after the Windows Temp cleanup was denied; Vite reports existing Zod annotation, dynamic/static import, and large-chunk warnings |
| `git diff --check` | PASS — no whitespace findings in the targeted change/report files; Git reported existing LF-to-CRLF normalization notices |
| Authenticated API/Browser UAT | Not run — Product Owner session remains required |
| Database operations | None — no database connection, migration, seed, reset, or write performed |

## Open evidence

- Verify live ManagementProvider bootstrap and selected-Portfolio lifecycle response in the Product Owner Browser UAT session.
- For the reported capacity 403, capture its response body/request ID and current Organization-effective `Project.View` and `Resource.View` grants/Active Deny state plus management context. Source behavior predicts an authorization denial if either required permission is absent; it does not prove the reported user's exact state.
- Exercise CAP-403-01 through CAP-403-03 and confirm 200 for an actor with both permissions and valid Project scope, 403 for a permission/Active Deny case, graceful single-attempt UI behavior, and 404 for out-of-scope Project access.
