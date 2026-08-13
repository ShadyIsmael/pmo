# Configurable POV Dashboard

## Overview

The configurable dashboard is the main landing page after login (`/[locale]/home`). A Point of View (POV) is a context and catalogue filter, not a fixed dashboard. Each POV contains multiple named personal and shared dashboards.

## Architecture

```
DashboardBuilder (state orchestrator)
  ├── DashboardHeader (controls, filters, POV selector, view selector)
  ├── DashboardFilters (date range, portfolio, program, project)
  ├── DashboardGrid (react-grid-layout - desktop/tablet)
  ├── WidgetShell (container wrapper)
  │   └── Widget Component (kpi, chart, list, integration, ai)
  ├── WidgetLibrary (sheet - browse + add widgets)
  ├── WidgetSettings (sheet - configure widget)
  └── DashboardViewManager (dialog - manage saved views)
```

## Data Models

All models defined in `apps/web/src/components/dashboard-builder/dashboard-types.ts`.

### WidgetDefinition
Describes a widget type (singleton via registry):
- `type` - unique identifier
- `name`, `description`, `category`, `icon`
- `supportedPOVs[]` - which roles can use this widget
- `requiredPermission?` - permission gate
- `requiredPlugin?` - plugin gate
- `minSize`, `defaultSize`, `maxSize` - grid constraints
- `defaultConfig` - default WidgetConfig values
- `configFields[]` - dynamic settings form schema
- `dataSourceLabel?` - "Mock data" / "Mock AI data" label

### WidgetInstance
An instance of a widget on a dashboard view:
- `id`, `type`, `title`, `config`, `position`, `collapsed`
- `lastUpdated`, `loading`, `error`

### WidgetConfig
Per-instance configuration:
- `customTitle`, `portfolio`, `program`, `project`
- `dateRange`, `health[]`, `status[]`, `owner`, `resource`, `department`
- `timesheetStatus`, `billableStatus`, `maxRecords`
- `chartType`, `groupBy`, `sortBy`
- `showLegend`, `showTrend`, `includeCompleted`

### DashboardView
A named collection of widget instances:
- `id`, `name`, `description`, `pov`, `widgets[]`
- `ownerId`, `ownerName`, `type` (`personal` or `shared`)
- `access` (`view`, `duplicate`, `edit`, or `manage-sharing`)
- `sharedWith[]`, per-dashboard `filters`
- `isDefault`, `createdAt`, `updatedAt`

### DashboardState
Persisted to `localStorage`:
- `currentViewId`, `views[]`, `pov`, `lastViewByPOV`, `editMode`, `lastUpdated`

## Points of View (POVs)

| POV | Description | Layout Focus |
|-----|-------------|--------------|
| Executive | High-level org performance | KPIs, health, budget, risks, AI |
| Project Manager | Project health + team | Attention items, milestones, tasks, costs |
| Portfolio Manager | Portfolio oversight | Health, status, budget, risks |
| Team Member | Personal workspace | To-do, timesheets, meetings, approvals |
| Resource Manager | Capacity + utilisation | Utilisation, capacity, overallocation |
| Finance Controller | Financial oversight | Budget, variance, costs, approvals |
| PMO Administrator | Governance + ops | Health, integrations, risks, AI |

POV switching opens the last dashboard used in that POV. On first use, a personal default-template dashboard and a shared PMO standard dashboard are seeded. POV does not grant permissions.

## Saved Dashboard Lifecycle

- Create from a blank canvas, the current POV default template, or a duplicate of an existing dashboard.
- Name, rename, duplicate, delete, switch, and set a default dashboard within the current POV.
- Each dashboard owns its widget instances, layout, global filters, and default flag.
- The last opened dashboard is remembered independently for every POV.
- Personal dashboards are owned by the current user.
- Shared dashboards display their owner and access level. A shared dashboard can be view-only, duplicable, editable, or sharing-managed.
- Duplicating a shared dashboard creates an editable personal copy.
- Owners can create shared dashboards and change their mock access level in Manage Dashboards.

## Widget Categories

Overview, Projects, Portfolios, My Work, Schedule, Resources, Timesheets, Costs, Risks, Meetings, Approvals, Notifications, Integrations, AI

## All Widgets

