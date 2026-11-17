# API Endpoints Reference

Base URL: `/api/v1`

All endpoints require JWT Bearer authentication unless noted.

---

## Authentication

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/auth/login` | Authenticate user, receive JWT | No |
| GET | `/auth/me` | Get current user profile, roles, permissions | Yes |
| POST | `/auth/logout` | Invalidate current session | Yes |
| POST | `/auth/switch-role` | Compatibility endpoint; returns the current user and does not switch roles | Yes |

## My Account

The account endpoints are scoped exclusively from the authenticated `NameIdentifier`, `tenantId`, and `sid` claims. They accept no caller-selected user or tenant IDs. Responses use explicit projections and never return password hashes, access tokens, or refresh-token hashes.

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/account` | Read personal and linked professional profile, tenant, explicit organization-settings grants, current organizational-unit/team/project memberships, security summary, and active-session metadata | Authenticated self |
| PUT | `/api/account/profile` | Update only the authenticated user's first and last name (each max 100 characters) | Authenticated self |
| POST | `/api/account/change-password` | Verify the current local password, apply the configured length policy, hash the new password, revoke all sessions, and require reauthentication | Authenticated self; rate limited |
| GET | `/api/account/sessions` | List active sessions belonging to the authenticated user and mark the current `sid` | Authenticated self |
| DELETE | `/api/account/sessions/{sessionId}` | Revoke only a session owned by the authenticated user and tenant | Authenticated self |
| POST | `/api/account/sessions/revoke-others` | Revoke all other active sessions while preserving the current `sid` | Authenticated self |

Profile and security mutations write audit records without passwords or tokens. Session revocation takes effect on the next API request because `UserPermissionClaimsMiddleware` validates the persisted `UserSession` for every authenticated request. The Account API adds no schema/migration; runtime use depends on the database already containing the mapped Resource/Team/Project relationship objects. Theme and language continue to use their existing browser preference providers; notification settings continue to use `/api/notifications/preferences`. Compact layout is currently unsupported.

---

## Projects

### Project Settings authorization

The Header Side Sheet and Project workflow settings use the canonical Project-scoped `ProjectSettings.View` and `ProjectSettings.Edit` keys. Financial sections keep their own View permissions and the existing sensitive `ProjectAnnualBudget.ManageApprovalConfiguration` action; edit access does not replace those checks.

| Method | Endpoint | Authorization |
|--------|----------|---------------|
| GET | `/api/projects/{projectId}/workflows` | `ProjectSettings.View` plus Tenant and management Project scope |
| PUT | `/api/projects/{projectId}/workflows/{domain}` | `ProjectSettings.View` + `ProjectSettings.Edit`, mutable management context, and Tenant/Project scope |
| GET | `/api/projects/{projectId}/annual-budgets/approval-configuration` | `ProjectSettings.View` + `ProjectAnnualBudget.View`, Tenant/Project scope |
| PUT | `/api/projects/{projectId}/annual-budgets/approval-configuration` | `ProjectSettings.View` + `ProjectSettings.Edit` + `ProjectAnnualBudget.ManageApprovalConfiguration` (which also requires `ProjectAnnualBudget.View`), mutable context and scope |
| GET | `/api/projects/{projectId}/financial-foundation` | `ProjectSettings.View` + `ProjectFinancialFoundation.View`, Tenant/Project scope |
| PUT | `/api/projects/{projectId}/financial-foundation/approval-configuration` | `ProjectSettings.View` + `ProjectSettings.Edit` + `ProjectFinancialFoundation.View` + `ProjectAnnualBudget.ManageApprovalConfiguration`, mutable context and scope |

