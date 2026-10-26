# Project Plan Grid — Desktop parity work log

Status: C — PARITY INCOMPLETE. Updated 2026-10-23. The latest evidence is in the final continuation entry; earlier sections are dated snapshots and may report blockers that were resolved later.

## Governing reference and architecture

Microsoft Desktop hierarchy and dependencies are independent. Summary dates derive from subtasks; duration is the working-time span rather than the sum of overlapping durations. Manual leaf tasks retain explicit dates. Sources inspected:
- https://support.microsoft.com/en-us/project/create-and-work-with-subtasks-and-summary-tasks-in-project-desktop
- https://support.microsoft.com/en-us/project/how-project-schedules-tasks-behind-the-scenes
- https://support.microsoft.com/en-us/project/task-mode-task-field
- https://support.microsoft.com/en-us/project/link-tasks-in-a-project
- https://support.microsoft.com/en-us/project/add-lead-or-lag-time-to-a-task
- https://support.microsoft.com/en-us/project/work-with-the-gantt-chart-view

NEXUS reuses Plan/PlanVersion, PlanActivity, PlanWbsNode adjacency hierarchy, PlanDependency stable activity keys, PlanCalendar, AutoSchedulingEngine, PlanningWorkingCalendar, SummaryTaskRollupService, PlanSchedulingAnalysisService, React PlanningGrid and shared Grid/Gantt workspace rows. Summary is derived from hierarchy, not a new persisted activity enum. ADR-007 remains governing. Next.js remains present.

## Phase 1: Summary scheduling correction

Removed dependency-gated milestone-container aggregation. Existing activities with effective WBS children acquire Task summary semantics while keeping child relationships. Leaf milestones remain zero-duration. Summary Start=min descendant Start, Finish=max descendant Finish; duration uses the existing selected/default working calendar. Removed first-row/manual-summary start preservation from rollup. First-created activity retains Manual mode, but derived summary dates are read-only in Grid and Task Details. Ordinary Manual leaf behavior is unchanged. Summary writes ignore supplied date overrides.

EF changed: no. Migration required: no. Development data reset/update: none.

Validation: TypeScript passed; focused Grid/WBS frontend tests 38/38 passed; targeted PlanningGrid lint passed; Vite production build passed (existing large-chunk warnings). Two new SQLite persistence tests passed: overlapping independent children span ten working days; nested summary propagation. These are SQLite integration checks, not SQL Server Development verification. Full API host and test project compiled in independent output folder. Broader regression run recorded below when completed.

Initial normal build was blocked by IKVM task-host creation inside process isolation. Running verification outside isolation resolved it. Host default output was locked by the running API; independent output avoided restarting the user's server. Running API has not yet been restarted onto these changes.

Browser: local demo login and Project Registry verified. Demo user has zero visible projects. User is preparing a different account with a project for persisted browser UAT. No plan mutations or browser Grid/Gantt acceptance claimed.

## Current source audit / remaining parity gates

| Feature | Classification | Evidence / next acceptance gate |
|---|---|---|
| WBS / nested subtasks | WORKING — REUSE | Existing adjacency tree, cycle and version validation; runtime pending |
| Summary start/finish/duration | DEFECTIVE — FIX | Corrected dependency gate and retained start; focused persistence passed |
| Summary milestone distinction | PARTIAL — EXTEND | Legacy containers promote without child loss; explicit conversion UX pending |
| Manual / Auto | PARTIAL — EXTEND | Existing modes and scheduler; broader Desktop conversion/warning parity pending |
| Dependencies / lead-lag | WORKING — REUSE | Four types and stable keys; runtime propagation pending |
| Calendars | PARTIAL — EXTEND | Working intervals and holidays reused; resource-calendar parity pending |
| Constraints / deadlines | PARTIAL — EXTEND | Existing backend and controls; all eight Desktop semantic gates pending |
| Inline editing / keyboard | WORKING — REUSE | Focused Grid suite passed; accessibility/browser gate pending |
| Copy/paste | PARTIAL — EXTEND | Rectangular TSV copy/paste and multiline task names are implemented; live persistence acceptance pending |
| Range / multi-row / Fill Down | PARTIAL — EXTEND | Cell/row ranges, Shift/Ctrl, drag, keyboard extension and Fill Down implemented; command-wide context selection and runtime accessibility checks pending |
| Undo/Redo | PARTIAL — IMPLEMENTED FOR TRACKED GRID COMMANDS | Persistent activity/dependency/WBS command history and row-version conflict checks; SQL Server runtime UAT pending |
| Indent/outdent / reorder | PARTIAL — EXTENDED | Atomic multi-node WBS placement and secondary activity reorder; browser persistence check pending |
| Columns / preferences | WORKING — REUSE | Existing visibility, resize, order and saved preferences |
| Sorting / outline-level filtering | PARTIAL — EXTEND | Existing column filters; view sorting/outline selector pending |
| Progress / actuals | PARTIAL — EXTEND | Existing fields and summary weighting; leaf-weight correctness pending |
| Baselines / version isolation | WORKING — REUSE | Existing frozen snapshot/version architecture; runtime regression pending |
| Resources / work calculation | PARTIAL — EXTEND | Existing assignment API/picker; task-type and effort-driven model pending |
| Gantt / critical path | WORKING — REUSE | Existing shared rows and backend analysis; runtime synchronization pending |
| Concurrency / atomic bulk writes | PARTIAL — EXTENDED | Activity, dependency, and WBS bulk updates use bounded transactions and row-version validation; broad concurrency/security audit remains |
| EN/AR/RTL / light-dark | NOT TESTED | Existing localization/tokens reused; rendered verification pending |
| 500/1000/5000 performance | NOT TESTED | Benchmark and virtualization evaluation pending |
| Direct API security scenarios | NOT TESTED | Existing policies/scopes retained; hostile-request regression pending |

