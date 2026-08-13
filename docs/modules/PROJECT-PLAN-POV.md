# Project Plan Workspace — Frontend POV / Prototype

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

## Limitations

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