Explicit user Deny overrides remain effective for these Project-scoped settings keys. Admin role identity does not bypass endpoint policies or management scope.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/projects` | List projects (paginated, filterable) |
| POST | `/projects` | Create a new project |
| GET | `/projects/{id}` | Get project details |
| GET | `/projects/{id}/overview/team-capacity?periodType={week|month|quarter}&startDate={date}&endDate={date}` | Get project team's period capacity and cross-project utilization summary (requires Project.View and Resource.View) |
| PUT | `/projects/{id}` | Update project |
| DELETE | `/projects/{id}` | Delete project |
| GET | `/projects/{id}/milestones` | List project milestones |
| POST | `/projects/{id}/milestones` | Create milestone |
| GET | `/projects/{id}/milestones/{mid}` | Get milestone details |
| PUT | `/projects/{id}/milestones/{mid}` | Update milestone |
| DELETE | `/projects/{id}/milestones/{mid}` | Delete milestone |
| GET | `/projects/{id}/baselines` | List baselines for project |
| POST | `/projects/{id}/baselines` | Create baseline |
| GET | `/projects/{id}/baselines/{bid}` | Get baseline details |
| GET | `/projects/{id}/deliverables` | List deliverables |
| POST | `/projects/{id}/deliverables` | Create deliverable |
| GET | `/projects/{id}/status-reports` | List status reports |
| POST | `/projects/{id}/status-reports` | Create status report |
| GET | `/projects/{id}/status-reports/current-progress?statusDate={date}&periodFrom={date}&periodTo={date}` | Read progress from the active Working plan (falling back to stored project progress) and capacity-service resource utilization for the selected reporting period (requires `StatusReport.Create` or `StatusReport.View`; project tenant/scope checks still apply) |
| GET | `/projects/{id}/status-reports/{reportId}` | Read a status report and its frozen performance snapshot |
| GET | `/projects/{id}/status-reports/compare?firstReportId={id}&secondReportId={id}` | Compare two Published reports for the same project using their frozen snapshots |
| PUT | `/projects/{id}/status-reports/{reportId}` | Update a Draft status report |
| POST | `/projects/{id}/status-reports/{reportId}/publish` | Publish a Draft directly (`StatusReport.Publish`); request carries final editable content and `rowVersion`, and publication captures the immutable project snapshot atomically |
| GET | `/projects/{id}/reporting/options` | List eligible project plans and versions for reporting configuration |
| GET | `/projects/{id}/reporting/configuration?effectiveAt={date}` | Read the effective reporting configuration |
| PUT | `/projects/{id}/reporting/configuration` | Create a versioned, effective-dated reporting configuration |
| GET | `/projects/{id}/performance?statusDate={date}&perspective={value}` | Read consolidated, individual-plan, or comparison EVM from the backend |
| GET | `/projects/{id}/members` | List project team members |
| POST | `/projects/{id}/members` | Add team member |
| DELETE | `/projects/{id}/members/{uid}` | Remove team member |
| GET | `/projects/{id}/phases` | List project phases |
| GET | `/projects/{id}/stakeholders` | List stakeholders |
| GET | `/projects/{id}/work-breakdown` | Get WBS |
| GET | `/projects/{id}/change-requests` | List persisted project change requests (`ChangeRequest.View`) |
| GET | `/projects/{id}/change-requests/approvers` | List active same-tenant project participants and external business approvers with effective `ChangeRequest.Approve`, plus project approval policy (`ChangeRequest.Manage`) |
| POST | `/projects/{id}/change-requests` | Create and persist a workflow-backed Draft (`ChangeRequest.Manage`) |

### Project workflow lookups

These purpose-specific routes supply minimal, tenant-scoped reference data to Project forms. Their workflow policies allow the existing `Project.Create` or `Project.Edit` operation permission, or the source entity's View permission. The policies are not catalog permissions and do not change role grants. The list endpoints retain their own View policies.

| Method | Endpoint | Authorization | Response scope |
|--------|----------|---------------|----------------|
| GET | `/api/portfolios/project-lookup?page=1&pageSize=200` | `WorkflowLookup.Project.Portfolios` (`Project.Create` / `Project.Edit` or `Portfolio.View`) | Portfolio ID, name, currency; tenant and management scope |
| GET | `/api/programs/project-lookup?page=1&pageSize=200` | `WorkflowLookup.Project.Programs` (`Project.Create` / `Project.Edit` or `Program.View`) | Program ID, name, Portfolio ID, currency; tenant and management scope |
| GET | `/api/organization/units/project-lookup` | `WorkflowLookup.Project.OrganizationalUnits` (`Project.Create` / `Project.Edit` or `Settings.View`) | Active same-tenant unit ID and name |
| GET | `/api/currencies/project-lookup` | `WorkflowLookup.Project.Currencies` (`Project.Create` / `Project.Edit` or `ReferenceData.View`) | Same-tenant currency ID, code, name, symbol, and base flag; no exchange rate |

`UserPermissionClaimsMiddleware` validates the session and rebuilds effective permissions before endpoint authorization; current Deny overrides therefore remove the corresponding operation claim before these policies run. The workflow routes remain authenticated and do not bypass Tenant or management-scope filtering.

### Route freshness and handover review 404s (verified 2026-11-03)

React calls the four lookup paths above and builds Project handover review URLs as `/api/handover-requests/{id}/review`; the controller templates match. A local Development API output check found that `NEXUS.Api/bin/Debug/net10.0/NEXUS.Modules.Portfolio.dll` and `NEXUS.Modules.Organization.dll` predated their current lookup controller source and contained no `project-lookup` route literals. The current source-built host exposes all four lookup routes in OpenAPI. Rebuild and restart the API after changing module controllers; a host launched from stale module assemblies can return 404 even when the current source is correct.

The handover review route is present in both checked outputs. Its `id` is the internal `ProjectHandoverRequest.Id`, not a Project ID or `Project.PublicId`. The endpoint returns 200 only for a pending request assigned to the authenticated active same-tenant incoming manager with `Handover.Approve`. A nonexistent/deleted request, closed request, wrong tenant, wrong assignee, or unavailable owning Project can legitimately return 404. `ApiExceptionHandler` maps unhandled exceptions to 500 and is not the source of these 404s.

Runtime route verification used a source-built Development host on `127.0.0.1:5000` with database initialization skipped: OpenAPI listed all five paths, and unauthenticated GETs returned 401 instead of 404. The configured Development connection target is `tcp:localhost,1433` / database `NexusPmo`; no database-backed request or account lookup was performed. Authorized live lookup responses and an assigned handover review were not exercised because no valid Development session was available in this run; see the Project workflow lookup cases in the [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md).

### Project Change Requests

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/project-change-requests/{id}` | Read a tenant- and management-scope-authorized request (`ChangeRequest.View`) | Yes |
| GET | `/project-change-requests/{id}/review` | Read the bounded projection for the current assigned approver (`ChangeRequest.Approve`); does not grant general Project access | Yes |
| PUT | `/project-change-requests/{id}` | Update a persisted Draft with row-version concurrency (`ChangeRequest.Manage`) | Yes |
| POST | `/project-change-requests/{id}/submit` | Snapshot selected approvers and submit through the configured Change Request workflow | Yes |
| GET | `/project-change-requests/pending-for-me` | List actionable assignments as a bounded inbox summary (no other approver identities/history) (`ChangeRequest.Approve`) | Yes |
| POST | `/project-change-requests/{id}/approve` | Approve through the canonical workflow; returns only a decision receipt (`ChangeRequest.Approve`) | Yes |
| POST | `/project-change-requests/{id}/reject` | Reject with a required decision note through the canonical workflow; returns only a decision receipt (`ChangeRequest.Approve`) | Yes |
| POST | `/project-change-requests/{id}/cancel` | Cancel a Draft or Pending request when authorized | Yes |
| POST | `/project-change-requests/{id}/remediate` | Reopen a legacy Pending request that has no assignment so approval setup can be completed | Yes |
| POST | `/project-change-requests/{id}/approvals/{assignmentId}/reassign` | Reassign a pending approval and preserve its reason/history | Yes |

