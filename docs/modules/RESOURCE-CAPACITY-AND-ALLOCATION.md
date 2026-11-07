# Resource Capacity and Allocation

## Overview
Provides capacity planning, resource allocation CRUD, and resource finder capabilities. Built on existing Resource, Project, and ResourceAllocation entities with no duplicate models.

## Backend API Endpoints

### Allocations (`/api/allocations`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Resource.View | Paged list (filterable by resourceId, projectId, status) |
| GET | `/{id}` | Resource.View | Single allocation |
| GET | `/by-resource/{resourceId}` | Resource.View | Allocations for a resource |
| GET | `/by-project/{projectId}` | Resource.View | Allocations for a project |
| GET | `/conflicts` | Resource.View | Conflict detection for a resource + date range |
| POST | `/` | Resource.Assign | Create allocation |
| PUT | `/{id}` | Resource.Assign | Update allocation |
| DELETE | `/{id}` | Resource.Assign | Soft-delete allocation |

**Create/Update DTO fields:** ResourceId, ProjectId, ProjectName, WorkItemId, Role, AllocationPercentage, HoursPerWeek, StartDate, EndDate, Status (Confirmed/Proposed/Tentative/Cancelled), IsBillable, Notes

### Capacity (`/api/capacity`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/matrix` | Resource.View | Capacity matrix with per-resource/per-period cells, heatmap states, summary |
| GET | `/availability` | Resource.View | Resource availability list sorted by available hours |
| GET | `/demand-vs-capacity` | Resource.View | Aggregated demand vs capacity by role/department |
| GET | `/gantt` | Resource.View | Flat list of allocations for Gantt rendering (filtered by date range) |
| GET | `/resource/{resourceId}/availability` | Resource.View | Single resource availability calculation |

**Filter params:** periodType (week/month/quarter), startDate, endDate, departmentId, locationId, resourceType, resourceId, includeProposed

Weekly capacity-matrix periods follow the application's Sunday-start reporting week. If `startDate` falls mid-week, the first cell begins on the Sunday of that reporting week; `endDate` remains exclusive and clips the final cell. The Project Overview weekly selector sends an aligned Sunday-to-Sunday range.

### Project Overview team capacity (`/api/projects/{projectId}/overview/team-capacity`)

The Project Overview Team, Resources & Allocation panel reads this project-scoped summary. It accepts `periodType` (`Week`, `Month`, or `Quarter`), `startDate`, and exclusive `endDate`; the backend uses active project-team memberships that overlap that range and counts each active resource once. The resource's explicit `HoursPerWeek` is the weekly capacity when configured; otherwise explicit `HoursPerDay` is multiplied by the effective capacity calendar's working-day count; when neither is configured, the resolved schedule calendar's productive hours are the compatibility fallback. The membership's `AllocationPercentage` multiplies that resource capacity to derive this project's ceiling. For example, a 48-hour resource week at 50% gives 24 project hours, and a 12-hour day over five resource working days gives 60 hours/week or 30 project hours at 50%. The Standard calendar determines working dates, holidays, and intervals; its daily interval does not cap explicitly configured resource hours. When both resource values are set, weekly hours must equal daily hours multiplied by the capacity calendar's working-day count. Calendar day rules, date-range/recurring holidays, and resource-specific exceptions are applied when configured. Membership and active-contract dates clip each period. Legacy calendars with only aggregate `WorkingDaysPerWeek` and `HoursPerDay` retain schedule fallback compatibility; they cannot identify actual weekdays, so new or corrected Standard calendars should have seven explicit day rules.

The calculation includes non-cancelled task-work allocations for the selected project only. Confirmed, Proposed, and Tentative assigned work counts as utilized; available capacity is clamped at zero per resource-period, and overallocated hours are returned separately. Project dedication defines the capacity ceiling and is never counted again as task utilization. Tasks consume capacity through their total `PlanAssignment.PlannedHours` (exposed to new clients as `AssignedWorkHours`), apportioned over their scheduled working-calendar period. Active assignments across the project's working plans are checked together, including overlapping tasks; the assignment API rejects work that exceeds the remaining project capacity. It also rejects assignment changes that would make total task assignments exceed the activity's authoritative planned work (`PlanActivity.EstimateHours`). Task Details → Resources uses that value and existing persisted assignment hours to calculate the remaining task-work budget. New member entitlements are based on Project Team allocation percentages; if entitlements exceed the remaining task work, they are proportionally scaled. Capacity caps are applied per resource without silently increasing another member's share, and unassigned task work remains visible. The batch endpoint validates every requested resource and the combined planned-work limit before writing, then atomically creates PlanAssignments, linked ResourceAllocations, and audit entries. Existing assignments are never recalculated by this action. Cross-project dedication continues to be governed by the overlapping Project Team allocation guard: concurrent project allocation percentages cannot exceed the resource's overall availability, while each project's task work consumes only its own allocated share.

