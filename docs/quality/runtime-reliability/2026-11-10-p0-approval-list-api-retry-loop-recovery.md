# P0 Approval List API Retry Loop Recovery

**Date:** 2026-11-10  
**Status:** Source fixes implemented; focused frontend regressions passed. The API project compiled, but the API test project failed before test execution. Authenticated browser UAT remains pending.

## Reported symptom

The Product Owner reported that opening My Work → Approval List caused rapid failed calls: `GET /api/handover-requests/pending-for-me` returned HTTP 400 21 times, `GET /api/manager-handovers/pending-for-me` returned HTTP 403 21 times, and `refreshAfterForbidden` ran repeatedly. These counts are user-supplied browser evidence; the authenticated browser session was not available to reproduce them here.

## Source findings and changes

- The shared React Query client already rejects automatic retries for `ApiError` 4xx responses and bounds retries for other failures to one retry. A deterministic 400, 401, or 403 therefore should not be retried by the default query policy.
- The API client emits `nexus:authorization-denied` for each authenticated 403. `AuthProvider` handled that event by refreshing `/api/auth/me`; repeated denials could interact with permission-gated query mounting. The handler now refreshes permissions at most once for the current local session and keeps the current UI permission state while that refresh runs. Access-token refresh and session-change protections are unchanged.
- The project handover `pending-for-me` action previously mapped every failed service result to HTTP 400. The service can return a `Forbidden` result with status 403. The controller now preserves the service error status, so a scope/eligibility denial is returned as 403 rather than mislabeled as a request validation error. No runtime trace was available to prove which service error produced the reported browser 400.
- The manager-handover endpoint's 403 is an expected authorization denial when the user lacks `Handover.Approve` or an active Management-category role. This gate remains enforced in the backend. Whether the affected browser user had both conditions was not verified against live grants or the authenticated session.
- My Work fetches project handovers and manager handovers only when both client-visible eligibility conditions are present. Their failures have separate retry controls and do not suppress independently authorized Change Request or Annual Budget results. Annual Budget loading retains its `Project.View`, `ProjectAnnualBudget.View`, and `ProjectAnnualBudget.Approve` requirements.

## Regression coverage added

- `apps/web-react/src/test/api-query-retry-policy.test.ts`: HTTP 400, 401, and 403 do not retry; a server/network transient is retried once and then stops.
- `apps/web-react/src/test/auth-session-isolation.test.tsx`: 21 concurrent 403 responses followed by 21 additional denials cause only one `/api/auth/me` permission refresh for the session.
- `apps/web-react/src/test/my-work-approval-failure-isolation.test.tsx`: denied handover queues do not hide successful Change Request and Annual Budget entries; a non-Management role with the handover permission does not call either handover queue while an authorized Annual Budget queue still loads.
- `apps/api/tests/NEXUS.Api.Tests/Unit/HandoverRequestControllerTests.cs`: a service-level Forbidden is returned as HTTP 403, and a successful pending query is returned as HTTP 200.

## Verification results

- Focused frontend checks passed: 24/24 tests across four files (`api-query-retry-policy`, `auth-session-isolation`, `my-work-approval-failure-isolation`, and `my-work-approvals-handover`). The first Vitest invocation stopped before loading tests because the sandbox Temp path denied a cache rename; rerunning with the permitted user Temp path completed successfully.
- The Annual Budget inbox regression passed separately: 3/3 tests. Its existing amount assertion was aligned with the authoritative formatter output (`EGP 125,000`, with no fractional digits for this whole amount); production formatting was not changed.
- React TypeScript check passed (`npm run typecheck`).
- One filtered API test/build attempt compiled `NEXUS.Api`, including the changed handover controller. The test project then failed compilation before running tests due two syntax errors in the shared Annual Budget regression files: a missing closing parenthesis in `ProjectAnnualBudgetAssignedInboxTests.cs` and a stray `Moq.` before `new` in `ProjectAnnualBudgetWorkflowRecoveryTests.cs`. Both syntax errors were corrected afterward, but no second build/test attempt was made under the one-attempt limit. `HandoverRequestControllerTests` therefore remains unexecuted.
- The API attempt emitted existing `NU1900` warnings because NuGet vulnerability metadata was unavailable, plus IKVM class-version/missing-reference warnings from the MPXJ adapter dependency.

## Scope and remaining verification

- No database records were changed. No entity, EF model, migration, or database schema change is required.
- API controller regression execution remains pending; the sole API build/test attempt was used. No source-only result is treated as proof of the affected user's live permission or scope state.
- Product Owner browser UAT must verify request counts after opening Approval List, expected project-scope denial behavior, no repeated `/api/auth/me` refresh, independent Annual Budget/Change Request results, and normal loading for a user with the required Handover permission and Management role.
- Preserve Active Deny, tenant/project boundaries, and all existing Annual Budget reviewer and Sponsor authorization checks.
