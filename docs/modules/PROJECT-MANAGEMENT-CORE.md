# Project Management Core

## Overview
The Project Management Core module provides comprehensive project lifecycle management including project CRUD operations, project team management, resource allocation with conflict detection, and integration with Resource Management capacity planning.

## Earned Value in Project Overview (SPI / CPI)

Project Overview calculates EVM from persisted planning, resource-cost, time-entry, and finance records. It does not use the legacy editable `Project.PlannedValue`, `EarnedValue`, or `ActualCost` fields as fallbacks.

- **Status date:** current UTC date. The UI labels it and explains that the view is a current snapshot; historical EVM is not claimed because activity progress is not versioned by date.
- **Baseline:** newest project plan (same plan order as the planning workspace), with its latest approved `PlanVersion` of type `Baseline`. PV/BAC use its dated `PlanAssignment` planned hours, resource calendar capacity, and effective `ResourceCostProfile` rates. When total `PlannedHours` was not captured but positive `HoursPerWeek` and a valid assignment/activity date range exist, total planned hours are derived from that immutable baseline data; an explicit total always takes precedence. Baseline capture snapshots the Working assignments; assignments added after capture require a later baseline and never mutate an approved one.
- **Current progress / EV:** latest active `Working` plan version, matching baseline activities by stable source lineage and only using a unique activity code for legacy activities. EV is baseline assignment cost multiplied by current task progress.
- **Actual labor cost:** persisted project `TimeEntry` hours priced with the applicable resource cost profile and the canonical labor cost calculator through the status date.
- **Direct actual costs:** only `CostEntry` rows of type `Actual`, status `Posted` or `Verified`, dated through the status date, without a resource link and not labeled as labor are added to displayed AC. Tax is excluded. Currency conversion is not inferred.
- **Comparable cost metrics:** if direct non-labor actual costs exist, total AC remains visible, but Cost Variance and CPI are unavailable because those direct costs have no activity/baseline mapping. This avoids comparing total AC against labor-only EV. SPI remains available when its inputs are complete.
- **Incomplete data:** missing baseline assignments, missing task progress, invalid dates/hours, missing or overlapping cost profiles, and currency mismatches return null metrics with localized reason codes; planned hours remain unavailable when neither an explicit total nor a usable weekly commitment and date range exist. No mock or legacy values fill the gaps.
- **Team signal:** the overview count and hours/week come from current, date-effective `ResourceAllocation` records. Plan assignment synchronization stores the owning Project ID (not a PlanVersion ID) on that allocation; project membership alone is not a current allocation.
- **Issue attention:** overdue issue counts and preview dates use `TargetResolutionDate` first, then `DueDate`, matching the Work Items filters and insights.
- **Tooltips:** concise Arabic and English SPI/CPI formulas and threshold meanings appear beside those metrics in Project Performance. KPI Summary does not duplicate SPI/CPI or the next milestone, which is already represented in the milestone timeline.
- **Verification:** `ProjectEvmCalculatorTests` verifies formulas and unavailable reasons; `ProjectEvmPersistenceTests` exercises persisted baseline, normal plan/time-entry/finance service updates, independence of progress and labor cost changes, and direct-cost comparability. Build/test execution status must be taken from the task verification report.

## Multi-Plan Project Reporting (Implementation Status)

Project reporting configuration is effective-dated and versioned. It selects the execution plans, active Working version, approved Baseline, reporting currency, weekly frequency, time zone, and included report sections. Alternative/What-if plans are not eligible for official consolidated results. Activity overlap across selected plans is rejected rather than silently double-counted.

`GET /api/projects/{projectId}/performance` is the server-side source for consolidated, individual-plan, and two-plan comparison calculations. Consolidated SPI/CPI use aggregated values rather than averaging plan ratios. Historical EV is returned unavailable when persisted progress history cannot substantiate the requested past status date. Missing values carry reason codes and are not replaced with zero or project-level editable fallback fields. Critical path remains explicitly `ByPlan` unless a unified cross-plan schedule network becomes available.

### React Project Overview presentation