| Widget | Type | Category | Data Source Label |
|--------|------|----------|-------------------|
| Active Projects | KPI | Overview | - |
| Projects at Risk | KPI | Overview | - |
| Total Budget | KPI | Overview | - |
| Forecast Variance | KPI | Overview | - |
| Projects by Health | Chart | Projects | - |
| Projects by Status | Chart | Projects | - |
| Projects Requiring Attention | List | Projects | - |
| Upcoming Milestones | List | Schedule | - |
| My To-do List | List | My Work | - |
| Overdue Tasks | KPI | My Work | - |
| Resource Utilization | Chart | Resources | - |
| Resource Capacity | Chart | Resources | - |
| Overallocated Resources | List | Resources | Mock data |
| My Timesheet Status | KPI | Timesheets | - |
| Hours This Week | KPI | Timesheets | - |
| Missing Timesheets | List | Timesheets | Mock data |
| Timesheet Approval Inbox | List | Timesheets | Mock data |
| Planned vs Actual Work | Chart | Timesheets | - |
| Budget vs Actual | Chart | Costs | - |
| Cost Variance | List | Costs | - |
| Open Risks | KPI | Risks | - |
| Top Risks | List | Risks | Mock data |
| Risk Trend | Chart | Risks | Mock data |
| Risk Heat Map | Chart | Risks | Mock data |
| Pending Approvals | List | Approvals | - |
| Upcoming Meetings | List | Meetings | - |
| Recent Notifications | List | Notifications | Mock data |
| Jira Sync Status | KPI | Integrations | Mock data |
| MS Project Import Status | KPI | Integrations | Mock data |
| Integration Health | Chart | Integrations | Mock data |
| AI Recommendations | List | AI | Mock AI data |
| Likely Delays | List | AI | Mock AI data |

## Modes

### View Mode
- Widgets display live data
- Drag-to-move disabled
- Resize disabled
- Header shows: POV selector, View selector, Refresh All, Edit button, Add Widget button
- External Link icon opens related page

### Edit Mode
- Widget library accessible
- Drag-to-move enabled (via grip handle)
- Resize enabled
- Header shows additional: Save View, Manage Views, Reset buttons
- Widget actions: Settings, Duplicate, Remove, Collapse

## Responsive

| Breakpoint | Columns | Behaviour |
|-----------|---------|-----------|
| >= 1024px (Desktop) | 12 | Drag + resize via react-grid-layout |
| 640-1023px (Tablet) | 6 | Drag + resize |
| < 640px (Mobile) | 1 | Move up/down buttons, no resize |

## i18n / RTL

The dashboard uses the app's existing next-intl setup. Layout automatically flips for Arabic (RTL) via the `dir` attribute on the HTML element. The grid and all components respect RTL layout.

## localStorage

State is persisted under the versioned key `nexus-pmo-dashboard-v2`:
- Saves on every state change
- Restores on page load
- Supports multiple dashboard views per POV
- Remembers the last dashboard used per POV
- Migrates the previous `nexus-pmo-dashboard` shape without discarding its widgets

## POV, Role, and Dashboard

- **Role/permissions** determine allowed data and actions.
- **POV** organizes relevant dashboards and widgets.
- **Saved Dashboard** is a named personal or shared collection of configured widget instances.
- **Widget Instance** is independently configured and positioned.
- Switching POV never elevates permissions.

## Click Actions

| Element | Navigates To |
|---------|-------------|
| Project widget items | `/[locale]/projects` |
| Milestone widget items | `/[locale]/projects/gantt` |
| Timesheet widgets | `/[locale]/my-work/timesheets` |
| Approval widgets | `/[locale]/my-work/approvals` |
| View All (External Link) | Correct module page |

## File Locations

```
apps/web/src/components/dashboard-builder/
├── DashboardBuilder.tsx         # Main orchestrator
├── DashboardHeader.tsx          # Controls + filters
├── DashboardGrid.tsx            # react-grid-layout wrapper
├── DashboardFilters.tsx         # Date/portfolio/program/project filters
├── DashboardViewManager.tsx     # View CRUD dialog
├── WidgetShell.tsx              # Widget container
├── WidgetLibrary.tsx            # Widget browser sheet
├── WidgetSettings.tsx           # Widget config sheet
├── dashboard-types.ts           # All type definitions
├── dashboard-storage.ts         # localStorage helper
├── widget-registry.ts           # Widget registration + helpers
├── default-layouts.ts           # POV-specific default layouts
├── widgets/
│   ├── kpi-widgets.tsx          # Number/metric widgets
│   ├── chart-widgets.tsx        # Bar/line/pie/gauge widgets
│   ├── list-widgets.tsx         # Item list widgets
│   ├── integration-widgets.tsx  # Integration status widgets
│   ├── ai-widgets.tsx           # AI insight widgets
│   └── extra-widgets.tsx        # Timesheet + cost + risk widgets

apps/web/src/app/[locale]/(dashboard)/home/page.tsx  # Route page
apps/web/src/lib/dashboard-mock-data.ts               # Mock data layer
```

## Extending

1. Add mock data function in `lib/dashboard-mock-data.ts`
2. Create widget component in `widgets/` file
3. Register via `registerWidget()` in the same file
4. Add to default layout in `default-layouts.ts` (optional)
5. Add to view details route map in `DashboardBuilder.tsx` (optional)

## Constraints

- No backend, APIs, DB entities, or migrations
- All data is mock data
- AI and Integration widgets display "Mock data" / "Mock AI data" labels
- Same widget type can be added multiple times with different configurations
- Widgets user lacks permission for are hidden from the library
- POV switching does not change permissions
- Frontend sharing and ownership are demonstrative only; production enforcement requires backend authorization and persistence