This log does not mark complete Desktop parity, full research coverage, SQL Server persistence, API startup on the new binary, or browser UAT as passed.
Final phase-1 regression result: 104/104 .NET scheduling/activity/WBS/dependency and new summary persistence cases passed. Task Details suite 9/9 passed; total focused React cases 47/47. Updated drawer passed targeted lint and TypeScript. Code-quality self-review completed for touched paths. Full Desktop parity remains pending as listed above.

## Phase 3 — Selection, clipboard, and atomic activity updates — 2026-10-22

### Grid selection

`PlanningGrid` now supports single-cell and rectangular cell selection, multi-row selection, Ctrl/Meta toggling, Shift extension, drag extension, and keyboard Shift+Arrow extension. Selections are pruned when rows or columns disappear and reset when the plan changes. Cell selection is keyboard accessible through the grid container. The selection model also drives Format ribbon updates: formatting multiple selected activity rows uses the bulk API. Context menus preserve a multi-row selection when opened on an already-selected row; their structural operations still target the clicked row, so command-wide multi-selection is not complete.

### Clipboard, activity edits, and dependency links

Copy exports the selected rectangular cell area as TSV. Paste reads tab/newline-delimited text from the active cell and supports activity name, duration, progress, Start/Finish, actual dates, constraint date, and deadline. Empty TSV cells are left unchanged. `Ctrl+D` fills the selected activity-cell range down from its first row. The UI rejects unsupported, read-only, invalid, milestone-duration, and over-limit inputs before sending the request. The activity update endpoint accepts 1–500 unique activity rows and requires each row's existing `RowVersion`; it is protected by `Planning.Manage` and runs each update through `PlanActivityService.UpdateAsync` inside one EF transaction. A failed validation or concurrency update rolls the transaction back.

Task Ribbon Link Tasks and Unlink Tasks commands now operate on the selected leaf activities in visible order and call a new bulk predecessor endpoint once. The endpoint limits requests to 1–500 unique successors, then reuses `ReplacePredecessorsAsync` inside a single transaction. This preserves external predecessors during unlink, applies canonical dependency validation/scheduling/summary roll-up/audit, and rolls back the complete request if a later item is invalid. The endpoint has the same `Planning.Manage` policy as single dependency edits. No EF entity, mapping, or schema changed; no migration or Development data write was made.

### Verification and remaining gates

Focused Grid/Ribbon tests pass 43/43, including TSV copy/paste, Fill Down, multi-row Format, Link Tasks, and Unlink Tasks retaining external predecessors. The complete React suite passes 641/641 across 128 files. `npm run typecheck`, changed-file ESLint, and the Vite production build pass (2,546 modules; existing large-chunk warnings remain). The `NEXUS.Modules.ProjectPlanning` project builds with 0 warnings and 0 errors. The focused `PlanActivityServiceTests` suite passes 30/30, including invalid-member and stale-row-version rollback; `PlanDependencyServiceTests` passes 30/30, including rollback when a later bulk dependency would create a cycle. The full isolated API suite passes 441/441. The API host builds with 0 warnings and errors and starts on an isolated port with Development database initialization explicitly skipped; `/health` returned 200 and Swagger includes the bulk predecessor route. Live browser verification confirms multi-cell selection (four selected cells across two rows), Link Tasks becomes enabled for two selected leaf activities, and Unlink Tasks remains disabled when no internal dependency exists. No link, paste, or edit mutation was sent to the live app; no plan or Development database was modified.

Gate 1 core selection mechanics and Grid-to-Format integration are implemented; complete selection sharing across all context-menu commands remains PARTIAL. Gate 2 is PARTIAL: TSV Copy/Paste, Fill Down, atomic activity edits, atomic Link/Unlink, and atomic multi-node WBS placement are implemented with service/UI regression coverage; SQL Server live persistence remains open. Gate 3 now has persistent command history for tracked activity, dependency, and WBS structural operations, but browser Undo/Redo persistence remains unverified and task creation/deletion history remains outside the supported scope. Gate 4 remains open: File and Report tabs are absent, and the remaining ribbon command coverage is incomplete. Scheduling parity (Gate 5) and full hostile security, ReadOnlyViewAs, EN/AR/RTL, light/dark, keyboard accessibility, live persistence, and 500/1,000/5,000-row performance checks (Gate 6) remain OPEN. Overall status remains C — PARITY INCOMPLETE.

## Phase 2 — Ribbon re-audit and accessibility work — 2026-10-22