The assignment-capacity endpoint clips capacity and existing-work calculations to the overlap between the requested range and the active Project Team membership (whose start and end dates are inclusive). It returns those membership bounds and whether the whole requested assignment schedule is inside them. Remaining hours describe the eligible overlap; a new or updated assignment is rejected unless its full schedule is within membership, and React shows the applicable membership dates and prevents saving until the dates are corrected. A separate assignment-capacity endpoint returns the project's allocation, period capacity, existing assigned work, and remaining assignable hours for the selected resource and dates. Its `GET /api/projects/{projectPublicId}/plans/{planId}/assignments/capacity` route is validated as a plan-owned collection action: the API verifies the project's tenant-scoped PublicId, management scope, and plan ownership before calculating capacity and checking active project membership. It does not expect an `assignmentId` because the request is a capacity lookup, not an assignment detail. No schema change is required because the current membership percentage and assignment planned-hours fields are reused.

When an explicit project ID is supplied for Overview, resource-allocation rows are filtered to that project; the broader Resource Capacity Matrix can still show resource commitments across projects. No allocation is a valid zero-utilization state. Unknown allocation statuses make utilization and idle-resource counts unavailable instead of being silently treated as no work. If a valid calendar/dedication configuration is unavailable, capacity totals are unavailable. An empty active team returns zero-valued metrics. Tenant isolation comes from the request's tenant-scoped `DbContext`; project visibility and management scope are checked before team membership is read.

## Working Calendar Inheritance

Calendar resolution uses the canonical tenant-scoped `WorkingCalendar` records:

1. A configured resource calendar applies first.
2. The project calendar applies when no valid resource calendar is configured, subject to the project member's explicit calendar policy.
3. The tenant Standard calendar applies when neither a valid resource nor project calendar applies.

An empty resource `BaseCalendarId` means inheritance; it does not require a separate resource calendar record. New projects reference the tenant Standard calendar when one is available. Existing projects and resources without calendar IDs resolve the same hierarchy at use time. Resource creation and the Create/Edit form require `HoursPerDay` from 1 through 24 hours, inclusive; fractional hours are supported, and empty, nonnumeric, below-1, and above-24 values are rejected by the form, create DTO validator, update DTO validator for explicit values, and capacity configuration rule. The update API rejects an explicit clear; an unrelated partial update that omits the field preserves its existing value. The capacity settings remain separate from schedule intervals: explicit weekly capacity wins; otherwise daily capacity is multiplied by the effective resource calendar's working days; otherwise schedule productive hours are used for legacy compatibility. The calculator preserves this fallback for legacy stored Resources whose `HoursPerDay` is null; legacy null values remain readable for calculations and may be preserved by unrelated partial updates, but cannot be entered or restored as empty through the form. Explicit resource hours may exceed the Standard calendar's daily interval, while date rules and holidays still decide which dates have capacity. If both resource values are supplied, the API rejects a mismatch instead of changing either value. Task assignment reports an actionable configuration error only when no valid effective schedule can be resolved.

`WorkingCalendarDayRule` stores all seven weekdays, working/non-working status, daily start/end, and an optional break interval. Holiday records support a date range and annual recurrence. `ResourceCalendar` remains the per-resource exception layer. No universal 40-hour or 48-hour week is synthesized. Project Team membership stores the allocation percentage, not an independent resource capacity; legacy member `HoursPerWeek` values do not override the resource's configured capacity. Task assignments store selected work hours and consume the member's percentage of resource capacity. The date-specific calendar remains required for assigning work across dates.

### Resource Create/Edit default calendar and validation

Resource Create and Edit preserve a selected `BaseCalendarId` after validating that the calendar is active, tenant-scoped, and has a usable schedule. When the field is empty, the service resolves the configured tenant `IsDefault` WorkingCalendar for capacity checks when exactly one valid default is available, while leaving `BaseCalendarId` empty so project-calendar inheritance remains available. `WorkingCalendar` has tenant scope; there is no Organization-level calendar override. The broader capacity inheritance at use time remains resource calendar, project calendar where applicable, then tenant Standard.

If no valid tenant default exists, a Resource with daily hours only may still be saved without a calendar; calendar-dependent capacity stays unavailable. Supplying both daily and weekly hours requires an effective calendar so their relationship can be checked. Missing or invalid defaults, unavailable explicit calendars, invalid calendar schedules, daily/weekly mismatch, and numeric hours violations return distinct coded validation errors keyed to `BaseCalendarId`, `HoursPerWeek`, or the relevant hours field. A calendar configuration failure is never reported as an invalid `HoursPerDay`.

