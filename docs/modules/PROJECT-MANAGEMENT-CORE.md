# Project Management Core

## Overview
The Project Management Core module provides comprehensive project lifecycle management including project CRUD operations, project team management, resource allocation with conflict detection, and integration with Resource Management capacity planning.

## Domain Entities

| Entity | Description | Key Fields |
|--------|-------------|------------|
| `Project` | Core project entity with all standard PM fields | Name, Code, Description, Status, Priority, StartDate, EndDate, SponsorId, ProjectManagerId, PortfolioId, ProgramId, DepartmentId, Budget, Currency, Progress, Health, RagStatus, Phase, RiskRating, BusinessCase, Notes |
| `ProjectMember` | Project team member with resource allocation | ProjectId, ResourceId, Role, AllocationPercentage, HoursPerWeek, StartDate, EndDate, Status, IsBillable, Notes |

## Project Plan Working Draft

Foundation for the project plan versioning flow. Once an approved baseline exists, further (not-yet-approved) plan edits are persisted here as a separate, isolated version — never by mutating the approved baseline.

| Entity | Table | Key Fields |
|--------|-------|------------|
| `ProjectPlanDraft` | `ProjectPlanDrafts` | ProjectId, SourceBaselineVersionId, Status (Draft/Submitted/Rejected/Applied), IsActive, SubmittedChangeRequestId, CreatedBy/CreatedAt/UpdatedBy/UpdatedAt, RowVersion, TenantId |

- **Tenant / project isolation**: one active draft per tenant+project is enforced by a filtered unique index `(TenantId, ProjectId) WHERE IsActive = 1` (`IX_ProjectPlanDrafts_Tenant_Project_Active`).
- **Concurrency**: `RowVersion` (rowversion) is configured as the concurrency token; the entity inherits `BaseEntity` (tenant filter, soft delete, audit).
- **Scope**: This is the persistence foundation only — no plan editor integration, CR submission, approval UI, notifications, or baseline promotion yet. Those workflows are deferred; when user-facing status is surfaced later, it must use the centralized localized enum/code-list pattern.
- **Migration**: `AddProjectPlanDraftTable`.

## Initial Baseline

Business action *Start Project / Save Initial Baseline*: creates the first approved baseline (v1) for a project and captures the current project-plan snapshot for future comparison.

| Entity | Table | Key Fields |
|--------|-------|------------|
| `ProjectBaselineVersion` | `ProjectBaselineVersions` | ProjectId, VersionNumber, Name, Status (Draft/PendingApproval/Approved/Rejected/Superseded), Description, CreatedBy/CreatedAt, ApprovedBy/ApprovedAt, SourceChangeRequestId, IsCurrent, RowVersion, TenantId |

- **Guard**: creation succeeds only when the project has no approved, current baseline; a second attempt returns `Conflict` (409). The DB enforces uniqueness via `(TenantId, ProjectId, VersionNumber)` (`IX_Tenant_Project_Version`) and a single current-approved row via a filtered index `(TenantId, ProjectId, IsCurrent, Status)` (`IX_Tenant_Project_CurrentApproved`).
- **Snapshot**: the workflow calls `IProjectSnapshotService.CreateBaselineSnapshotAsync` to freeze the project-plan payload (`ProjectSnapshot` with `SnapshotType = "InitialBaseline"`), reusing the same payload builder as manager handover.
- **Audit / concurrency**: tenant, created/approved by and timestamps are recorded; `RowVersion` is the concurrency token; the change is audited as `InitialBaselineCreated` on `ProjectBaselineVersion`.
- **Scope**: no change request is created and no Working Draft/plan-editor integration, frontend, or notifications are included.
- **Endpoints**: `POST /api/projects/{projectId}/baselines/initial` (`ProjectBaselineController`).
- **Tests**: `ProjectInitialBaselineTests` (create v1 approved/current + snapshot, duplicate rejection, tenant isolation, missing claims).


## API Endpoints
...fine...