### Project identifier contract

For every project-addressed URL (`/api/projects/{id}/...`) and project filter query (`projectId`), `{id}` / `projectId` is the tenant-scoped `Project.PublicId`. The API does not fall back to the database `Project.Id`; requests using an internal project ID in these external positions return `404`. The API resolves the public ID to the internal key only after validating the authenticated tenant, then applies the effective endpoint permission and management-scope checks before project data is returned.

Nested plan, plan-version, plan-content, and plan-import identifiers are checked against their parent project. Directly addressed project-owned resources (for example budgets, risks, work items, meetings, documents, baselines, and milestones) are checked against their owning project and the caller's management scope. Cross-reference bodies also validate parent ownership, including work-item parent links, meeting-linked work items, and commitment budget lines. Request-body foreign keys such as `projectId` remain internal relationship keys for existing create/update contracts; they are not project URL identifiers.

### Project manager handovers

Handover permission checks for a Project resolve its active owning Organization and evaluate Organization-owned Role grants in that Organization only. `Handover.AdminInitiate` is distinct from `Handover.View` (and retains its catalog dependency on View); it authorizes administrative initiation, while listing requests requires View. A View-only user can list but cannot initiate. The pending-for-me collection is narrowed to requests whose Project Organization grants the assigned manager effective `Handover.Approve`; request detail and decision routes recheck the permission against the request's Project. Tenant Admin's explicit protected baseline remains tenant-scoped. The selected incoming manager must be active and have effective `Handover.Approve` in the Project's Organization. Manager-initiated requests require the current manager plus `Handover.Manage`; administrative handover and first assignment require `Handover.AdminInitiate`. For a handover effective today, acceptance applies the manager change and captures its snapshot transactionally. For a future date, acceptance records an approved scheduled request; the current manager and permissions remain unchanged until the effective date, when the scheduler applies the transition and captures one snapshot. Backdated requests are rejected because historical project state cannot be reconstructed. Only one pending or approved scheduled handover may exist for a project; completed history is retained and does not prevent the next sequential handover.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/projects/{id}/handover-capabilities` | Resolve the current user's available handover actions |
| GET | `/projects/{id}/handover-managers` | List eligible active same-tenant incoming managers |
| GET | `/projects/{id}/handover-requests` | List requests for the Project; requires effective `Handover.View` in the Project's Organization for Organization-owned Role grants. The controller maps service failures to 404. |
| POST | `/projects/{id}/handover-requests` | Current manager requests a transfer (`Handover.Manage`) |
| POST | `/projects/{id}/handover-requests/admin` | Administrator starts a transfer for an assigned Project (`Handover.AdminInitiate`) |
| POST | `/projects/{id}/handover-requests/initial-assignment` | Administrator requests the first manager for an unassigned Project (`Handover.AdminInitiate`) |
| GET | `/handover-requests/pending-for-me` | List pending requests addressed to the current manager with effective `Handover.Approve` in each request Project's Organization |
| GET | `/handover-requests/{requestId}/review` | Read the bounded projection for the assigned incoming Project manager with effective `Handover.Approve` in the request Project's Organization |
| POST | `/handover-requests/{requestId}/accept` | Accept a due request or approve a future-dated request for scheduled application with effective `Handover.Approve` in the request Project's Organization |
| POST | `/handover-requests/{requestId}/reject` | Reject the request with a reason and effective `Handover.Approve` in the request Project's Organization |
| POST | `/handover-requests/{requestId}/cancel` | Requester cancels their pending request; requires the permission matching its initiation type |

For Project handovers, only manager-to-manager transfers capture a prior-owner snapshot. Initial Project assignment behavior is unchanged by the Portfolio and Program handover snapshot policy below.

### Assignment-scoped approval review

The remaining supported review routes are `GET /api/manager-handovers/programs/requests/{id}/review` and `GET /api/manager-handovers/portfolios/requests/{id}/review`. Project, Program, and Portfolio handovers and Project Change Requests return only a bounded read-only review projection after validating tenant, current actionable assignment, effective approval permission, request state, and owning object. This does not grant general access to the Project, Program, or Portfolio; decisions remain on their canonical accept/reject or workflow transition endpoints.

---

## Portfolios

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/portfolios` | List portfolios |
| POST | `/portfolios` | Create portfolio |
| GET | `/portfolios/{id}` | Get portfolio details |
| PUT | `/portfolios/{id}` | Update portfolio |
| DELETE | `/portfolios/{id}` | Delete portfolio |
| GET | `/portfolios/{id}/health` | Get portfolio health history |
| GET | `/portfolios/{id}/programs` | List programs in portfolio |
| GET | `/portfolios/{id}/budgets` | List portfolio budgets |
| GET | `/portfolios/{id}/risks` | List portfolio-level risks |
| GET | `/portfolios/{id}/optimization` | Get portfolio optimization data |

### Programs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/programs` | List programs |
| GET | `/programs/eligible-portfolios` | List active same-tenant Portfolios the caller can assign to a Program (`Program.Manage`, `Portfolio.View`) |
| POST | `/programs` | Create program |
| GET | `/programs/{id}` | Get program details |
| PUT | `/programs/{id}` | Update program |
| DELETE | `/programs/{id}` | Delete program |

### Manager handovers (portfolio and program)