The React/Vite `Project > Overview` keeps its existing project widgets and defaults the performance card to the consolidated project view. Users with `StatusReport.View` see explicit Consolidated, Individual, and Compare controls. The plan selectors are presentation filters over the currently approved reporting configuration; changing them never edits that configuration. Only plans included in the official reporting scope are selectable, and Compare is disabled unless at least two such plans exist.

The KPI progress gauge identifies whether its value comes from the active Working plan or the project record fallback. Its delta is labelled as a percentage-point comparison against the latest published report's frozen progress value, not as a time-series change. If the published report period ends after the API's current status date, Overview shows a localized data-quality warning; it does not rewrite or unpublish the immutable report. Reporting permission gates remain unchanged.

The Overview source now exposes every available comparable series: approved budget ceiling, EVM actual cost (labor time entries plus posted/verified direct actual costs through the current status date), active commitments in the project currency, signed schedule/cost variance, high/critical risks vs overdue issues, active resources vs confirmed allocations, project-member roles, complete issue-status totals and non-closed risk-status/level totals, pending change requests, published vs non-published reports, and meetings with vs without final minutes. Donut charts are primary for true part-to-whole status and composition distributions (issue statuses, non-closed risk statuses, member roles, reports, and meeting minutes). The financial summary uses one multicolor donut to compare actual spend with active commitments, with the approved budget shown in the center and exact currency values beside the chart; the slices are not percentages of the approved budget. The Project Overview Team and Allocation card shows project-scoped capacity, task-work utilization, availability, active team resources, idle resources, and overallocated hours for the selected reporting period. Its chart separates utilized and available hours. Capacity is derived from the effective resource/project working calendar and the Project Team allocation percentage; utilization comes from planned work hours on active project assignments. Project dedication is never counted as task utilization. The card follows the same capacity model used when assigning task work. Milestone badges derive from active Working-plan progress/completion dates and compare due dates with the API status date. The milestone progress chart plots only valid recorded percentages in the API's milestone preview; it does not infer values for missing percentages or present the preview as a full milestone aggregate. Empty denominators, inconsistent counts, and failed change-request queries display as unavailable instead of as zero. Automated verification on 2026-10-25: React typecheck/build and focused donut tests pass; the Risk Management module builds, and the focused API integration test passes (1/1). The rebuilt local API was started with database initialization skipped and returned HTTP 200 from `/health`; it was then stopped at the user's request. The React dev server at localhost:4100 returned HTTP 200 during verification and was not started or stopped in this task. The authenticated project page still needs a refresh for live visual confirmation. No database migration or business data was changed.

The Project Registry's table, cards, progress filters, and exports use the API's `PlanProgress` value from the same active Working-plan lookup. They do not substitute the separately stored `Project.Progress`; when no active Working plan has a recorded activity percentage, the registry shows progress as unavailable (`—`). Forecast/actual-cost figures are omitted from the registry table, cards, filters, KPI hint, and exports; financial values remain available in the project's financial views. The projects API continues returning the stored project field separately for other consumers, and neither value is rewritten by the registry read.

### Project Overview capacity summary

`GET /api/projects/{projectId}/overview/team-capacity` requires both `Project.View` and `Resource.View`. It applies project tenant/management-scope checks before reading active team memberships, deduplicates active resources, and returns capacity and task-work utilization for this project only. Weekly resource capacity comes from the effective resource/project working calendar; the membership's `AllocationPercentage` determines the project's share. Active plan assignments contribute their explicitly assigned work hours as utilization and never redefine project allocation. The Project Overview selector applies one Week, Month, or Quarter range to total capacity, utilization, available hours, idle count, and overallocation. The React card displays one utilized/available donut followed by exactly four KPI cards. Missing calendar/allocation inputs remain unavailable; an unknown allocation status cannot produce an idle-resource count. See `RESOURCE-CAPACITY-AND-ALLOCATION.md` for the capacity rules and limits.