### Projects (`/api/projects`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Project.View | Paged list with filters (status, priority, portfolio) |
| GET | `/{id}` | Project.View | Single project with full details and team |
| POST | `/` | Project.Create | Create new project |
| PUT | `/{id}` | Project.Edit | Update project |
| DELETE | `/{id}` | Project.Delete | Soft-delete project |
| POST | `/{id}/archive` | Project.Edit | Archive project (sets status to OnHold) |
| POST | `/{id}/reactivate` | Project.Edit | Reactivate archived project |

### Project Team (`/api/projects/{id}/team`)
| Method | Path | Auth Policy | Description |
|--------|------|-------------|-------------|
| GET | `/` | Project.View | List project team members |
| POST | `/` | Project.AssignResources | Add resource to project team with conflict checking |
| PUT | `/{memberId}` | Project.AssignResources | Update team member allocation |
| DELETE | `/{memberId}` | Project.AssignResources | Remove team member |
| GET | `/conflicts` | Project.View | Check resource conflicts before assignment |

## Authorization Policies
| Policy | Required Claims |
|--------|-----------------|
| `Project.View` | `Project.View` |
| `Project.Create` | `Project.Create` |
| `Project.Edit` | `Project.Edit` |
| `Project.Delete` | `Project.Delete` |
| `Project.AssignResources` | `Resource.Assign` |

## Frontend Pages

| Page | Route | Description |
|------|-------|-------------|
| Project Registry | `/projects` | Main project list with search, filters, table/card views |
| Project Detail | `/projects/[id]` | Project overview, financials, team, risks, issues, milestones |

## Frontend Hooks
| File | Key Hooks |
|------|-----------|
| `hooks/projects/useProjects.ts` | `useProjects`, `useProject`, `useProjectTeam`, `useCreateProject`, `useUpdateProject`, `useDeleteProject`, `useArchiveProject`, `useReactivateProject`, `useAddTeamMember`, `useUpdateTeamMember`, `useRemoveTeamMember`, `useCheckResourceConflicts` |

## Resource Integration
- **Conflict Detection**: Before assigning a resource to a project, the system checks for overlapping allocations and validates that total allocation doesn't exceed 100%
- **Capacity Matrix**: Project allocations appear in Resource Capacity Matrix (`/resources/capacity`)
- **Resource Gantt**: Project allocations appear in Resource Gantt view (`/resources/capacity` Gantt tab)
- **Resource Finder**: Resources can be found and assigned via Resource Finder dialog
- **Real-time Updates**: Cache invalidation after mutations refreshes all related views

## Validation Rules
- Project Code: Required, max 50 chars, unique
- Project Name: Required, max 200 chars
- Status: Must be one of (Initiation, Planning, InProgress, OnHold, Completed, Cancelled, Closed)
- Priority: Must be one of (Low, Medium, High, Critical)
- Currency: 3-letter ISO code
- Dates: Start date must be before end date
- Budget: Non-negative
- Team Member Allocation: 1-100%, hours/week 1-168, end date after start date
- Resource Conflicts: Total allocation across all projects must not exceed 100%

## i18n Support
Full English and Arabic translation support with RTL layout.

## Test Status
- Backend: Compiles successfully, EF Core migrations ready
- Frontend: TypeScript typecheck and build pass, ESLint passes (warnings only for unused vars in existing code)

## Migration Result
No new database migration required - existing Project and ProjectMember tables updated with new fields via EF Core conventions.

## Remaining Limitations
- No full project plan/WBS/task scheduling (deferred per requirements)
- Plan working-draft persistence foundation exists; plan editor integration, CR submission/approval UI, notifications, and baseline promotion (Draft → Applied) are not yet implemented
- Initial (first) approved baseline workflow implemented (`POST /api/projects/{projectId}/baselines/initial`): creates `ProjectBaselineVersion` v1 approved/current and captures an `InitialBaseline` project snapshot, with tenant/audit/concurrency and duplicate rejection; no CR, Working Draft, frontend, or notifications
- Project templates page still uses mock data
- Project board/Gantt/matrix views not yet connected to real APIs
- Bulk operations not implemented