These authenticated routes use the matching `Handover.View`, `Handover.Manage`, `Handover.AdminInitiate`, or `Handover.Approve` permission. Resource and user queries remain tenant-scoped.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/manager-handovers/pending-for-me` | List pending transfers addressed to the current manager (`Handover.Approve`) |
| GET | `/manager-handovers/{programs|portfolios}/{id}/capabilities` | Resolve current user's available actions (`Handover.Access`) |
| GET | `/manager-handovers/{programs|portfolios}/{id}/eligible-managers` | List active same-tenant managers with effective handover-approval access (`Handover.Manage` or `Handover.AdminInitiate`) |
| GET | `/manager-handovers/{programs|portfolios}/{id}/requests` | List transfer requests for the resource (`Handover.View`, `Handover.Manage`, or `Handover.AdminInitiate`) |
| POST | `/manager-handovers/{programs|portfolios}/{id}/requests` | Request manager transfer; the current manager remains assigned until acceptance (`Handover.Manage`) |
| POST | `/manager-handovers/{programs|portfolios}/{id}/admin-requests` | Administrator requests transfer from an existing manager (`Handover.AdminInitiate`) |
| POST | `/manager-handovers/{programs|portfolios}/{id}/initial-assignment-requests` | Administrator requests first assignment for an unassigned entity (`Handover.AdminInitiate`) |
| POST | `/manager-handovers/{programs|portfolios}/requests/{requestId}/accept` | Accept transfer, update manager assignment, and capture a snapshot (`Handover.Approve`) |
| POST | `/manager-handovers/{programs|portfolios}/requests/{requestId}/reject` | Reject transfer with a reason (`Handover.Approve`) |
| POST | `/manager-handovers/{programs|portfolios}/requests/{requestId}/cancel` | Requester cancels their pending request; requires the permission matching its initiation type |
| GET | `/manager-handovers/{programs|portfolios}/{id}/history` | List manager assignment history (`Handover.View`) |
| GET | `/manager-handovers/{programs|portfolios}/{id}/snapshots` | List immutable handover snapshots (`Handover.View`) |

`PUT /portfolios/{id}` and `PUT /programs/{id}` cannot change an already-assigned manager. Initial assignment during creation remains available; an unassigned legacy entity may receive its first manager through the initial-assignment request above. The recipient must accept before that first assignment is applied. Later manager changes must use the governed handover routes above.

Every accepted Portfolio or Program handover, including an initial manager assignment, captures an immutable snapshot in the same database transaction as the accepted request and manager assignment. For an initial assignment, the snapshot's previous manager ID is null; no synthetic manager is created.

---

## Work Items

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/work-items` | List work items (paginated, filterable by project/status/type) |
| GET | `/work-items/{projectId}/assignees` | List all current active Project Team member Resources, including those without a linked system User, and active tenant Teams assignable to a Work Item (`WorkItem.View`; `projectId` is the project PublicId in the URL) |
| POST | `/work-items` | Create work item |
| GET | `/work-items/{id}` | Get work item details |
| PUT | `/work-items/{id}` | Update work item |
| DELETE | `/work-items/{id}` | Delete work item |
| POST | `/work-items/{id}/assign` | Assign work item |
| POST | `/work-items/{id}/status` | Update work item status |

Work Item assignee targets are identified by `ResourceId` or `TeamId` (`targetType`/`targetId` in the lookup response); they are not system `UserId` values. The lookup includes every current active Project Team membership under the caller's tenant and management scope; a linked system account must also be active, while a team Resource without a linked account remains assignable. Internal project IDs remain the request-body relationship key when creating or updating Work Items.

The Work Item Details status track preserves the status reference order returned by the API (`DisplayOrder`, then `Code`). The current status is highlighted in place; another status is enabled only when the workflow API returns an executable transition to it. Inactive statuses are omitted unless one is the item's current status.

The Administration Work Item Statuses screen manages shared status reference data through `GET`, `POST`, and `PUT /api/reference-data/work-item-statuses`; writes require the `settings.write` permission. On create, the screen suggests a unique code from the English name using uppercase letters and underscores, adding a numeric suffix when an existing tenant code conflicts. The code can be adjusted before saving; the API validates the saved code and rejects duplicates.

---

## Resources

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/resources` | List resources (paginated, filterable) |
| GET | `/resources/reference-data` | Get enum-derived resource type and seniority choices with EN/AR labels; requires `Resource.View` |
| POST | `/resources` | Create resource |
| GET | `/resources/{id}` | Get resource details |
| PUT | `/resources/{id}` | Update resource |
| DELETE | `/resources/{id}` | Delete resource |
| GET | `/resources/{id}/availability` | Get resource availability |
| GET | `/resources/{id}/allocations` | Get resource allocations |
| GET | `/resources/{id}/skills` | Get resource skills |
| POST | `/resources/{id}/skills` | Add skill to resource |
| GET | `/resources/{id}/calendar` | Get resource calendar |
| GET | `/resources/{id}/utilization` | Get utilization metrics |

Resource create/update JSON represents `Type` as the `Staff` or `Vendor` enum name. `Vendor` continues to be stored using the existing `VendorResource` value; this does not add or require lookup data or a database migration.

### Allocations

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/allocations` | List allocations |
| POST | `/allocations` | Create allocation |
| GET | `/allocations/{id}` | Get allocation details |
| PUT | `/allocations/{id}` | Update allocation |
| DELETE | `/allocations/{id}` | Delete allocation |

---

