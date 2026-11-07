# P0 Meeting Minutes Display After Completion

**Date:** 2026-11-05  
**Status:** Source correction is in place. Focused React checks pass; focused API test execution is blocked by the repository's IKVM test-build dependency.  
**Scope:** Existing Meetings completion, Minutes read/create/update, and completed workspace behavior. No database reset, seed, direct SQL, or browser UAT was performed.

**Verification update (2026-11-06):** The Main Agent's consolidated React check passed 8 files / 41 tests, including the Minutes suite. The API identity/scope regressions remain unexecuted; earlier builds stalled in the transitive IKVM/MPXJ adapter graph and later bounded attempts returned no test result. The generated public-identifier migration was not applied. See [the completion-fix report](../modules/MEETING-MINUTES-COMPLETION-FIX.md) for the corrected root cause and deployment status.

## Root-cause correction (2026-11-06)

This section supersedes the initial source-level assessment recorded on 2026-11-05. That assessment attributed the failure to React query synchronization alone and stated that the API contract was already correct. A later comparison with the baseline and API lookup requirements confirmed the primary defect: the Meeting route accepted a public identifier, but the Minutes requests used that route identifier directly even though the Minutes APIs resolve by the internal `Meeting.Id`. Opening through a PublicId URL could therefore return `404` for a valid Meeting.

The React page now resolves Meeting Details first and uses the returned internal Meeting ID for Minutes and child-resource APIs while preserving the public route. For a valid in-scope completed Meeting without a Minutes row, the API returns `200` with `null`; missing or inaccessible Meetings remain non-disclosing `404`. Stale completion/query state and the former page-wide error handling were contributing UI issues and are also corrected: completion refreshes authoritative state, invalidates/refetches Minutes, opens Minutes & Follow-up by default, and keeps API errors distinct from an authorized empty Draft.

The source-level identity mismatch is confirmed by the route/API contracts and covered by a focused regression. No live browser/network capture of the Product Owner's original request was available, so its actual HTTP response remains unobserved.

## Affected API contract

| Operation | Endpoint | Expected behavior |
|---|---|---|
| End meeting | `POST /api/meetings/{id}/end` | Authorized `Meeting.Edit`; successful response returns the persisted `Completed` state and actual end timestamp. |
| Read Minutes | `GET /api/meetings/{id}/minutes` | `Meeting.View`; existing Minutes returns its DTO, valid completed Meeting without Minutes returns `200` with `null`, and missing/out-of-scope Meeting remains non-disclosing `404`. |
| Create draft | `POST /api/meetings/{id}/minutes` | Existing `Meeting.Manage`; only an explicit create/save creates a Draft. A retry returns the existing record without replacing its fields or publication state. |
| Update Minutes | `PUT /api/meetings/{id}/minutes` | Existing `Meeting.Manage`; updates the resolved record with concurrency protection. |
| Finalize Minutes | `POST /api/meetings/{id}/minutes/finalize` | Existing `Meeting.Manage`; publication remains an explicit action. |

Authentication, authorization, tenant, organization, management, and resource-scope checks remain authoritative. An empty draft is never synthesized from `401`, `403`, `404`, or server failures. Existing publication, comments, replies, attachments, and follow-up content are preserved.

## Before and after

| Area | Before | After |
|---|---|---|
| API identity and missing Minutes | Minutes requests from a PublicId route used the public route ID where the API resolves by internal `Meeting.Id`; valid Meetings could return `404`. | Resolve Meeting Details first, use the internal ID for Minutes APIs, return `200/null` for an authorized completed Meeting without a Minutes row, and keep missing/out-of-scope Meetings at `404`. |
| End Meeting UI | Successful completion could leave the completed workspace or Minutes query unsynchronized. | Refreshes authoritative Meeting state, invalidates relevant Minutes data, and selects Minutes & Follow-up. |
| Empty versus failed | The query states were not made sufficiently actionable after completion. | Only successful `null` shows the authorized empty-draft path; loading and errors remain distinct, with retry and English/Arabic copy. |
| Existing Minutes | Existing rows are read and edited through the existing workflow. | Existing Draft or Final record remains the same record; retries do not replace existing content or publish automatically. |

