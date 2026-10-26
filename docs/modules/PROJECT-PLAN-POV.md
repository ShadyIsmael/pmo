# Project Plan Workspace — Legacy Prototype Notes and Current React/Vite Workspace

> The `apps/web` component tree and mock-data details below describe the retained Next.js prototype. They are historical design notes, not the live Project Plan implementation. Keep the Next.js application intact. The active workspace is the persisted React/Vite feature in `apps/web-react/src/features/projects/ProjectPlanningTabFull.tsx` and its shared planning API, grid, and Gantt components.

## Current implementation

The React/Vite workspace uses the canonical Plan/PlanVersion API and scheduling services. It provides File, Task, Resource, Project, View, Format, and Report ribbon tabs; a persisted planning grid; shared Gantt and network views; task details; calendar, assignment, import, version, and baseline flows. File contains permission-gated plan import and localized Excel export. Project places permission-gated plan creation beside the plan selector. Task and Resource ribbon actions use compact icon-only buttons with localized hover titles and accessible names; progress selection is also an icon-only dropdown whose percentage options appear when opened. Task, Resource, and Format actions render in the shared ribbon panel. Ribbon command groups size to their contents, and the ribbon no longer reserves empty minimum height or pushes controls away from the tabs. Undo/Redo commands and shortcuts are removed from the planning UI; the persisted history records remain available to the backend. View provides view modes, zoom, and baseline display; row collapse and expand remain available through grid and timeline controls, without an outline-level dropdown. The Report tab opens the existing critical-path view/export and permission-checked project status reports. Grid selection supports cell ranges, multiple rows, whole-column selection with Ctrl+Space, whole-row selection with Shift+Space, and keyboard or drag extension; structural context-menu actions preserve and apply valid multi-row selections. Grid copy/paste uses TSV; activity field pastes, Task ribbon bulk scheduling-mode/progress changes, and Format ribbon changes use the permission-protected atomic bulk activity endpoint, while Ctrl+D fills a selected range down. Multi-node WBS placement uses one permission-protected transaction, and tracked activity, dependency, and WBS structural commands participate in persistent history. Create/delete and other untracked commands remain history barriers. The parity report `docs/reports/PROJECT-PLAN-DESKTOP-PARITY.md` records verified behavior and open gaps. Browser UAT of the new File/Report actions, full Gantt formatting, broad Desktop scheduling/security parity, and live SQL Server acceptance remain incomplete.

Resource assignment creation checks the loaded assignments for the selected activity/resource pair before submitting. The API remains authoritative for duplicate prevention; a duplicate or stale-write `409` keeps the form open, refreshes assignments, and shows a localized message instead of an unhandled promise rejection. The React assignment form uses the standard large Side Sheet with grouped assignment and schedule fields, associated labels (including both dates), and EN/AR section headings. Deleted plan assignments remain in history but no longer block re-adding the same resource to the same activity: the unique database index applies only where `IsDeleted = 0`, through migration `20261026140000_AllowPlanAssignmentReassignmentAfterDelete`.

## Retained Next.js prototype reference

## Objective

Build an interactive, frontend-only proof-of-concept for the Project Plan Workspace at `/[locale]/projects/[id]/plan`. This is a pure frontend prototype using deterministic mock data — no backend, no database, no migrations, no API controllers, no scheduling engine.

## Architecture

The workspace is a single-page composition of independent, loosely coupled components orchestrated by `PlanningWorkspace.tsx`. All state lives in `useState` / `useCallback` hooks within this orchestrator; no global state or React context is used.

### Component Tree