The baseline was checked against the current source and live React/Vite page. The previously named `UAT Grid Summary 2026-10-22` Scenario was still selected and its stored Summary and overlapping child dates matched the phase-1 result. No plan rows were edited in this audit.

### Verified command inventory

| Tab / capability | Current implementation and handler | Permission/context result | Verified status |
|---|---|---|---|
| Task — New Task / Insert Above / Insert Below / Add Subtask / Milestone / Delete | `PlanningGrid` `createRow` / `removeRow`; existing canonical mutations | Mutations use `editable`; insertion context is selected single row; no multi-row transaction | PARTIAL — single-row commands reusable; full command parity not tested |
| Task — Indent / Outdent / Move Up / Move Down / Collapse / Expand | Existing WBS/activity placement and move handlers; workspace uses shared rows | Single selected row; API writes may span multiple sequential WBS calls for moves | PARTIAL — reuse; atomic bulk structural changes missing |
| Task — Task Information / Dependencies / Notes | row Edit opens `PlanningActivityDetailsSheet`; Dependencies command opens the same details flow; Notes section is in details | Existing detail-sheet editability guard | PARTIAL — canonical details reused; section-targeted open and bulk link/unlink missing |
| Task — Auto / Manual and progress | Mode is editable from the grid cell; progress via grid/details | No Task Ribbon mode/progress commands; bulk progress unavailable | PARTIAL |
| Resource — Assignments | Opens existing assignment panel, backed by planning assignment/resource APIs | Picker and assignment mutation reuse canonical services; panel is not opened prefiltered to selected row | PARTIAL |
| Project — plan/version/baseline/calendar/import | Plan selector, create plan, import, capture/view baseline, calendar panel, versions panel | Baseline lifecycle permission hint and write guards present; baseline is read-only | PARTIAL — commands present; live mutation UAT not done |
| Project — Recalculate / diagnostics / status date | Project ribbon exposes a permission-gated explicit recalculate action; analysis is fetched for Network view | Status-date command remains absent | PARTIAL |
| View — Grid / Gantt / Split / Critical Path / zoom / baseline display | Existing shared workspace state controls the table, timeline, network, zoom, and baseline display | View controls update the existing Gantt/network | WORKING — REUSE; phase-2 end-to-end commands not exhaustively exercised |
| View — filters / sorting / outline / columns | Grid has column filter/visibility/reorder/resize/preferences; collapse and expand operations exist | Filter and column actions live in grid controls; sorting/outline-level ribbon commands absent | PARTIAL |
| Format — grid row text styles | Existing row text-format API and NEXUS token selectors | Requires editable plan and selected row | PARTIAL — grid text formatting only; Gantt formatting absent |
| Report | Critical-path diagram export exists in its own Network view | Not exposed through planning ribbon; no ribbon report inventory integration | MISSING |
| File | Import Plan is available on Project tab; no planning export/print surface found in this audit | Import respects planning write access | MISSING — no File/Backstage tab or plan export/print integration |
| Quick Access / Save / Undo / Redo | No workspace command bar or persisted transaction history | Auto-saving edits do not imply a Save command | MISSING — do not expose nonfunctional buttons |

### Phase 2 change and verification

`PlanningRibbonTabs` now implements roving tab focus, Left/Right navigation that follows LTR/RTL direction, and Home/End. The active tab alone participates in the normal tab sequence. The workspace adds an accessible Collapse/Expand Ribbon control; ribbon body commands remain connected to the same existing grid/workspace handlers. English and Arabic labels were added. Visual UAT caught and removed an empty toolbar gap on Task/Resource/Format tabs. This collapse state is currently component-session state and does not persist across navigation/reload.

Regression: `planning-grid.test.tsx` 35/35 and new `planning-ribbon.test.tsx` 2/2 passed. React TypeScript project check passed; targeted ESLint passed. Live browser UAT on the authenticated Project Manager session switched Project → Task with ArrowRight, collapsed and expanded the ribbon, and confirmed task commands disappear/reappear. The Scenario data remained unchanged. Browser screenshot: `ribbon-uat.png` in the Codex task workspace.

The Vite production build passed after the final Ribbon layout change (2,546 modules; 1m17s); it continues to report large output chunks above 500 kB, including the existing PDF/Excel bundles.

### Additional Microsoft Desktop reference check

Current Microsoft Support guidance for Project Desktop confirms that WBS outline actions support selecting one or more tasks, linking is a multi-selection command, and users can access actions from the Task ribbon or keyboard. The official shortcut list also documents selection extension, row/column selection, fill down, undo, link/unlink, and scheduling-mode shortcuts. These are useful parity gates, not claims that this browser workspace already implements the same shortcuts. Microsoft describes views as different projections of shared project data: sheet, Gantt, network, form, usage, table, filter, and group components. NEXUS should continue to use shared canonical schedule data across its grid/Gantt/network.