Individual view shows the selected plan's configured working-version number, approved reporting baseline, plan-specific EVM metrics, and that plan's milestones. Compare view uses the performance endpoint's two-plan result to render a plan-grouped PV/EV/AC chart and a side-by-side metric table; it does not present the comparison as a consolidated total. Consolidated view retains the project-level finance context and the plan breakdown. When EVM values are unavailable, the UI keeps plan context and localized reason codes visible instead of substituting zero or hiding the plan. Reporting permission and query failures have explicit messages; existing server-side authorization remains authoritative. The reporting-configuration query treats an empty API response as the documented no-effective-configuration state (`null`), while transport and authorization failures remain query errors. The controls, plan labels, and empty/error states have EN/AR resources and follow the React app's LTR/RTL and semantic financial-color conventions.

Status-report drafts retain a separate Status Date. The implemented lifecycle uses the canonical tenant-scoped Workflow Engine: Draft → Generated → Submitted → Approved → Published, with a Returned → Draft revision path. Generation freezes the EVM performance snapshot, reporting-configuration version, included-section selection, and related report snapshots with the workflow transition. The report author cannot approve their own submission, and project/tenant management scope is checked by the workflow adapter. Published snapshots are immutable. The detail view and PDF/Excel exports honor the frozen section selection: Executive Summary, Performance KPIs, Plan Performance, Milestones, Risks & Issues, and Decisions Required. The decisions section is based on pending project change requests; no missing priority or due-date data is fabricated. EN/AR labels are localized and Excel tabs follow the selected direction. The workflow implementation is not, by itself, evidence that a live canonical-engine approval cycle was exercised; see the verification limits below.

Published reports can be compared through `GET /api/projects/{projectId}/status-reports/compare?firstReportId={id}&secondReportId={id}`. The endpoint requires two distinct Published reports belonging to the same project and compares frozen progress/EVM values, included plans, configuration/baseline changes, and changed milestone status/date/variance. Risk and issue trend counts are limited to the up-to-five prioritized records captured in each report snapshot; they are not full project totals. The Decisions Required section is likewise based on up to five prioritized change requests captured at generation time.

### Integration verification (2026-10-20)

- **Development database:** the configured local Development target is SQL Server `NexusPmo`; migration `20261018190000_AddMultiPlanReportingConfiguration` is already present in migration history, and the reporting tables/columns/relations were verified. No migration was applied during this verification, so no database backup was required and no business rows were changed. EF reports no pending model changes. The local DB does not contain a designated project with three execution plans and a generated/published weekly report series.
- **Persisted multi-plan fixture:** `FocusedProjectReporting=true` uses a disposable SQLite database populated with canonical project/plan/version/baseline/activity/time-entry entities. It matches the requested selection: Core Implementation V3/BL-02, Data Migration V2/BL-01, Training & Go-Live V1/BL-01, plus an excluded Accelerated Scenario. The independent oracle and persisted service result matched: BAC 1,750,000; PV 1,000,000; EV 780,000; AC 900,000; SPI 0.78; CPI 0.866666…; SV -220,000; CV -120,000; progress 44.5714…%. The fixture also checked effective-dated configuration and a generated snapshot remaining unchanged after source changes.
- **Defect fixed — High, cost attribution:** selected-plan views could previously receive project-wide labor actuals, including work belonging to excluded plans. Actual labor is now filtered to the configured activity scope; only Consolidated includes unassigned project labor and reports it as unallocated. Individual/Compare do not silently absorb project-level unassigned cost. The regression test verifies plan totals remain attributed, the 50,000 unallocated amount is surfaced, and CPI becomes unavailable when cost attribution is incomplete.
- **Defects fixed — Medium, PDF:** the Arabic export reversed currency text and localized digits contrary to the Western-digit display convention; the EVM table and small report sections could also split across pages. PDF money/date formatting now keeps Western digits and an LTR currency token inside RTL content, and short sections keep their heading/data together. English and Arabic PDFs were generated as actual files, rendered, and visually inspected; the EVM table, plan breakdown, milestones, risks, issues, decisions, and metadata were present without orphaned table headers. Excel files were generated, reopened with ExcelJS, and checked for sheet names, snapshot values, Arabic RTL direction, and selected-section behavior.
- **Automated results:** Release API build succeeded (0 warnings/errors); `FocusedProjectReporting` 1/1, `FocusedEvm` 19/19, `FocusedProjectStatusReports` 6/6, `FocusedProjectSecureRouting` 22/22, and general management-context/read-only middleware tests 25/25 passed. Focused React reporting/export tests passed 12/12; React TypeScript, targeted ESLint, EN/AR parity (5,088 keys), used-key validation (3,312 keys), and Vite production build passed. The Vite build still emits the existing large-chunk warning for PDF/XLSX libraries.
- **Verification limits:** the persisted three-plan test uses isolated SQLite rather than the real Development SQL Server/API. The actual canonical workflow-engine transition/approval chain, reporting-endpoint Tenant/Management Scope/ReadOnlyViewAs matrix, cross-session isolation, and live Week 1/Week 2 reports/exports were not end-to-end exercised. Existing unit and routing tests are useful coverage but do not satisfy those checks. No browser UAT was performed, as requested.

