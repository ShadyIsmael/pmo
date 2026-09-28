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
| DELETE | `/{id}` | Resource.Manage | Soft-delete allocation |

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
| Allocations GET | `Resource.View` | `Resource.View` OR `Resource.Allocate` OR `Resource.Manage` |
| Allocations POST/PUT | `Resource.Assign` | `Resource.Allocate` OR `Resource.Manage` |
| Allocations DELETE | `Resource.Manage` | `Resource.Manage` |
| Capacity GET | `Resource.View` | `Resource.View` OR `Resource.Allocate` OR `Resource.Manage` |
| Resource Finder POST | `Resource.View` | `Resource.View` OR `Resource.Allocate` OR `Resource.Manage` |

Permission claims are embedded in JWT tokens by `DemoAuthService.GenerateJwtToken` using the `"permission"` claim type. Claims are derived from role-permission mappings stored in the database.

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
Resource capacity and allocation strings are **not externalised** to next-intl translation files. Hardcoded English text is used throughout:

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
No test framework is configured in the project. No tests exist for capacity, allocation, or resource finder modules.

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