### Optional Standard Calendar Initialization

The API has an idempotent bootstrap that runs after migrations in Development and at startup in other environments. It is disabled unless `Organization:StandardWorkingCalendar:Enabled` is explicitly `true`. Configuration must supply `TenantId`, `Name`, and seven `Days` entries (`DayOfWeek`, `IsWorkingDay`, optional `StartTime`, `EndTime`, `BreakStartTime`, and `BreakEndTime`); optional `Holidays` accept `Name`, `Date`, `EndDate`, and `IsRecurring`. The initializer creates a Standard calendar only when that tenant has no active default and no active calendar with the configured name. It preserves existing calendars and uses a serializable transaction to avoid duplicate initialization. The repository's Development configuration explicitly defines a seeded-tenant-only schedule: Sunday through Thursday, 08:00–17:00 with a 12:00–13:00 break (40 productive hours/week), and Friday/Saturday non-working. This is a Development fixture, not a universal or Production default. If configuration is absent or incomplete in another environment, no calendar is created and capacity remains unavailable until an administrator supplies the approved schedule.

### Resource Finder (`/api/resource-finder`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| POST | `/find` | Resource.View | Search resources by role, skills, seniority, availability with match scoring |

**Query fields:** ProjectId, Role, SkillIds, Seniority, StartDate, EndDate, RequiredAllocation, ResourceType, DepartmentId, LocationId, VendorId

## Frontend Pages

### Capacity Planning (`/resources/capacity`)
- **Matrix view** (default): Department capacity overview, summary cards, Demand vs Capacity tab, Resource Availability tab
- **Gantt view**: Timeline-based resource allocation bars with today line
- **View toggle** saved to localStorage (`nexus_capacity_view`)
- **Shared filters**: Period type (Week/Month/Quarter), start/end date, department
- **+Allocation button** opens create dialog

### Resource Utilization (`/resources/utilization`)
- Summary cards: Avg utilization, over-allocated count, under-utilized count
- Sorted resource table with utilization progress bars and status dots

### Resource Directory (`/resources`)
- Main resource list with search, tabs (All/Internal/Outsourced)
- **Resource Finder** dialog accessible from page header

## Components

### `AllocationDialog`
Create and edit allocation form with fields: Resource, Project, WorkItem, Role, Start/End date, Allocation %, Hours/Week, Status, Billable toggle, Notes. Shows conflict warnings from API before saving.

### `ResourceGanttView`
Custom Gantt chart component:
- Resource rows (left label panel) + timeline (scrollable right panel)
- Allocation bars color-coded by status (Confirmed=blue, Proposed=amber, Tentative=purple)
- Allocation % shown inside bars
- Today vertical line
- Conflict indicator icon on rows with overlapping allocations
- Month/quarter labels based on zoom level
- Popover on bar click: project name, status badge, role, %, dates, billable, notes
- Popover actions: Open Resource link, Open Project link, Edit Allocation button

### `ResourceFinderDialog`
Resource search dialog with:
- Criteria inputs: Role, Seniority, Allocation %, Date range
- **List/Gantt toggle**: List view shows ranked cards with skill matches, allocation %, match score; Gantt view shows mini horizontal allocation bars per resource
- Date range shown in header when both dates provided

## Frontend Hooks

| File | Key Hooks |
|------|-----------|
| `hooks/resources/useAllocations.ts` | `useAllocations`, `useAllocation`, `useAllocationsByResource`, `useAllocationsByProject`, `useAllocationConflicts`, `useCreateAllocation`, `useUpdateAllocation`, `useDeleteAllocation` |
| `hooks/resources/useCapacity.ts` | `useCapacityMatrix`, `useCapacityAvailability`, `useDemandVsCapacity`, `useCapacityGantt`, `useResourceAvailability` |
| `hooks/resources/useResourceFinder.ts` | `useResourceFinder` |

## Authorization Policies

| Endpoint Group | Auth Policy | Required Claims |
|----------------|-------------|-----------------|
| Allocations GET | `Resource.View` | `Resource.View` |
| Allocations POST/PUT/DELETE | `Resource.Assign` | `Resource.Assign` |
| Capacity GET | `Resource.View` | `Resource.View` |
| Resource Finder POST | `Resource.View` | `Resource.View` |

The API refreshes the effective `"permission"` claims from current tenant-scoped grants and Deny overrides on every authenticated request. The JWT permission snapshot is not trusted for final authorization.