## Permissions reviewed

- `Meeting.View` continues to gate Minutes reads.
- `Meeting.Edit` continues to gate Start/End actions.
- `Meeting.Manage` continues to gate Minutes creation, editing, and finalization.
- Backend tenant and project-management scope checks continue to distinguish valid in-scope Meetings from missing, foreign-tenant, and out-of-scope Meetings.
- Completion does not grant Minutes editing permission. No role-name bypass or broad authorization fallback was added.

## Regression coverage

The focused React suite covers the Completed default tab, selectable and mounted Meeting Details panel, successful End Meeting state refresh, authorized empty draft, read-only access without `Meeting.Manage`, existing Draft preservation, distinct retryable failure state, Arabic/RTL error rendering, and Minutes-query invalidation/refetch after completion.

The focused .NET tests cover controller `200/null` versus `404`; service-level scoped empty Minutes, nonexistent/foreign/out-of-management-scope IDs, pre-completion eligibility, idempotent draft creation, existing Final Minutes preservation, and Scheduled → In Progress → Completed timestamp/content preservation.

The Master UAT checklist adds MMP-01-MMP-09 for lifecycle, timestamp, identity and scope, existing and missing Minutes, permissions, tabs/cache/refresh, EN/AR error states, and follow-up preservation. Automated checks are recorded separately from browser UAT; all browser UAT rows remain `NOT RUN` for Product Owner execution.

## Changed files

Primary implementation paths include:

- `apps/api/src/NEXUS.Modules.Meetings/Application/MeetingService.cs`
- `apps/web-react/src/api/meetingsFull.ts`
- `apps/web-react/src/features/delivery/MeetingPages.tsx`
- `apps/web-react/src/features/delivery/MeetingMinutesCollaboration.tsx`
- `apps/web-react/src/i18n/messages/en.json`
- `apps/web-react/src/i18n/messages/ar.json`

Focused test and documentation changes:

- `apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj`
- `apps/api/tests/NEXUS.Api.Tests/Unit/MeetingMinutesControllerTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/MeetingMinutesServiceRegressionTests.cs`
- `apps/web-react/src/test/meeting-minutes-page-parity.test.tsx`
- `apps/web-react/src/test/meeting-minutes-query-invalidation.test.tsx`
- `docs/quality/MASTER-UAT-CHECKLIST.md`
- `docs/meetings/MEETING-MINUTES-POST-COMPLETION-FIX.md`

## Build and test record

| Check | Command/result |
|---|---|
| Focused React regressions | Three focused test files passed, 14 tests total. React Router emitted future-flag warnings only. |
| Focused .NET API tests | Two attempts to run the focused `MeetingMinutes` filter are blocked while IKVM converts the MPP adapter dependency, emitting unsupported Java class-version and missing-class warnings. The test cases did not execute; this is not a passing test result. |
| Meetings module build | Backend workstream reported its Meetings module build succeeded with 0 warnings and 0 errors. |
| React typecheck | `npm run typecheck` - passed. |
| React lint | Focused ESLint run for the changed TypeScript files - passed. |
| React production build | `npm run build` - passed; Vite reported existing large-chunk and mixed dynamic/static import warnings. |
| Full API host build | Interrupted after more than five minutes in the IKVM MPP adapter conversion; no Meetings source compiler error was observed. |
| Browser UAT | Not run; reserved for Product Owner. |
| Database changes | None. No migration, reset, seed, or direct SQL operation. |

## Remaining limitations

The focused API tests remain unexecuted because the test project triggers an IKVM MPP adapter conversion that does not complete in this environment. The reported runtime error was not captured live, so the root cause is verified from source behavior and regressions rather than a reproduced browser response. Browser UAT remains for the Product Owner.