## Costs

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/budgets` | List budgets |
| POST | `/budgets` | Create budget |
| GET | `/budgets/{id}` | Get budget details |
| PUT | `/budgets/{id}` | Update budget |
| DELETE | `/budgets/{id}` | Delete budget |
| GET | `/costs` | List cost entries |
| POST | `/costs` | Create cost entry |
| GET | `/costs/{id}` | Get cost entry details |
| PUT | `/costs/{id}` | Update cost entry |
| GET | `/invoices` | List invoices |
| POST | `/invoices` | Create invoice |
| GET | `/committed-costs` | List purchase commitments |

---

## Risks

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/risks` | List risks |
| POST | `/risks` | Create risk |
| GET | `/risks/{id}` | Get risk details |
| PUT | `/risks/{id}` | Update risk |
| DELETE | `/risks/{id}` | Delete risk |
| POST | `/risks/{id}/mitigations` | Add mitigation action |
| POST | `/risks/{id}/contingencies` | Add contingency plan |
| GET | `/risks/categories` | List risk categories |

---

## Issues

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/issues` | List issues |
| POST | `/issues` | Create issue |
| GET | `/issues/{id}` | Get issue details |
| PUT | `/issues/{id}` | Update issue |
| DELETE | `/issues/{id}` | Delete issue |
| POST | `/issues/{id}/actions` | Add action item |
| POST | `/issues/{id}/resolve` | Resolve issue |

---

## Meetings

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/meetings?page=&pageSize=&search=&status=&projectId=&projectIds=&dateFrom=&dateTo=` | List meetings with project, status, date, and title/objective filters |
| GET | `/meetings/calendar?from=&to=&search=&status=&projectId=&projectIds=` | Bounded calendar feed for a visible date range; overlaps the range, ordered by start time |
| GET | `/meetings/projects` | Lightweight accessible-project options for the project filter (public ID, name, code) |
| POST | `/meetings` | Create a scheduled meeting with organizer, participants, and optional ordered initial agenda |
| GET | `/meetings/{id}` | Get meeting details |
| PUT | `/meetings/{id}` | Edit scheduled meeting details or cancel a scheduled meeting |
| POST | `/meetings/{id}/start` | Start a scheduled meeting; records server UTC start time and actor audit (`Meeting.Edit`) |
| POST | `/meetings/{id}/end` | End an in-progress meeting; records server UTC end time and actor audit (`Meeting.Edit`) |
| DELETE | `/meetings/{id}` | Soft-delete meeting |
| GET/POST | `/meetings/{id}/attendees` | List or add participants |
| DELETE | `/meetings/{id}/attendees/{participantId}` | Remove participant |
| GET | `/meetings/{id}/participants` | List participant responses and attendance |
| PUT | `/meetings/{id}/attendance` | Compatibility endpoint for a signed-in participant's invitation response while Scheduled; this does not record actual attendance |
| PUT | `/meetings/{id}/participants/{participantId}/attendance` | Record actual participant attendance while In Progress (`Meeting.Edit`) |
| PUT | `/meetings/{id}/participants/{participantId}/attendance/organizer` | Record actual attendance while In Progress; requires `Meeting.View` and server-verified meeting organizer identity |
| GET/POST | `/meetings/{id}/agenda` | List or add agenda topics; agenda writes are limited to scheduled meetings |
| PUT | `/meetings/{id}/agenda/{itemId}` | Update agenda topic |
| PUT | `/meetings/{id}/agenda/reorder` | Reorder agenda topics |
| DELETE | `/meetings/{id}/agenda/{itemId}` | Delete agenda topic |
| GET/POST/PUT | `/meetings/{id}/minutes` | Read, create, or update meeting minutes draft; available only after completion |
| POST | `/meetings/{id}/minutes/finalize` | Finalize minutes; available only after completion |
| GET/POST | `/meetings/{id}/minutes/comments` | Read or add a comment |
| GET/POST | `/meetings/{id}/minutes/checklist` | List or add follow-up checklist items |
| PUT | `/meetings/{id}/minutes/checklist/{itemId}` | Update checklist item |
| PUT | `/meetings/{id}/minutes/checklist/reorder` | Reorder checklist items |
| DELETE | `/meetings/{id}/minutes/checklist/{itemId}` | Delete checklist item |
| GET/POST | `/meetings/{id}/minutes/attachments` | List or add attachments |
| GET | `/meetings/{id}/minutes/attachments/{attachmentId}/content` | Download attachment |
| DELETE | `/meetings/{id}/minutes/attachments/{attachmentId}` | Delete attachment |
| GET/POST | `/meetings/{id}/minutes/work-items` | List or link Work Items to the minutes |
| DELETE | `/meetings/{id}/minutes/work-items/{workItemId}` | Remove minutes Work Item link |
| GET/POST | `/meetings/{id}/actions` | List or create meeting action items |
| PUT | `/meetings/{id}/actions/{itemId}` | Update meeting action item |
| POST | `/meetings/{id}/actions/{itemId}/work-item` | Link a same-project Task or Issue (`Meeting.Manage` and `WorkItem.View`) |
| DELETE | `/meetings/{id}/actions/{itemId}/work-item` | Remove the action-item Work Item link (`Meeting.Manage`) |
| DELETE | `/meetings/{id}/actions/{itemId}` | Delete meeting action item |
| GET | `/meetings/{id}/decisions` | Read live decision notes while In Progress (`Meeting.View`) |
| PUT | `/meetings/{id}/decisions` | Record live decisions while In Progress in the existing minutes draft storage (`Meeting.Manage`) |
| GET | `/meetings/{id}/sharing/recipients?search=` | Search up to 50 active internal users in the current tenant, with attendee and channel eligibility context (`Meeting.Manage`) |
| GET | `/meetings/{id}/sharing/history` | Read grouped sharing operations with selected recipients and per-channel delivery results (`Meeting.Manage`) |
| POST | `/meetings/{id}/sharing/minutes-email` | Queue the current published Minutes version for explicitly selected internal recipients (`Meeting.Manage`) |
| POST | `/meetings/{id}/sharing/completion` | Create a completion notification for explicitly selected active users in the current tenant (`Meeting.Manage`) |