```
page.tsx (route)
└── PlanningWorkspace (orchestrator)
    ├── ProjectContextHeader (project banner + actions)
    ├── PlanningToolbar (view/filter/display actions)
    ├── SplitPane
    │   ├── TaskGrid (left) — hierarchical tree grid
    │   └── GanttChart (right) — timeline + bars
    ├── FilterBar (search + filter toggles)
    ├── ColumnChooser (column visibility)
    ├── PlanSummaryPanel (collapsible stats card)
    └── TaskDetailsDrawer (slide-over details — 8 tabs)
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| No React Context / global state | Workspace is single-page; prop drilling is simpler and more explicit |
| `cloneTasks` deep clone before mutation | Avoids direct state mutation; React re-renders correctly |
| `rebuildWBS` WBS recomputation after mutations | Keeps WBS codes in sync with tree structure |
| Task agent for GanttChart and TaskDetailsDrawer | Complex visual/UI logic generated autonomously; reviewed & integrated manually |
| `dayjs` for date math | Already used in codebase; `date-fns` would work too but we avoided adding deps |
| Deterministic mock data in single file | `plan-mock-data.ts` exports typed `PlanTask[]`, `ProjectInfo`, `PlanConfig`; UI never contains mock logic |

## Mock Data

- **File**: `apps/web/src/lib/plan-mock-data.ts`
- **Project**: "Core Banking Modernisation"
- **Scope**: 7 phases, ~80+ tasks, 11 milestones, with dependencies, resources, costs, baselines, predecessors/successors
- **Task hierarchy**: Up to 4 levels deep
- **Data types covered**: Summary, Task, Milestone, with full PlanTask interface coverage

## Components

### `PlanningWorkspace.tsx` (orchestrator, 667 lines)
- Manages all state: `rootTasks`, `selectedId`, `drawerOpen`, `expandedIds`, `view`, `filters`, `columnVisibility`, `searchQuery`, `checklist`
- Handlers for: Add/Move/Delete/Duplicate/Indent/Outdent/Reorder tasks, add milestone, add summary, add subtask
- View mode switching: Gantt, Milestones, Critical Path, Baseline Comparison, Resource View
- Clipboard operations with toast notifications
- `useMemo`-driven derived state: `flattenedTasks`, `filteredTasks`, `viewData`

### `TaskGrid.tsx` (hierarchical tree grid)
- Recursive tree rendering with indentation per depth level
- Expand/collapse with chevron on summary rows
- 22 column types: WBS, Name, Type, Mode, Duration, Start, Finish, Predecessors, Resources, Work, Cost, Status, Priority, Owner, etc.
- Inline editing for Name, Duration, Start, Finish, Status, Priority, Owner (via contentEditable or select)
- Context menu: Add Task Above/Below, Add Subtask, Indent, Outdent, Duplicate, Delete, Add Milestone
- Drag-and-drop reorder (up/down, no cross-tree drag yet)
- `Shift+Enter` to add sibling below, `Ctrl+Enter` to add subtask
- Column resizing via mouse drag

### `GanttChart.tsx` (371 lines, task agent)
- Timeline header with configurable scale: Days (daily columns), Weeks (month + week rows), Months (month columns)
- Zoom slider from 12px to 80px per day
- Summary bars with dark header + lighter body
- Task bars (blue), milestone diamonds (diamond shape)
- Progress fill on task bars
- Dependency arrows between predecessor finish and successor start
- Baseline bars (grey, above task bars) when `baselineStart` / `baselineFinish` exist
- Today line (red dashed vertical)
- Weekend shading (grey background on Sat/Sun columns)
- Critical path highlighting (red/orange bars)
- Horizontal scroll sync with TaskGrid (via shared `scrollLeft` state)

### `TaskDetailsDrawer.tsx` (445 lines, task agent)
- Shadcn Sheet (slide-over from right)
- 8 tabs:
  1. **General** — name, description, type, status, priority, owner, WBS
  2. **Schedule** — start, finish, duration, actual dates, constraint type/date, deadline, total/free slack
  3. **Dependencies** — predecessor list with type (FS, FF, SS, SF) and lag
  4. **Resources** — resource names, work, actual/remaining work
  5. **Costs** — planned, actual, remaining, forecast, committed, variance
  6. **Baseline** — baseline start/finish/duration/work/cost vs current
  7. **Notes** — text area for notes
  8. **Workflow** — external ID, key, sync status, last sync, external link

### `ProjectContextHeader.tsx`
- Project name, status, health indicator, date range
- Action buttons: Save, Publish, Create Baseline, Import, Export, Undo, Redo (all toast-only stubs)

### `PlanningToolbar.tsx`
- Compact bar: View selector, zoom, Add Task/Milestone, Indent/Outdent, Filter, Column Chooser, Expand/Collapse All, Wrap Text toggle, Auto Schedule (stub)

### `SplitPane.tsx`
- Horizontal resizable split (left = grid, right = gantt)
- Drag handle in center
- Collapsible left or right pane (double-click handle)

### `FilterBar.tsx`
- Search input (filters by name)
- Status multi-select checkboxes
- Type filter (Task/Milestone/Summary)
- Quick toggles: Critical Path, Delayed, Milestones Only

### `ColumnChooser.tsx`
- Dialog with checkboxes for each column type
- Persisted in local state

### `PlanSummaryPanel.tsx`
- Collapsible panel showing task counts, total work, total cost, baseline variance

## Files

| File | Lines | Purpose |
|---|---|---|
| `apps/web/src/lib/plan-mock-data.ts` | ~336 | Deterministic mock data |
| `apps/web/src/components/planning/PlanningWorkspace.tsx` | 667 | Main orchestrator |
| `apps/web/src/components/planning/TaskGrid.tsx` | ~260 | Hierarchical tree grid |
| `apps/web/src/components/planning/GanttChart.tsx` | 371 | Gantt timeline visualization |
| `apps/web/src/components/planning/TaskDetailsDrawer.tsx` | 445 | Task details slide-over |
| `apps/web/src/components/planning/ProjectContextHeader.tsx` | ~70 | Project header |
| `apps/web/src/components/planning/PlanningToolbar.tsx` | ~150 | Toolbar |
| `apps/web/src/components/planning/SplitPane.tsx` | ~120 | Resizable split pane |
| `apps/web/src/components/planning/FilterBar.tsx` | ~100 | Search + filters |
| `apps/web/src/components/planning/ColumnChooser.tsx` | ~80 | Column visibility |
| `apps/web/src/components/planning/PlanSummaryPanel.tsx` | ~70 | Summary card |
| `apps/web/src/app/[locale]/(dashboard)/projects/[id]/plan/page.tsx` | ~15 | Route page |

## Persisted React/Vite planning grid

The persisted planning grid lives in `apps/web-react` and uses the Project Planning API and automatic scheduling engine. The first activity created in a plan is automatically set to Manual scheduling and uses the project's planned start date when that date is available. For a first milestone, start and finish use that same date so it remains zero-duration in Manual mode. If the project has no planned start date, its start remains unset until a user supplies one. Later activities default to Automatic scheduling. New tasks default to a one-working-day duration in the API when no positive duration is supplied; milestones remain zero-duration. An activity with effective WBS children is a derived Summary Task: start is the earliest descendant start, finish is the latest descendant finish, and duration is the working-time span on its selected/default planning calendar. Dependencies are not required. Legacy milestone containers convert to Task summaries without removing their child relationships; leaf milestones remain zero-duration.

The React/Vite grid supports rectangular cell selection, multi-row selection, whole-column and whole-row selection, TSV copy/paste, Fill Down, and bulk activity field updates. The Task Ribbon links selected leaf activities in visible order, can remove dependencies internal to the selected set while retaining external predecessors, and applies scheduling-mode or progress changes to selected leaf activities in one atomic activity update. Summary/WBS rows and invalid selections are excluded. Progress is displayed and accepted in whole percentages rounded to the nearest integer throughout plan editing, details, Gantt/network views, and exports; the UI sends rounded values when saving a plan activity. Activity, dependency, and WBS bulk APIs enforce `Planning.Manage`, active Working Plan Version scope, per-row concurrency where applicable, validation limits, canonical scheduling/summary recalculation, and transaction rollback. Bulk WBS placement and tracked activity/dependency/WBS edits and reorder operations record append-only audit events and persistent history, with actor/version/row-version checks on undo and redo. Service and component tests cover these source behaviors. A later read-only check connected to Development SQL Server under Windows Integrated Authentication, but no live plan mutation/reload was run; SQL Server write persistence remains unverified. File/Report tabs connect to the existing import, critical-path, and status-report workflows and add a localized Excel plan export. Task creation/deletion undo and broader Project Desktop parity remain open.

When an automatic subtask is created under a dated WBS activity, the API uses the parent's start date as the new task's initial start anchor if no dates were supplied. Summary start, finish, duration and progress are derived and read-only in the grid, including when the activity retains Manual mode. Leaf Manual activities retain their explicit dates.

## Historical Next.js prototype limitations

The following limitations describe the retained mock-data prototype under `apps/web`; they do not describe the persisted React/Vite workspace documented above. Keep the Next.js application intact.

- No backend persistence — all state is in-memory, lost on refresh
- No undo/redo beyond stubs
- No real scheduling engine (Auto Schedule is a stub)
- No real resource leveling
- Drop-down select for Status/Priority uses native `<select>` instead of shadcn Select (to maintain editability within the grid)
- Cross-tree drag-and-drop not yet implemented (up/down reorder only)
- Performance not optimized for 1000+ tasks (no virtualization in TaskGrid)
- Print/PDF export is a stub
- Build requires deleting `.next` cache on Windows occasionally due to ENOENT rename race

## Build Verification

```bash
cd apps/web
npx tsc --noEmit        # TypeScript check (must pass clean)
npx next build          # Next.js build (must succeed)
npm run dev             # Dev server at http://localhost:3000
```


### Ribbon and runtime acceptance note — 2026-10-22

This is a historical 2026-10-22 snapshot. The later API and database checks are recorded below and in `docs/reports/PROJECT-PLAN-DESKTOP-PARITY.md`.

### Latest implementation and runtime note — 2026-10-23

The React/Vite workspace has all seven ribbon tabs. Undo/Redo commands and keyboard shortcuts are no longer exposed in the planning UI; persistent history still records supported activity, dependency, WBS-placement, reorder, and recalculate changes. Column/row selection, context-menu multi-selection, row-level collapse/expand, TSV/Fill Down, bulk activity/dependency/WBS operations, tracked activity/WBS reordering, and explicit schedule recalculation have regression coverage. Recalculation calls the canonical scheduler and checks active Working-version scope and `Planning.Manage`. Current continuation verification passed 453 API tests and 654 React/Vite tests; the focused recalculate test passed 4/4, changed-file ESLint and the Vite production build passed. The full .NET solution build was stopped at the user's request after the changed API projects compiled in the API test build. The updated API artifact is running on port 5000 with Development database initialization skipped. No migration, seed, reset, or plan write occurred. Authenticated browser persistence, the remaining ribbon/scheduling/security/accessibility/theme gaps, and performance benchmarks remain open. See the current matrix in `docs/reports/PROJECT-PLAN-DESKTOP-PARITY.md`.