**Readiness:** implementation and isolated integration/export checks pass, but release readiness remains **blocked for manual/API security verification** until a designated DEV project and authorized/restricted identities are available for the real API workflow and tenant/scope checks. Do not treat the focused test results as a substitute for those checks.

## Domain Entities

| Entity | Description | Key Fields |
|--------|-------------|------------|
| `Project` | Core project entity with all standard PM fields | Name, Code, Description, Status, Priority, StartDate, EndDate, SponsorId, ProjectManagerId, PortfolioId, ProgramId, DepartmentId, Budget, Currency, Progress, Health, RagStatus, Phase, RiskRating, BusinessCase, Notes |
| `ProjectMember` | Project team member with project dedication | ProjectId, ResourceId, Role, AllocationPercentage, StartDate, EndDate, Status, IsBillable, Notes; HoursPerWeek is legacy and ignored for current capacity calculations |

## Project Plan Working Draft

Foundation for the project plan versioning flow. Once an approved baseline exists, further (not-yet-approved) plan edits are persisted here as a separate, isolated version — never by mutating the approved baseline.

| Entity | Table | Key Fields |
|--------|-------|------------|
| `ProjectPlanDraft` | `ProjectPlanDrafts` | ProjectId, SourceBaselineVersionId, Status (Draft/Submitted/Rejected/Applied), IsActive, SubmittedChangeRequestId, CreatedBy/CreatedAt/UpdatedBy/UpdatedAt, RowVersion, TenantId |

- **Tenant / project isolation**: one active draft per tenant+project is enforced by a filtered unique index `(TenantId, ProjectId) WHERE IsActive = 1` (`IX_ProjectPlanDrafts_Tenant_Project_Active`).
- **Concurrency**: `RowVersion` (rowversion) is configured as the concurrency token; the entity inherits `BaseEntity` (tenant filter, soft delete, audit).
- **Scope**: Plan-editor integration and baseline promotion remain separate. Change Request Draft persistence, workflow submission, approval UI, and notifications are described in the approval-workflow section below.
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

## Project Change Request Approval Workflow

Change Requests are persisted as workflow-backed Drafts before submission. Draft updates use `RowVersion`; submission snapshots the selected, currently eligible project approvers into assignment rows and moves the request through the `ChangeRequest` workflow domain. The project Sponsor is mandatory by default and must be an active, different user with effective `ChangeRequest.Approve`; the project policy may enable Sequential and Any-One-Optional strategies. Any-One-Optional still requires mandatory approvals and at least one optional approval.

Approver candidates include active same-tenant project participants and external business approvers who currently have effective approval permission; the requester cannot approve their own request. Each decision is assignment-specific and workflow-validated. An external business approver gains access only to the exact assigned review and receives no general project or Change Request read access. Reassignment requires a reason and writes history. Legacy Pending rows without assignments are not inferred or approved automatically; an authorized manager explicitly reopens them for setup. Approval does not itself apply a proposed budget, baseline, or plan change.