The global list's `projectId` filter accepts the tenant-scoped public project ID. To filter several authorized projects, send `projectIds` as a comma-separated or repeated query parameter; `projectId` and `projectIds` are mutually exclusive. `ProjectPublicIdRoutingMiddleware` resolves every public ID against the caller's tenant, rejects malformed or unknown IDs, and rewrites the filter to internal IDs before the controller runs; unknown IDs never silently widen the result. The list and calendar read paths both apply tenant query filters and the caller's management scope, so unauthorized meetings are never returned. Calendar queries use overlap semantics (`StartTime < to` and `EndTime > from`) so multi-day and in-progress meetings are included, require `from`/`to`, reject ranges longer than 366 days, and cap the response at 2000 events with `isTruncated: true` when more exist. `startTime`/`endTime` in list, detail, and calendar responses are UTC instants; clients send UTC instants in `dateFrom`/`dateTo` and the calendar `from`/`to`. Meeting creation accepts `agendaItems` as an ordered array and saves them with the meeting. Its request-body `projectId` remains the internal project ID returned by the resolved project DTO. The participant response uses `invitationResponseStatus` for Pending/Accepted/Declined/Tentative and `actualAttendanceStatus` for Present/Late/Excused/Absent; `attendanceStatus` is a compatibility projection. Scheduled meetings expose invitee details but not actual attendance. The participants/attendance workspace is available after start. Agenda writes are limited to Scheduled meetings; action item listing and writes are available In Progress and Completed; action-to-Work-Item linking is available only after completion. An action accepts optional `agendaItemId`, which must identify an agenda item belonging to that same meeting. A meeting action can link to a same-tenant Task or Issue in the meeting's project after completion. Creating a linked Task uses the existing Work Items API and requires `WorkItem.Create`. The action list returns a linked Work Item title only to callers with `WorkItem.View` and matching tenant/project scope. Meeting-action and Work Item statuses remain independently owned. Decision notes use a dedicated In Progress endpoint and the existing `MeetingMinutes.Decisions` field. Minutes, their comments, checklist, attachments, and minutes Work Item links are available only after completion. The reminder scheduler uses the shared Notifications service; opted-in internal users can receive Email when the SMTP provider is configured.

The sharing recipient API accepts a search term and returns at most 50 active internal users from the authenticated meeting tenant, regardless of project association. The attendee shortcut remains restricted to actual Present/Late participants; the all-users group supports searching the full tenant directory. Responses include whether the recipient's Minutes email preference and shared email provider are available, plus attendance context when applicable. The server revalidates every selected recipient as active and same-tenant; `Meeting.Manage`, completed meeting state, explicit selection, channel preferences, and email-provider availability remain enforced. The side sheet's three steps cover recipient selection, message preview, then a final recipient/message/channel review and confirmation. Step navigation remains available for preview and review when final-send requirements are unmet; the final send action stays disabled until requirements are satisfied. Nothing is selected by default; ineligible recipients are explained and cannot be sent to. Sharing history groups each audited operation and shows recipient-level and channel-level delivery status. These response additions reuse existing identity, audit, notification, and delivery data and require no migration.

---

## Workflows

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/workflows` | List workflow templates |
| POST | `/workflows` | Create workflow template |
| GET | `/workflows/{id}` | Get workflow template |
| PUT | `/workflows/{id}` | Update workflow template |
| DELETE | `/workflows/{id}` | Delete workflow template |
| GET | `/workflows/{id}/versions` | List workflow versions |
| POST | `/workflows/{id}/versions` | Create new version |
| GET | `/workflows/{id}/instances` | List workflow instances |
| GET | `/workflow-instances/{id}` | Get workflow instance |
| POST | `/workflow-instances/{id}/transition` | Execute transition |

---

## Approvals

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/approvals` | List approval requests |
| POST | `/approvals` | Create approval request |
| GET | `/approvals/{id}` | Get approval details |
| POST | `/approvals/{id}/approve` | Approve request |
| POST | `/approvals/{id}/reject` | Reject request |
| POST | `/approvals/{id}/delegate` | Delegate approval |
| POST | `/approvals/{id}/escalate` | Escalate approval |
| GET | `/approvals/delegations` | List delegations |
| POST | `/approvals/delegations` | Create delegation rule |

---

## Notifications

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/notifications?page=&pageSize=&unreadOnly=` | List visible notifications for the authenticated user |
| GET | `/api/notifications/unread-count` | Read the lightweight visible unread count |
| POST | `/api/notifications/{id}/mark-read` | Mark one of the current user's notifications as read |
| PUT | `/api/notifications/read-all` | Mark all visible notifications for the current user as read |
| GET | `/api/notifications/preferences` | Read current-user preferences and channel availability |
| PUT | `/api/notifications/preferences` | Update preferences for supported event types |
| POST | `/api/notifications/devices` | Register/update an FCM device token for the authenticated user |
| DELETE | `/api/notifications/devices/{deviceId}` | Deactivate a device owned by the authenticated user |

All routes require JWT authentication. User and tenant identity come from claims; preference and device requests cannot select another user. Push and configured email delivery are asynchronous through the shared Notifications worker. Meeting sharing actions remain manual and require `Meeting.Manage`.

---

## Documents

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/documents` | List documents |
| POST | `/documents` | Create document record |
| GET | `/documents/{id}` | Get document details |
| PUT | `/documents/{id}` | Update document metadata |
| DELETE | `/documents/{id}` | Delete document |
| POST | `/documents/{id}/upload` | Upload file (multipart form) |
| GET | `/documents/{id}/download` | Download file |
| GET | `/documents/{id}/versions` | List document versions |
| POST | `/documents/{id}/versions` | Create new version |
| GET | `/documents/categories` | List document categories |
| GET | `/documents/{id}/reviews` | List document reviews |