References: [Edit a project in Project desktop](https://support.microsoft.com/en-us/project/edit-a-project-in-project-desktop), [Keyboard shortcuts for Project](https://support.microsoft.com/en-us/accessibility/project/keyboard-shortcuts-for-project), [Overview of Project views](https://support.microsoft.com/en-us/project/overview-of-project-views), [Use the keyboard to work with the ribbon](https://support.microsoft.com/en-us/accessibility/windows/use-the-keyboard-to-work-with-the-ribbon), and [Use a screen reader to explore and navigate Project](https://support.microsoft.com/en-us/accessibility/project/use-a-screen-reader-to-explore-and-navigate-project). Microsoft’s shortcut page distinguishes desktop from web-only commands; this report uses desktop behavior as a functional target and adapts keyboard behavior to browser conventions.

No backend, schema, data, permission, or version behavior was changed in this phase; no migration or database action was needed. Phase-2 completion is PARTIAL. Open acceptance areas still include File/Report integration, undo/redo, multi-row selection and atomic bulk operations, clipboard grid ranges/tabular paste/fill-down, direct baseline/report/security UAT, all-tab command coverage, ribbon persistence/responsive/contrast review, and the previously listed scheduling, resource, localization/theme, and performance gates. No full Microsoft Project Desktop parity claim is made.

## Live Development browser verification — 2026-10-22

Authenticated as the user's Project Manager on project PRJ-1001 (public id cde8483e-5be5-49d3-9a23-7dbc3ab883f0). Running API started after the updated library was built; SHA256 of its ProjectPlanning DLL matches the verified output (651E16A955EF64D57BA412DC5F83FD2814EE7EFE87B77FFE4522BECD8E80DBCB).

Created a separate Scenario plan named `UAT Grid Summary 2026-10-22`; this fixture remains available for review. First activity created as Manual and duration 1. First Automatic subtask inherited its start, duration 1; parent immediately acquired start/finish/duration without dependencies. Editing child to Nov 2 and duration 5 produced parent Nov 2–6 / 5 working days. Added an independent overlapping child Nov 5 / 7 working days. Parent became Nov 2–13 / 10 working days, not 12. All predecessor cells remain empty. Reload retained the complete schedule and chosen Scenario. Split Gantt showed summary bracket and bars on the same boundaries. Existing Execution plan was not edited. Browser screenshots are saved in the Codex task workspace as summary-uat.png and summary-table-uat.png.

Live UAT found and fixed a second defect: Grid date-range validation rejected an Automatic task's edited start beyond its old finish, before the engine could recompute finish. Validation now rejects inconsistent explicit ranges for Manual tasks; Auto date edits reach the canonical backend Start/Finish anchor calculation. Added Auto/Manual regression cases and made the existing invalid-finish test explicitly Manual. No API contract/schema/migration changes in this follow-up. Broader parity gaps above remain outstanding; this verifies the core summary scenario, not full Desktop parity or direct SQL Server inspection.


## Final functional gap closure — acceptance snapshot (2026-10-22)

This snapshot supersedes older ribbon and gate-status summaries above. It records source and runtime evidence without treating tests or Swagger as persisted user acceptance.

### Latest implementation and verification

The React/Vite ribbon now presents File, Task, Resource, Project, View, Format, and Report tabs. File reuses plan import and exports a localized Excel workbook with Plan Info and Tasks sheets, readable identifiers, and row-based predecessor references. Report reuses the existing critical-path/network view and, when StatusReport.View is granted, routes to the existing status-report page. No duplicate reporting engine was added. Undo/Redo commands and shortcuts were subsequently removed from the planning UI at the user's request; persisted history records remain in place. Task Ribbon bulk scheduling-mode and progress commands use the existing atomic activity bulk-update API for selected leaf activities; summary rows, invalid selections, and selections above 500 are disabled.

Frontend verification: TypeScript check passed; targeted ESLint passed; PlanningGrid, export, and Ribbon tests passed 51/51 across three files; Vite production build passed with 2,547 modules. Existing large-chunk warnings remain for Excel/PDF and the main bundle.

The current NEXUS.Api.Tests verification output compiled successfully and 68/68 tests passed (zero failed). A separate direct ProjectPlanning build attempt returned exit code 1 without diagnostic details, so it is not counted as a successful standalone build. The previous report's 441/441 result predates this backend revision and is not used as evidence for this pass.

The active API on port 5000 returns /health = 200. Swagger includes bulk activity update and grid history but lacks the source's /wbs/bulk-placement endpoint. This pass also started the isolated verification host on 127.0.0.1:51237 with database initialization skipped: /health returned 200, and Swagger contained bulk placement, bulk activity update, and grid history. The process was stopped after verification. This does not make the active host on port 5000 current. SQL Server Integrated Authentication from the isolated host failed with: The target principal name is incorrect. Cannot generate SSPI context. The browser is unauthenticated at /login. No plan edits, database writes, seed/reset, migrations, or changes to official Execution Plans were made in this verification.

### Final acceptance table

| Feature | Result | Evidence |
|---|---|---|
| Live API matches source | FAIL | Port 5000 is healthy, but Swagger lacks /wbs/bulk-placement present in source. |
| Development DB connectivity | BLOCKED | Isolated API connection failed with the SQL Server SSPI principal-name error. |
| Multi-cell Paste persistence | BLOCKED | TSV paste is implemented and frontend-tested; authenticated SQL Server save/reload could not run. |
| Fill Down persistence | BLOCKED | Fill Down is implemented and frontend-tested; persisted save/reload could not run. |
| Bulk Link/Unlink persistence | BLOCKED | Atomic bulk predecessor handlers exist; persistence and preservation of external predecessors could not be checked live. |
| Atomic bulk Indent/Outdent | BLOCKED | Bulk WBS placement has validation, row-version checks, transaction, roll-up, audit, and history; live persistence is unverified. |
| Complete selection integration | FAIL | Grid cell/range/row and keyboard selection exist. Context-menu structural commands do not consistently operate on the complete selected set. |
| Persistent Undo | BLOCKED | Actor-scoped audit-backed history handles tracked activity, dependency, and WBS structural operations; live Undo is unverified and task creation/deletion are unsupported. |
| Persistent Redo | BLOCKED | Same history and limits as Undo; live Redo is unverified. |
| File Ribbon | FAIL | Import and Excel export are connected and frontend-tested. Print/PDF and plan information are not integrated. |
| Task Ribbon | FAIL | Existing task actions, bulk mode/progress, and dependency commands are connected; full Desktop coverage and authenticated UAT are incomplete. |
| Resource Ribbon | BLOCKED | Existing authorized assignment workflow is reused; authenticated interaction could not be verified. |
| Project Ribbon | FAIL | Plan/version, calendar, and baseline workflows are connected. Explicit recalculation and status-date controls are absent. |
| View Ribbon | FAIL | Grid/Gantt/Split/Network switching, zoom, and baseline display work in source. The outline-level dropdown was removed; sorting and full filter integration remain incomplete. Row collapse/expand remains in the grid and timeline. |
| Format Ribbon | FAIL | Selected-row text formatting is connected; Gantt formatting and broader contextual parity are absent. |
| Report Ribbon | BLOCKED | Existing critical-path and permission-gated status-report workflows are connected; authenticated runtime behavior was not checked. |
| Quick Access Toolbar | REMOVED | Undo/Redo buttons and keyboard shortcuts were removed from the planning UI at the user's request; stored history remains available to the backend. |
| Scheduling parity | FAIL | Canonical scheduler and summary roll-up are reused. Gaps remain in transitions, constraints, resource calendars/work calculations, diagnostics, progress semantics, and WBS conversion. |
| Security tests | FAIL | Four added negative service cases cover cycle, cross-version parent, other-tenant plan, and missing row-version; the current NEXUS.Api.Tests run passed 68/68. The requested matrix (ReadOnlyViewAs, frozen versions, cross-project references, undo conflicts, concurrent bulk edits) is still incomplete. |
| EN/AR/RTL | BLOCKED | Localized labels and RTL-aware Ribbon keyboard navigation are source-tested. Rendered bilingual browser checks require an authenticated session. |
| Light/Dark | NOT TESTED | No visual theme acceptance run. |
| 500/1000/5000 benchmarks | NOT TESTED | No representative benchmark run. |
| EF migrations / DEV update | PASS | Command history reuses the existing append-only audit store; no EF entity/mapping change or migration was needed. No Development database update was made. |
| End-to-end browser UAT | BLOCKED | Session is logged out and the matching active API/database could not be established. Earlier Summary Task UAT covers only that scenario. |

**Final status: C — PARITY INCOMPLETE.** Source coverage advanced, but active API mismatch and SQL Server SSPI prevent persisted acceptance. Ribbon, scheduling, security, localization/theme, and scale gates remain as listed. No full Microsoft Project Desktop parity claim is made.

## Continuation — verification and selection closure (2026-10-23)

This continuation supersedes the earlier selection, build, and test entries where newer evidence is listed.

### Changes completed

- Added whole-column selection by clicking a grid column header or pressing Ctrl+Space in the grid, and whole-row selection with Shift+Space. Existing selection state continues to drive copy, formatting, and supported bulk commands. EN and AR keyboard hints identify the shortcuts.
- Confirmed that hierarchy commands invoked from a context menu preserve an existing multi-row selection and route valid sibling selections through the atomic bulk WBS placement call. Added regression coverage for context-menu Indent.
- Added a View ribbon outline-level selector that collapses WBS branches below the chosen level, shares the same collapsed rows with grid/Gantt views, and saves the current user table preference. Added row-model coverage for level-two collapse and preference-state recognition.
- Kept Next.js and the planning domain/schema unchanged. No migration, database write, seed, reset, or official Execution Plan edit was made.

### Verification

| Check | Result | Evidence / limit |
|---|---|---|
| ProjectPlanning module build | PASS | `dotnet build` succeeded with zero warnings and zero errors after allowing the IKVM Maven task to start its required x64 task host. |
| Full solution build in the restricted shell | BLOCKED | It stopped before compilation at IKVM `MavenReferenceItemImport` (`MSB4216` task-host creation; follow-on `MSB4027`). The user's active `NEXUS.Api` process also locks the default API output, so test outputs were isolated. |
| API regression suite | PASS | 449 passed, 0 failed, 0 skipped. Existing test-project nullable and xUnit analyzer warnings remain. |
| Planning grid and row-model regression suites | PASS | 55 passed across two files, including whole-column/whole-row keyboard selection, multi-row context-menu Indent, and outline-level collapse. |
| React/Vite production build | PASS | TypeScript and Vite completed; 2,547 modules transformed. Vite still reports large chunks for the main, PDF, Excel, and font bundles. |
| Targeted ESLint | PASS | `PlanningGrid.tsx` and `planning-grid.test.tsx`. |
| Isolated API startup | PASS | Built API started on loopback with migrations, seeders, workflow provisioning, and hosted database services disabled. `/health` and Swagger returned 200; Swagger included bulk WBS placement, bulk activity update, and grid-history routes. The verification process was stopped afterward. |

### Current acceptance matrix

| Area | Result | What is verified |
|---|---|---|
| Grid selection | PASS — source/test | Cell ranges, multiple rows, Ctrl+Space column selection, Shift+Space row selection, and context-menu structural multi-selection have regression coverage. |
| Bulk grid edits | PASS — source/test; live persistence BLOCKED | Atomic activity update, dependency link/unlink, WBS placement, TSV paste, and Fill Down are implemented. Current API tests cover transaction/row-version and tenant validation. Authenticated save/reload against Development remains unverified. |
| Undo/Redo | PARTIAL — source/test; live persistence BLOCKED | Activity, dependency, and WBS structural edits are audit-backed, actor/tenant/version scoped, and reject stale row versions. Create/delete and other untracked commands remain barriers; the 30-day/1,000-event cap remains. |
| File / Task / Resource / Project / Format / Report ribbon | PARTIAL — source/build | These tabs exist in React/Vite. Task and Format actions have focused tests; File export/import, Resource assignment, Project baseline/version/calendar, and Report routes compile and reuse existing services. Complete authenticated tab-by-tab interaction is unverified. |
| View ribbon | PARTIAL — source/test | Grid/Gantt/split/network switching, zoom, baseline visibility, and persisted outline-level selection are present. Sorting and full filter integration remain open; authenticated live interaction is unverified. |
| Scheduling and reporting parity | PARTIAL | The canonical scheduler, summary roll-up, analysis, critical-path and existing status-report routes are reused. Explicit status-date/recalculation commands, full Desktop constraints/resource-calendar semantics, and full report actions remain open. |
| Authorization and tenant isolation | PARTIAL — API test | Controllers require `Planning.View` for history reads and `Planning.Manage` for undo/redo. Service regression tests cover tenant, version, row-version, atomicity, actor scope, and stale-command rejection; full hostile `ReadOnlyViewAs`/concurrency matrix remains open. |
| EN / AR / RTL | PARTIAL — source/build | Added shortcut guidance in both locales and use logical start alignment. Full rendered bilingual RTL acceptance still needs an authenticated session. |
| Theme / accessibility | NOT TESTED | No rendered light/dark or full screen-reader/keyboard audit was run. |
| 500 / 1,000 / 5,000 row performance | NOT TESTED | No representative browser benchmark or virtualization evaluation was run. |
| Active API and Development database | BLOCKED | The isolated updated host is healthy. A current GET to `localhost:5000/health` was unavailable; no active host was restarted or terminated. Development SQL port 1433 is unreachable and the earlier connection failed with `Cannot generate SSPI context`; authenticated browser acceptance remains unavailable. |

### Remaining acceptance checklist

After the Development SQL Server authentication is restored and the API can be safely restarted, verify the active server's Swagger against source and log in with an authorized project user. Then check all seven ribbon tabs on a real Working plan; exercise row/cell/range/column selection, TSV paste, Fill Down, bulk link/unlink, indent/outdent, Undo/Redo and reload; confirm resource assignments, baseline/version/calendar and report routes obey permissions; repeat in EN and AR/RTL; and measure 500/1,000/5,000-row rendering and editing. Do not use seed/reset or modify official Execution Plans for this acceptance pass.

**Continuation status: C — PARITY INCOMPLETE.** The source-level selection gap is closed and module/API builds, focused frontend tests, API regression tests, and isolated API startup are verified. Persisted acceptance remains blocked by the Development SQL Server SSPI issue and unavailable authenticated browser session; scheduling, full ribbon semantics, theme, and scale gates remain open.

## Final continuation — runtime evidence and acceptance matrix (2026-10-23)

This entry supersedes earlier runtime and database findings where the evidence below is newer. The process serving port 5000 was identified as PID 31028, running `C:\Projects\pmo\.verification\solution-build-final-artifacts\bin\NEXUS.Api\debug\NEXUS.Api.dll` in Development with `--skip-development-database-initialization`. It is the verified build artifact and was left running. No unrelated process was stopped. The startup option explicitly skips migrations, seeders, workflow provisioning, and database-backed hosted services.

The active API returned 200 from `/health` and `/swagger/v1/swagger.json`; Swagger documents 359 paths and contains the six expected bulk/history routes. A CORS preflight from `http://localhost:4100` returned 204 and allowed that origin. The Development `NexusPmo` database was reachable for a read-only identity and migration-history query under Windows Integrated Authentication; the history contained 11 migrations. No plan mutation, EF migration, seed, reset, or Development database write was performed. API service persistence tests use isolated SQLite and do not establish a write/reload result against SQL Server.

### Latest automated verification

| Check | Result | Evidence / limit |
|---|---|---|
| Full .NET solution build | PASS | Isolated artifact build completed with 0 warnings and 0 errors. |
| API regression suite | PASS | 452 passed, 0 failed, 0 skipped. Four existing warnings remain in `RiskServiceTests` and `GlobalQueryFilterTests`. |
| Grid and workspace row tests | PASS | 57 passed across `planning-grid.test.tsx` and `planning-workspace-rows.test.ts`. |
| Changed-file ESLint | PASS | Touched planning workspace, grid, and focused tests. |
| React/Vite production build | PASS | 2,547 modules transformed; existing large-chunk warnings remain. |
| API runtime | PASS | PID 31028 serves the verified artifact on port 5000; `/health`, Swagger, six planning routes, and the React-origin CORS preflight passed. Startup skipped all Development database initialization. |
| SQL Server schema/data change | NOT APPLICABLE | No entity, mapping, index, relationship, or schema change was introduced. No migration or data write was needed. |

### Required implementation matrix

| Workstream | Implementation | Automated tests | Integration |
|---|---|---|---|
| Selection | PASS — cell/range, multi-row, whole-column, whole-row, keyboard extension, and context-menu multi-selection are implemented. | PASS — selection cases are included in the 57 focused React tests. | BLOCKED — no authenticated project session for live selection acceptance. |
| Bulk Paste / Fill Down | PASS — TSV copy/paste, Fill Down, and bounded activity updates use the grid's canonical API. | PASS — focused React tests plus API transaction/row-version tests. | BLOCKED — SQL Server save/reload was not exercised. |
| Bulk WBS | PASS — multi-node placement is validated and committed atomically with scheduling, roll-up, audit, and history. | PASS — API rollback/concurrency tests and a multi-row grid command test. | BLOCKED — live persistence was not exercised. |
| Link / Unlink | PASS — selected leaf activities use one bulk predecessor request; unlink preserves external predecessors. | PASS — component and dependency-service regression coverage. | BLOCKED — no authenticated persisted browser operation. |
| Undo / Redo | PARTIAL — committed activity, dependency, WBS placement, and activity/WBS reorder changes are actor/tenant/version scoped and row-version guarded. Create/delete and untracked commands remain barriers. | PASS — persisted SQLite service tests cover edit, dependency, WBS, and reorder history. | BLOCKED — no SQL Server save/reload acceptance. |
| File Ribbon | PARTIAL — permission-gated Import and localized Excel export are connected. Print/PDF actions remain absent. | PARTIAL — import/export source is present; no complete ribbon interaction test. | BLOCKED — authenticated browser interaction not run. |
| Task Ribbon | PARTIAL — task CRUD, scheduling mode/progress, hierarchy, links, and details actions are connected as compact icon buttons with localized hover labels; complete Desktop command parity remains open. | PASS — focused grid tests and API mutation regression suite. | BLOCKED — full command-by-command browser acceptance not run. |
| Resource Ribbon | PARTIAL — opens the canonical planning assignment workflow from a compact icon button with a localized hover label; broader resource operations are not represented in this tab. | PARTIAL — underlying assignment services are covered by API tests; no focused tab-interaction test. | BLOCKED — authenticated assignment save/reload not run. |
| Project Ribbon | PARTIAL — permission-gated Create plan sits beside the plan selector; version, baseline, calendar, and explicit schedule recalculation workflows are connected. Plan status-date control remains absent. | PASS — recalculate service undo/redo regression and API suite pass; no complete ribbon test. | BLOCKED — authenticated browser interaction not run. |
| View Ribbon | PARTIAL — table/Gantt/split/network, zoom, and baseline visibility are connected; sorting and full filter integration remain open. Row-level collapse/expand remains available outside the ribbon. | PASS — row-model and query regressions passed before the latest ribbon-only change. | BLOCKED — rendered live acceptance not run. |
| Format Ribbon | PARTIAL — selected-row text formatting persists; Gantt formatting and wider contextual controls remain open. | PASS — focused multi-row format tests pass. | BLOCKED — no authenticated format save/reload. |
| Report Ribbon | PARTIAL — critical-path view and permission-gated status reports reuse existing flows; full report action coverage remains open. | PASS — network/export and API reporting regressions pass. | BLOCKED — authenticated report navigation/export not run. |
| Quick Access | PARTIAL — Undo/Redo use persisted history APIs for supported command types; unsupported create/delete remain barriers. | PASS — history availability/action and service tests pass. | BLOCKED — no SQL Server-backed Undo/Redo verification. |
| Scheduling | PARTIAL — canonical scheduler and summary roll-up are reused; explicit recalculate is available and recorded in persistent history. Full Desktop constraints, resource-calendar/work semantics, diagnostics, status date, and conversion behavior remain open. | PASS — recalculate undo/redo regression and scheduling/dependency/calendar/summary API suites pass. | BLOCKED — no live database scheduling acceptance. |
| Gantt / Critical Path | PARTIAL — shared workspace rows, baseline overlay, zoom, network diagram, and canonical critical-path analysis are connected. | PASS — focused network, export, and analysis-query tests pass. | BLOCKED — no authenticated Gantt/network acceptance. |
| Security | PARTIAL — `Planning.View`/`Planning.Manage`, active Working version scope, tenant filters, row versions, and atomic rollback are enforced in tested paths. ReadOnlyViewAs, concurrent bulk races, and the complete hostile-request matrix remain open. | PASS — 453 API tests pass, including tenant/version/concurrency cases. | BLOCKED — no authenticated cross-tenant browser/API UAT. |
| EN / AR / RTL | PARTIAL — localized command labels/hints exist in English and Arabic; layout uses RTL-aware logical alignment. | PASS — localized bundles build with the app; focused source tests pass. | BLOCKED — no rendered EN/AR/RTL browser acceptance. |
| Performance | NOT TESTED — no 500/1,000/5,000-row benchmark or virtualization evaluation. | NOT TESTED | NOT TESTED |
| EF / Development DB | PASS — no schema change; migration not required. Read-only Development database identity/history query succeeded; no write was issued. | PASS — isolated SQLite persistence suites passed. | BLOCKED — real SQL Server plan mutation and reload were not tested. |

**Final status: C — PARITY INCOMPLETE.** The source and automated gates listed above are complete, but this does not meet the requested Microsoft Project Desktop parity bar. Remaining work includes create/delete undo, complete command-level Ribbon behavior, scheduling/status-date gaps, the full hostile security and accessibility/theme matrix, scale benchmarks, and authenticated SQL Server/browser save-and-reload acceptance. The browser was logged out, so no authenticated live plan was changed or tested. Next.js remains intact.

### Final continuation — explicit schedule recalculation (2026-10-23)

The Project ribbon now includes a localized, `Planning.Manage`-gated **Recalculate schedule** command. Its API endpoint validates that the project and plan match the active Working version, then invokes the canonical scheduling and summary-rollup services. The committed schedule snapshot is recorded in the existing append-only command-history store; Undo restores the exact previous fields and Redo reapplies the recalculated snapshot. No domain/schema change or EF migration was needed. Next.js remains intact.

The user asked to stop additional testing while this continuation was in progress. Before that instruction, the current code passed the complete API suite (453/453), complete React/Vite suite (654/654), the focused recalculate test (4/4), targeted ESLint, and the Vite production build. A full solution build was started but canceled at the user's direction; the API test build compiled the changed API projects. The active API was then replaced on port 5000 with the new artifact using `--skip-development-database-initialization` and no plan/data writes.

| Workstream | Latest result |
|---|---|
| Project recalculate command | IMPLEMENTED — frontend command, localized EN/AR, permission gate, project/plan validation, canonical scheduling, history-backed undo/redo. |
| API regression | PASS — 453 passed, 0 failed, 0 skipped. |
| React/Vite regression | PASS — 654 passed across 129 files. |
| Focused recalculate regression | PASS — 4 passed, including cache/history invalidation coverage. |
| Targeted lint / Vite build | PASS — changed frontend files linted; production build succeeded with existing large-chunk warnings. |
| Full .NET solution build | STOPPED — canceled at user request; changed API projects compiled as part of the passing API test build. |
| Active API | UPDATED — PID 50416 serves port 5000; `/health` is Healthy, Swagger returns 200 with 360 paths and lists the recalculate route; startup skips Development database initialization. |
| Database / official plan | UNCHANGED — no migration, seed, reset, or plan mutation. |

**Final status remains C — PARITY INCOMPLETE.** Remaining acceptance includes authenticated save/reload on a real Working plan, broader ribbon command coverage, create/delete undo, status date and advanced Desktop scheduling semantics, the full tenant/security and accessibility/theme matrix, and 500/1,000/5,000-row performance checks. No full Desktop parity claim is made.

### Ribbon spacing and File tab correction (2026-10-23)

Removed fixed minimum widths from Project, View, and Format ribbon groups, tightened inter-group spacing, and stopped PlanningGrid from rendering an empty command strip for File and Report. The Create plan command's current location is beside the plan selector in Project, where it is shown only with planning-management permission. Next.js remains intact.

Task and Resource ribbon commands now use icon-only controls with localized native hover titles and accessible names. Progress selection uses an icon-only dropdown, with percentage choices in the menu. Automated and live visual checks were not run for this compact-icon change.

### Ribbon whitespace and View menu cleanup (2026-10-23)

Removed the View tab's outline-level dropdown while keeping the view-mode, zoom, baseline, and row-collapse controls. Removed the ribbon panel's forced minimum height, reduced padding and group gaps, and aligned its remaining controls beside the tabs. Task, Resource, and Format commands now render inside the same ribbon panel; Format retains its grid selection and mutation handlers through a React portal. Undo/Redo buttons and keyboard shortcuts have since been removed from the planning UI; persistent history records remain unchanged. No tests or live visual checks were run for these UI changes, per the user's instruction to finish the task without testing. Next.js remains intact.

### Whole-number progress in the project plan (2026-10-23)

Progress values now display as whole percentages rounded to the nearest integer across the plan grid, activity details, Gantt and network views, import preview, and plan exports. Grid edits and pasted values round before saving; activity detail saves and other plan activity updates also send rounded progress. Numeric inputs use whole-number steps and retain the existing 0–100 range. This is a presentation and input normalization change; it does not change the domain model or run a data migration. No tests or live checks were run, per the user's instruction to finish the task without testing.