- **Persistence**: `ProjectChangeRequestApprovals` stores ordered assignment snapshots and decisions; `ProjectChangeRequestApprovalHistories` stores reassignment history. Project policy options live on `ProjectWorkflowConfigurations`.
- **Migrations**: `20261021120000_AddProjectChangeRequestApprovals` was applied to local Development `NexusPmo`; `20261021130000_AddChangeRequestApprovalModelIndexes` adds the missing FK indexes and default constraints for approval policy flags. The follow-up was applied by the normal EF startup migration runner. Existing business rows were not deleted or rewritten.
- **Endpoints**: see the Project Change Requests API table in `docs/api/endpoints.md`.
- **Verification**: API build passed (0 warnings/errors); `dotnet ef migrations has-pending-model-changes` reports no model drift; API startup applied the follow-up migration and `/health` returned HTTP 200. The earlier workflow compilation fixes remain in place. Backend test assertions were not run in this pass.

## Project Sponsor and approval review access

`Project.SponsorId` remains the canonical Sponsor assignment. Project create/edit and Project Team governance actions synchronize it to an existing same-tenant active User/Resource membership when the Sponsor has a linked Resource. Synchronization is idempotent, preserves pre-existing membership roles and allocation, and deactivates only membership rows explicitly marked as Sponsor-managed. If the Sponsor has no linked Resource, the canonical User assignment is still shown as a governance relationship without synthesizing a Resource or TeamMember. Clearing the Sponsor removes only that relationship; it does not remove unrelated team membership or create a User, Resource, or tenant security role.

Supported My Approvals types (Project, Program, and Portfolio manager handovers plus Project Change Requests) link to their actual assignment-scoped review route. Each route returns a bounded read-only projection only after validating the authenticated tenant, current assignment, effective approval permission, request state, and owning object. Change Request managers may select active same-tenant users who currently have effective `ChangeRequest.Approve`, including business approvers outside the project team; an active exact assignment grants review and decision access to that request only, even outside ordinary Management Scope. General project reads remain scope-limited and unrelated mutations remain unavailable. The My Approvals list returns only a bounded summary, and approve/reject returns a minimal decision receipt; decisions use the canonical domain/workflow path. Unsupported approval categories have no placeholder destination.

No schema migration is introduced for Sponsor synchronization or review projections. Targeted API/React test classes cover the selected membership and routing/authorization behavior; these automated checks do not constitute browser UAT or a complete cross-tenant/session security matrix.


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
- Backend: Change Request integration tests were retried after the schema update but could not be built/run because referenced `ProjectPlanning` fails in the IKVM/MSBuild task host (`MSB4216`/`MSB4027`).
- Frontend: React typecheck and production build pass; focused Change Request tests pass; ESLint has warnings only.

## Migration Result
The Change Request approval migrations `20261021120000_AddProjectChangeRequestApprovals` and `20261021130000_AddChangeRequestApprovalModelIndexes` are recorded by the EF startup runner in local Development `NexusPmo`. The follow-up reconciles the snapshot with the configured indexes and defaults; it changes schema metadata only and preserves business rows. API build passed, EF reports no pending model changes, and the API health endpoint returned HTTP 200 during startup verification.

## Remaining Limitations
- No full project plan/WBS/task scheduling (deferred per requirements)
- Plan working-draft persistence foundation exists; plan editor integration and baseline promotion (Draft → Applied) remain separate work
- Initial (first) approved baseline workflow implemented (`POST /api/projects/{projectId}/baselines/initial`): creates `ProjectBaselineVersion` v1 approved/current and captures an `InitialBaseline` project snapshot, with tenant/audit/concurrency and duplicate rejection; no CR, Working Draft, frontend, or notifications
- Project templates page still uses mock data
- Project board/Gantt/matrix views not yet connected to real APIs
- Bulk operations not implemented

## Project Overview Issues / Risks cards — 2026-10-25
- Issues displays a status donut with a center total and a readable status/count/percentage breakdown. Segment colors come from the tenant-scoped Work Item status reference, with semantic fallbacks.
- Risks Overview includes a localized “View all” link scoped by `Project.PublicId`, matching the other project overview cards.
