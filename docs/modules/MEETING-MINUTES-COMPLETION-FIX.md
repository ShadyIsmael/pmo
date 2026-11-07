# Meeting Minutes Completion Fix

**Date:** 2026-11-06  
**Status:** Source correction implemented; browser and current focused API execution remain pending.  
**Scope:** Existing Meeting completion, Minutes retrieval, completed workspace, and Minutes deep links.

## Confirmed source root cause

Meeting Details accepts a tenant-scoped public identifier in the route. Before the fix, the Minutes request used that route identifier directly, while the Minutes and child-resource API operations resolve by the internal `Meeting.Id`. Opening a completed Meeting through its public URL therefore sent the wrong identifier and could produce a `404`, which the UI displayed as an inability to load Minutes.

The React page now loads the authoritative Meeting Details record first, then uses its internal ID for Minutes reads, Minutes mutations, and collaboration child queries. The public ID remains in the URL. The completion flow also refreshes the Meeting state, invalidates/refetches the Minutes query, and selects Minutes & Follow-up. These query and error-state changes address contributing synchronization behavior; they do not convert errors into an empty draft.

The exact HTTP response from the Product Owner's original browser session was not captured. The identifier mismatch is confirmed by comparing the route/query implementation with the API's internal-ID lookup contract and is covered by a focused regression test.

## API behavior and lifecycle

| Operation | Contract |
|---|---|
| Start/end Meeting | Existing authorized endpoints persist `InProgress`/`Completed` and actual UTC timestamps. Completion preserves agenda, attendance, decisions, and action items. |
| Read Minutes | `GET /api/meetings/{id}/minutes` returns the existing record, or `200` with `null` for a valid, in-scope completed Meeting with no Minutes row. Missing or inaccessible Meetings retain non-disclosing `404`; authentication, authorization, and server failures are not mapped to empty content. |
| Create/update Draft | Existing `Meeting.Manage` authorization remains required. Empty Draft creation is explicit; repeated creation resolves to the existing record and does not overwrite existing or published content. |
| Publish | Existing explicit finalize operation and permission checks remain in place. Completion does not publish Minutes or grant edit access. |

The React workspace keeps Minutes editing hidden before completion. Completed Meetings default to Minutes & Follow-up; Meeting Details remains selectable and both panels retain their mounted state. Only a successful `200/null` response displays an empty Draft. Loading, forbidden, not-found, and failed responses remain distinct, with retryable localized errors.

## Identity, permissions, and data protection

- Public Meeting IDs remain in supported routes. The detail response's internal ID is used only for authenticated API requests.
- `Meeting.View`, `Meeting.Edit`, and `Meeting.Manage` continue to gate viewing, lifecycle operations, and Minutes changes respectively.
- Tenant, organization, project-management scope, and backend resource checks remain authoritative. No role-name bypass or new permission grant was added.
- Reads do not create Draft rows. Existing Draft, published Minutes, comments, replies, attachments, checklist items, and Work Item links are preserved.
- Comment and reply notification links use public Meeting and comment/reply identifiers and revalidate access through the existing Meeting route.

## Changed areas

- `apps/web-react/src/api/meetingsFull.ts` — resolve route PublicId to the Meeting DTO, use the internal Meeting ID in Minutes API query keys and requests, and invalidate/refetch after completion.
- `apps/web-react/src/features/delivery/MeetingPages.tsx` — completed workspace tab selection and separate Minutes loading/empty/error states.
- `apps/web-react/src/features/delivery/MeetingMinutesCollaboration.tsx` — avoid child-resource requests until a Minutes record exists; resolve public comment/reply targets.
- `apps/api/src/NEXUS.Modules.Meetings/Application/MeetingService.cs` — valid completed Meeting with no Minutes returns the empty result while preserving inaccessible-Meeting handling.
- Focused API and React tests cover PublicId-to-internal-ID resolution, empty and existing Minutes, permission visibility, completion cache refresh, errors/localization, and comment/reply deep links.

## Verification

| Check | Result |
|---|---|
| Consolidated React focused tests | Passed: 8 files, 41 tests (Main Agent consolidated run). This includes the 17-test Minutes suites and 18 notification content/navigation/center tests. |
| React typecheck | Passed: `npm run typecheck`. |
| Focused React lint | Passed for changed notification resolver, Notification Center, and tests. Previous Minutes lint/build checks are recorded in the detailed report below. |
| Current focused .NET test run | Not completed. Earlier focused test builds stalled in the transitive IKVM/MPXJ adapter graph before reaching tests. The latest consolidated attempts produced: first attempt exit code 1 after about 12 seconds with no console output; second attempt produced no output/logs and was interrupted after about 90 seconds. No test result is available; new API tests remain unexecuted. A stale `--no-build` result is not counted. |
| Database / migration | No database operation, reset, seed, SQL write, or migration application performed. The generated meeting-notification migration remains unapplied; target database history/schema are not verified here. |
| Browser UAT | Not run; reserved for Product Owner. |

A separate duplicate Vitest attempt was auto-review blocked after the npm process triggered telemetry egress to an untrusted external host; it was not retried through a workaround and produced no result. The Main Agent's consolidated local React run subsequently passed as listed above.

## Deployment status and remaining limits

The source path is implemented. Notification deep links also require the generated public-identifier migration to be applied through the separately authorized database workflow. No database application was authorized or performed; classify notification deployment as **Source Implemented — Pending Migration / Runtime Verification**. The exact original browser failure was not captured, and current focused API tests remain blocked by the IKVM/MPXJ test build. Browser UAT remains with the Product Owner.

See [the detailed post-completion investigation history](../meetings/MEETING-MINUTES-POST-COMPLETION-FIX.md) for the earlier verification log and test-environment notes.