The active React/Vite Dashboard Builder mirrors the Capacity API read policy: Resource Utilization, Resource Capacity, and Overallocated Resources widgets load only when the effective user permissions include `Resource.View`. Assignment and management grants are not read fallbacks. Saved widgets without the view permission show an unavailable state and do not issue the protected resource query. Capacity values are never replaced with a fabricated zero when the matrix response is absent.

## Calendar / Leave Integration

The `ResourceCalendar` entity stores per-resource, per-date entries:

| Field | Type | Description |
|-------|------|-------------|
| `ResourceId` | Guid | FK to Resource |
| `Date` | DateTime | Calendar date |
| `DayType` | string | WorkingDay, NonWorkingDay, Holiday, SickLeave, PersonalLeave |
| `AvailableHours` | decimal? | Adjusted available hours for the day |
| `Notes` | string? | Reason or description |

**Integration points:**
- `Contract.EndDate` provides a natural boundary for resource availability (no allocations or capacity beyond contract end)
- ResourceCalendar entries override default working hours and reduce net capacity in matrix calculations
- Capacity matrix (`/api/capacity/matrix`) calculates `NetCapacityHours = GrossCapacityHours - leave/absence hours` from calendar entries
- Resource availability endpoints factor in calendar data when computing available hours

**Current implementation status:** ResourceCalendar entity exists with full schema. Capacity service methods read calendar data for net capacity calculations.

## i18n Support

### Current State
Resource capacity and allocation strings are **not externalised** to the React app English and Arabic message catalogs. Hardcoded English text is used throughout:

- AllocationDialog labels and validation messages
- ResourceGanttView status labels, tooltips
- ResourceFinderDialog criteria labels and result cards
- Capacity page filter labels, summary card titles, heatmap states
- Utilization page column headers and summary cards

### Pages Affected
All pages under `/resources/capacity`, `/resources/utilization`, and the allocation dialog components need i18n integration.

### Translation Keys Required
When implementing i18n, the following section should be added to `src/messages/en.json` and `src/messages/ar.json`:

```json
{
  "resources": {
    "capacity": {
      "title": "Capacity Planning",
      "matrixView": "Matrix",
      "ganttView": "Gantt",
      "demandVsCapacity": "Demand vs Capacity",
      "resourceAvailability": "Resource Availability",
      "summary": "Summary",
      "periodType": "Period Type",
      "week": "Week",
      "month": "Month",
      "quarter": "Quarter",
      ... (to be expanded)
    },
    "allocation": {
      "create": "Create Allocation",
      "edit": "Edit Allocation",
      "resource": "Resource",
      "project": "Project",
      "role": "Role",
      "startDate": "Start Date",
      "endDate": "End Date",
      "allocationPercentage": "Allocation %",
      "hoursPerWeek": "Hours/Week",
      "status": "Status",
      "isBillable": "Billable",
      "notes": "Notes",
      "conflictWarning": "Conflict Warning"
    }
  }
}
```

## Test Status
The .NET Resource Management test suite includes unit coverage for capacity calculations in `apps/api/tests/NEXUS.Modules.ResourceManagement.Tests/CapacityServiceTests.cs`. Project Overview capacity cases cover configured team hours without a calendar, dedication percentages, idle team members, cross-project commitments, calendar holidays and exceptions, unknown allocation statuses, and unavailable configuration.

## Key Behaviours
- **Gantt view**: Resources grouped from allocation data; overlapping allocations per resource flagged with conflict icon; bars positioned by percentage within timeline; today line rendered when today falls within visible range
- **Allocation create/edit**: Conflict warnings fetched from `/api/allocations/conflicts` before submit; warnings shown inline with severity badges
- **View persistence**: Matrix/Gantt selection saved to localStorage; survives page refresh
- **Resource Finder Gantt**: Shows free periods as the gap between allocation bars within the requested date range
- **Period zoom**: Week/Month/Quarter affects Gantt header labels and matrix period grouping

## Remaining Limitations
- Leave/absence API endpoints not exposed (ResourceCalendar entity exists but no public API for managing calendar entries)
- Unavailable periods not rendered in Gantt (requires ResourceCalendar API integration)
- No dedicated API endpoints for calendar entry CRUD (create/update/delete calendar entries)
- Contract boundary enforcement not verified (Contract entity exists but no validation in allocation service)
- No drag-to-resize or drag-to-reposition on Gantt bars (read-only timeline)
- No real-time updates (polling not implemented)
- Conflict detection is percentage-based only (no hours-level conflict check exposed)
- Resource Finder skill matching requires skill IDs (no skill search by name)
- i18n strings not externalised (hardcoded English throughout)
- No unit or integration tests (test framework not set up)
- Frontend components use embedded mock data, not wired to live API

