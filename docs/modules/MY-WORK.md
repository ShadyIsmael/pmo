# My Work

My Work groups the user's existing personal work surfaces under route-backed navigation:

| Route | Surface | Existing data |
|---|---|---|
| `/my-work/timesheets` | My Time | Time entries and the existing personal/team summary |
| `/my-work/tasks` | Tasks | Assigned `Task` work items (`type=Task`, `myWork=true`) |
| `/my-work/approvals` | Approvals | Pending and historical handovers, manager handovers, and project change requests supported by the existing approval services |

The `/my-work` route continues to open Tasks. Approval review links remain under `/my-work/approvals/review/...`, and task rows continue to open the canonical `/work-items/{id}` detail route.

All three surfaces use the shared `RouteTabs` horizontal navigation used by Project Details, in the order **My Time**, **Approvals**, **Tasks**. Each surface places the tabs directly after its page header. The route remains the source of the selected tab, so direct links, browser history, and approval review deep links continue to work; only the active route content is rendered.

## Task list views

The task list keeps its existing status filters:

| Filter | API status |
|---|---|---|
| All Tasks | No status filter |
| In Progress | The active status catalog entry whose code is `InProgress` |
| Completed | The active status catalog entry whose code is `Done` |

The default task filter remains All Tasks. Task status controls stay within the Tasks route and update the existing list query; the `/my-work/timesheets`, `/my-work/tasks`, and `/my-work/approvals` paths provide shareable deep links to the three primary sections. The task list continues to resolve status codes from the existing Work Item Status catalog.

Search, existing summary cards, task rows, task creation, attachment handling, status badges, due-date display, and loading/error/empty states remain on their existing data and permission paths. This does not add Work Item types or introduce task data into the approval or time lists.

## Authorization and scope

This UI change adds no permission key, grant, or backend authorization path. Existing API authorization and tenant/project scoping remain authoritative.

| Surface/action | Existing frontend check | Scope boundary |
|---|---|---|
| Task list | Existing My Work list request (`myWork=true`) | Work Item API remains responsible for authenticated tenant and management scope |
| Create Task | `WorkItem.Create`, writable management context, and not in read-only mode | Existing Work Item create endpoint validation and scope |
| Approval requests and decisions | Existing `Handover.Approve` and `ChangeRequest.Approve` checks; decision requests remain subject to backend authorization | Existing request assignment, tenant, and resource scope |
| View time entries | `TimeTracking.ViewOwn` and/or `TimeTracking.ViewTeam` | Existing time-entry endpoint and management scope |
| Manage own time entries | `TimeTracking.ViewOwn` plus `TimeTracking.ManageOwn` in a writable context; mixed team rows also require `TimeTracking.ManageTeam` for row changes | Existing time-entry endpoint authorization |

## My Time entry form

For an actor with `TimeTracking.ViewOwn` and `TimeTracking.ManageOwn` in a writable context, **Add entry** opens the existing time-entry form in the application Side Sheet. The form keeps its project, work date, start time, end time, and description fields and submits through the existing time-entry API and validation rules. Required-field validation remains attached to controls; keyed API validation errors are associated with their fields when supplied. The current time-entry endpoint returns unkeyed business-rule errors, which remain in the form's general save alert. Cancel closes without saving. Closing a modified form asks before discarding the draft. A successful create closes the sheet and the existing query invalidation refreshes the time-entry list and summary. Edit and delete actions keep their existing permission checks and API paths.

The Side Sheet uses the application component's responsive width and logical end-side placement, and the existing localized labels and document direction support English/LTR and Arabic/RTL. No Time Tracking permissions, grants, endpoints, or backend rules are added by this UI change.

## Verification notes

Focused React verification passed: five My Work suites passed 28 tests (layout 3, Time Entry Side Sheet 7, permissions 6, Tasks 3, approvals/handover 9), and the separate approval-review regression passed 1 test (6 files, 29 tests total). Coverage includes route order/default/selection, EN/AR and RTL, Side Sheet cancel/save/error/validation/refresh behavior, and existing permission/action regressions. A broader check ran 10 test files: 50 passed and 2 failed (52 total). The CSRF bootstrap mock rejection in `auth-session-isolation.test.tsx` was fixed; the remaining failures are its refresh flow expecting a 401 retry to resolve and a `timer.test.tsx` timing assertion that expected `00:01:00` but received `00:01:01`. `npm run typecheck` passed after test-only compatibility fixes; `npm run lint` passed with 0 errors and 10 existing warnings in unrelated files; `npm run build` passed with existing PlannedPayments mixed dynamic/static import and chunk-size warnings. Browser UAT was not run per request; responsive visual behavior and authenticated persistence/scope remain for Product Owner verification.

## Implementation record

Scoped implementation files changed for this request:

- `apps/web-react/src/features/my-work/MyWorkLayout.tsx`
- `apps/web-react/src/features/my-work/MyWorkTabs.tsx`
- `apps/web-react/src/features/my-work/MyWorkTasksPage.tsx`
- `apps/web-react/src/features/my-work/MyWorkApprovalsPage.tsx`
- `apps/web-react/src/features/my-work/ApprovalReviewPage.tsx`
- `apps/web-react/src/features/my-work/MyWorkTimesheetsPage.tsx`
- `apps/web-react/src/i18n/messages/en.json` and `apps/web-react/src/i18n/messages/ar.json`
- `apps/web-react/src/test/my-work-layout.test.tsx`, `apps/web-react/src/test/my-work-timesheet-side-sheet.test.tsx`, and `apps/web-react/src/test/approval-review-page.test.tsx`

The existing time-entry API hooks, permission catalog, and shared `RouteTabs` and `SideSheet` components are reused. No endpoint or permission changes were made. The current server's time-entry business-rule errors do not include field keys, so those errors remain a general form alert; keyed field errors are rendered beside their controls when an API response supplies them. A separate API log showed `/api/auth/csrf` called over HTTP while antiforgery uses `SecurePolicy=Always`; this auth/API runtime issue is outside this UI task and was not changed here. Authenticated browser UAT remains pending.

Validation-only test compatibility edits also touched `management-scope.test.ts`, `directory-integration-access.test.ts`, `project-detail.test.tsx`, `project-reporting-configuration-query.test.tsx`, `project-settings-navigation.test.ts`, `projects-list.test.tsx`, `timer.test.tsx`, `visible-first.test.tsx`, `routing.test.tsx`, `auth-session-isolation.test.tsx`, and `meeting-minutes-query-invalidation.test.tsx`. These edits update existing test helpers/mocks for current API contracts; they do not change production behavior. The broader check's remaining refresh-flow and timer-duration failures are listed above.