---

## Reports

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/reports` | List report definitions |
| POST | `/reports` | Create report definition |
| GET | `/reports/{id}` | Get report definition |
| PUT | `/reports/{id}` | Update report definition |
| DELETE | `/reports/{id}` | Delete report definition |
| POST | `/reports/{id}/generate` | Generate report |
| GET | `/reports/{id}/exports` | List report exports |
| GET | `/reports/exports/{id}` | Get export details/download |
| GET | `/reports/scheduled` | List scheduled reports |
| POST | `/reports/{id}/schedule` | Schedule report |
| PUT | `/reports/scheduled/{id}` | Update schedule |
| DELETE | `/reports/scheduled/{id}` | Delete schedule |

---

## Plugins

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/plugins` | List available plugins |
| GET | `/plugins/{id}` | Get plugin details |
| POST | `/plugins/{id}/install` | Install plugin |
| POST | `/plugins/{id}/activate` | Activate plugin |
| POST | `/plugins/{id}/deactivate` | Deactivate plugin |
| DELETE | `/plugins/{id}` | Uninstall plugin |
| GET | `/plugins/{id}/dependencies` | Get dependency tree |
| GET | `/plugins/{id}/settings` | Get plugin settings |
| PUT | `/plugins/{id}/settings` | Update plugin settings |

---

## Integrations

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/integrations` | List integrations |
| POST | `/integrations` | Create integration connection |
| GET | `/integrations/{id}` | Get integration details |
| PUT | `/integrations/{id}` | Update integration |
| DELETE | `/integrations/{id}` | Delete integration |
| POST | `/integrations/{id}/sync` | Trigger data sync |
| GET | `/integrations/{id}/syncs` | Get sync history |
| GET | `/integrations/{id}/syncs/{sid}` | Get sync detail |
| POST | `/integrations/{id}/webhooks` | Register webhook |
| DELETE | `/integrations/{id}/webhooks/{wid}` | Delete webhook |
| GET | `/integrations/{id}/connections` | Test connection |

---

## Directory Integration (Microsoft Entra ID / Active Directory)

These routes use the authenticated tenant from the JWT and require the listed directory permission. The controller route is `/api/directory-integrations` (not under the `/api/v1` base prefix above).

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| GET | `/api/directory-integrations/connections` | `Directory.View` | List current-tenant directory connections |
| POST | `/api/directory-integrations/connections` | `Directory.Manage` | Create a connection |
| PUT | `/api/directory-integrations/connections/{connectionId}` | `Directory.Manage` | Update a connection |
| POST | `/api/directory-integrations/connections/{connectionId}/test` | `Directory.TestConnection` | Test provider connectivity |
| GET / PUT | `/api/directory-integrations/connections/{connectionId}/scope` | `Directory.View` / `Directory.Manage` | Read or update sync scope |
| GET / PUT | `/api/directory-integrations/connections/{connectionId}/field-mappings` | `Directory.View` / `Directory.ManageMappings` | Read or update field mappings |
| GET | `/api/directory-integrations/connections/{connectionId}/groups` | `Directory.PreviewSync`, `Directory.ManageMappings`, or `Directory.Manage` | Browse provider groups |
| GET / PUT | `/api/directory-integrations/connections/{connectionId}/group-mappings` | `Directory.View` / `Directory.ManageMappings` | Read or update group-to-role mappings |
| GET / PUT | `/api/directory-integrations/connections/{connectionId}/organization-mappings` | `Directory.View` / `Directory.ManageMappings` | Read or update organization mappings |
| GET / PUT | `/api/directory-integrations/connections/{connectionId}/provisioning-policy` | `Directory.View` / `Directory.ManageProvisioningPolicy` | Read or update provisioning policy |
| GET | `/api/directory-integrations/connections/{connectionId}/preview/{itemType}` | `Directory.PreviewSync` | Preview a bounded provider page before sync |
| POST | `/api/directory-integrations/connections/{connectionId}/sync` | `Directory.ExecuteSync` | Apply selected sync actions |
| GET | `/api/directory-integrations/sync-runs` | `Directory.View` | List sync history |
| GET | `/api/directory-integrations/sync-runs/{runId}` | `Directory.View` | Read sync-run details |
| GET | `/api/directory-integrations/reference/organizational-units` | `Directory.View` | List current-tenant organization units |
| GET | `/api/directory-integrations/reference/calendars` | `Directory.View` | List current-tenant working calendars |

Every route also requires JWT authentication. Secrets are resolved only by the API host and are never sent by the client.

---

## AI Planning

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/ai/generate-plan` | Generate project plan from requirements |
| POST | `/ai/review-plan` | Review and optimize existing plan |
| POST | `/ai/analyze-risk` | AI-powered risk analysis |
| POST | `/ai/optimize-resources` | Resource optimization suggestions |
| POST | `/ai/insights` | Generate project insights |
| GET | `/ai/requests` | List past AI requests |
| GET | `/ai/requests/{id}` | Get AI request/response details |
| GET | `/ai/requests/{id}/status` | Check AI processing status |

---

## Audit