### Project membership allocation guard

Cross-project Project Team allocations are compared only while their effective membership dates overlap. Membership end dates are inclusive. Separate allocations in different periods are not added together, and any concurrent total above 100% is rejected. Task work remains a separate project-scoped calculation based on scheduled `PlanAssignment.PlannedHours`.

### Team-based Project Team assignments — 2026-10-31

A project assignment can target one Resource or one active same-tenant Team. Team membership is resolved using `TeamMember.EffectiveFrom`/`EffectiveTo` and the project assignment dates. Capacity, project overview, status-report utilization, and allocation-conflict checks expand active Team memberships and deduplicate Resources that also have direct Project Team rows. The highest overlapping project dedication is used for one Resource within the same Project, avoiding duplicate capacity consumption from overlapping direct and Team paths. Cross-project simultaneous allocations still combine and cannot exceed 100%.

The group allocation rule is per Resource: a Team-target assignment's percentage is applied independently to every Resource while that Resource is an effective member. It is not divided among the group. This behavior is stated in the Project Team UI and implemented in project overview/status-report expansion; when the same Resource is reached through direct and Team paths, calculations retain the highest overlapping dedication once. Work Item group assignments remain routing/visibility targets; Time Entries and actual work stay attributed to the individual Resource. No automatic allocation or actual-work split is performed across Team members.

Work-item assignee options and time-entry validation use only active, effective Resources and Teams. Historical ProjectMember and TeamMember rows, time entries, plan assignments, and ResourceAllocation records remain preserved. The behavior requires consolidated API and persisted-schema UAT; no Development migration or runtime verification was performed as part of source implementation.

## Cost Utilization Calendar View - 2026-11-01

The Cost Utilization page keeps Daily Detail and adds a month calendar over the same `GET /api/resource-analytics/cost-utilization` report and selected filters. Month navigation sets the shared `from`/`to` filters to the selected calendar month, so Daily Detail and Calendar View use the same response and totals. Each day sums the existing daily resource metrics; monetary values remain grouped by their authoritative currency, and the utilization bars use the report's planned and actual hours/capacity.

Day Details uses the daily resource rows and their tenant-scoped planned activity and work-item references. The report response includes activity/work-item labels and planned/actual hours for the detail sheet. It does not change cost or utilization formulas, create a second calculation path, or require a database schema change.

Daily Detail groups those same report rows by work date. Each date keeps day-level capacity, planned/actual hours, utilization, excess, and overtime visible; expanding a date shows its per-resource breakdown inline with resource type, department, vendor, per-day metrics, and actual labor cost when `canViewCosts` permits it. Day-level actual costs remain grouped by currency. This replaces the separate on-page Resource Breakdown section while retaining its fields in the expanded daily rows. PDF/Excel report types and their Resource Breakdown worksheets remain unchanged. The view uses the existing `Resource.View` report response and filters; no permission, API, or database change was introduced.

| View / data | Permission | Enforcement and scope |
|---|---|---|
| Daily Detail, Calendar View, resource and activity details | `Resource.View` | React navigation and route guard plus the existing API `Resource.View` policy; tenant and management Project scope are enforced by the API. |
| Monetary cost fields | `Resource.ViewCostRate` | The API sets `canViewCosts` and redacts monetary fields when the claim is absent; both views honor that response. Cost-rate edits require `Resource.EditCostRate`, which depends on `Resource.ViewCostRate`. |
| PDF and Excel exports | `Resource.View` | Export reuses the same report API and independently selected report filters; the API applies existing tenant and management Project scope before returning export data. No separate export endpoint or permission was introduced. |
| Export Team filter | `Team.View` | Team options load only for users with `Team.View`; the report service checks the same claim, requires an active same-tenant Team, and filters to active, effective same-tenant Team memberships. |

No permission key or role grant was added. Permission-positive, permission-negative, cost-redaction, and cross-scope UAT scenarios are recorded as CUV-01-CUV-04; export-specific currency, Team filter, empty-result, and file-format scenarios are recorded as CUE-01-CUE-04 in the Master UAT Checklist.

PDF and `.xlsx` downloads are generated in the browser from the report API response already authorized for that user. The PDF supports page breaks and repeated table headers; workbooks contain a filter/period header and separate summary, daily, calendar-hours, calendar-costs, resource, and activity worksheets as applicable. Monetary fields remain hidden when `canViewCosts` is false; costs with different currencies are kept in separate rows and worksheets. Empty reports still produce files with an explicit no-data row/message.