Audit records are append-only. There is no API to create, update or delete an audit
event; entries are produced by the owning module inside its own unit of work.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/projects/{projectId}/audit` | Project audit trail: server-side paging, filters (`from`, `to`, `module`, `action`, `actorId`, `entityType`, `search`, `sort=newest\|oldest`), deterministic `timestampUtc` + `id` ordering. Requires `Audit.View` plus management scope on the project |
| GET | `/projects/{projectId}/audit/facets` | Available modules, actions, actors, record types and the first captured event for the project. Requires `Audit.View` |
| GET | `/audit` | Tenant-wide audit entries (paginated, filterable). Requires `Audit.View` |

`{projectId}` is the project **public** identifier. The response reports the caller's
detail entitlements: `includeFinancialDetail` (from `ProjectFinancialAuditTrail.View`) and
`includeGovernanceDetail` (change requests, status reports, stakeholders). Masked
values are returned as `[restricted]`; secret material is removed before persistence
and never returned.

---

## Administration

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | List tenant users and their assigned roles (`Users.View`) |
| POST | `/api/users` | Create a user (`Users.ManageAccounts`); assigning roles or creating an inactive account also requires `Users.AssignRoles` or `Users.SetStatus` |
| PUT | `/api/users/{id}` | Update profile, role assignments, and status with the matching `Users.ManageAccounts`, `Users.AssignRoles`, and `Users.SetStatus` permissions |
| GET | `/api/users/{id}/permissions` | Read direct overrides (`UserPermissions.Manage`) |
| PUT | `/api/users/{id}/permissions` | Save direct `Allow`/`Deny` overrides (`UserPermissions.Manage`) |
| GET | `/api/roles` | List roles (`Roles.View`) |
| GET | `/api/roles/manage` | List roles manageable by the actor (`Roles.View`) |
| GET | `/api/roles/permissions` | List assignable permissions (`Roles.ConfigurePermissions`) |
| GET | `/api/roles/{id}` | Read a role and its permissions (`Roles.View`) |
| POST | `/api/roles` | Create a tenant role (`Roles.Create`; `Roles.ConfigurePermissions` when assigning permissions) |
| PUT | `/api/roles/{id}` | Update role metadata (`Roles.EditDetails`) and permission set (`Roles.ConfigurePermissions`) |
| DELETE | `/api/roles/{id}` | Soft-delete an unassigned custom role (`Roles.Delete`) |
| GET | `/admin/settings` | Get system settings |
| PUT | `/admin/settings` | Update system settings |
| GET | `/admin/license` | Get license information |

### Entity assignment recovery

These administrative discovery routes accept `Project`, `Program`, or `Portfolio` plus a tenant-scoped `PublicId` (never an internal entity key). Discovery is limited to the authenticated tenant. `Governance.AssignmentRecovery.View`, `.Initiate`, and `.Manage` are independently assignable permissions; manager initiation also requires `Handover.AdminInitiate`, while administrative handover additionally requires `.Manage`.

| Method | Endpoint | Description | Authorization |
|--------|----------|-------------|---------------|
| GET | `/api/governance/assignment-recovery/{entityType}` | Tenant-scoped, paginated recovery list, summary, search, and state filter | Any recovery permission |
| GET | `/api/governance/assignment-recovery/projects/{publicId}/summary` | Minimal read-only Project context for the initial-manager assignment sheet: name, code, description, dates, and Sponsor name | Initiate + `Handover.AdminInitiate` |
| GET | `/api/governance/assignment-recovery/{entityType}/{publicId}/manager-candidates` | Eligible incoming managers for the entity | Initiate or Manage |
| GET | `/api/governance/assignment-recovery/projects/{publicId}/sponsor-candidates` | Active same-tenant Sponsor candidates | Manage |
| GET | `/api/governance/assignment-recovery/{entityType}/{publicId}/history` | Assignment and handover history for that entity | Any recovery permission |
| POST | `/api/governance/assignment-recovery/{entityType}/{publicId}/initial-assignment` | Start the canonical first-manager approval flow; manager is not changed until acceptance | Initiate + `Handover.AdminInitiate` |
| POST | `/api/governance/assignment-recovery/{entityType}/{publicId}/administrative-handover` | Start a canonical manager-transfer approval flow | Manage + `Handover.AdminInitiate` |
| POST | `/api/governance/assignment-recovery/projects/{publicId}/sponsor` | Assign an unassigned Sponsor and synchronize the existing project-team projection | Manage |
| POST | `/api/governance/assignment-recovery/{entityType}/{publicId}/reconcile` | Reconcile legacy direct manager, current assignment, and Project PM team projection | Manage |

Incoming-manager decisions remain on the existing canonical My Approvals/handover routes. Recovery does not directly set the manager on first assignment or bypass the acceptance step.

The Project summary is queried only for a selected Project in the initial-assignment flow. It returns the bounded fields above, resolves the Project by authenticated Tenant and `PublicId`, and rechecks both effective permissions so an Active Deny is honored. A foreign-Tenant or unavailable Project is non-disclosing. This read remains inside the existing Native-mode, tenant-wide Assignment Recovery exception; it does not grant access to ordinary Project routes or Project editing.

---

## Organization

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/organization` | Get organization details |
| PUT | `/organization` | Update organization |
| GET | `/business-units` | List business units |
| POST | `/business-units` | Create business unit |
| GET | `/business-units/{id}` | Get business unit |
| PUT | `/business-units/{id}` | Update business unit |
| GET | `/departments` | List departments |
| POST | `/departments` | Create department |
| GET | `/teams` | List teams |
| POST | `/teams` | Create team |
| GET | `/locations` | List locations |
| GET | `/lookups` | Get lookup values |
| GET | `/cost-centres` | List cost centres |
| GET | `/currencies` | List supported currencies |
| GET | `/working-calendars` | List working calendars |

---

## Health

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check endpoint (unauthenticated) |
