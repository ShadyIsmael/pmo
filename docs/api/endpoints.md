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
| POST | `/auth/switch-role` | Switch active role context | Yes |

---

## Projects

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
| GET | `/projects/{id}/status-reports/current-progress?statusDate={date}` | Read backend-calculated reporting progress at a status date |
| GET | `/projects/{id}/status-reports/{reportId}` | Read a status report and its frozen performance snapshot |
| GET | `/projects/{id}/status-reports/compare?firstReportId={id}&secondReportId={id}` | Compare two Published reports for the same project using their frozen snapshots |
| PUT | `/projects/{id}/status-reports/{reportId}` | Update a Draft status report |
| POST | `/projects/{id}/status-reports/{reportId}/generate` | Capture the immutable performance/configuration snapshot and enter Generated (`StatusReport.Generate`) |
| POST | `/projects/{id}/status-reports/{reportId}/submit` | Submit a generated report for review (`StatusReport.Submit`) |
| POST | `/projects/{id}/status-reports/{reportId}/approve` | Approve a submitted report; the author cannot approve their own report (`StatusReport.Approve`) |
| POST | `/projects/{id}/status-reports/{reportId}/return` | Return a submitted report for revision (`StatusReport.Approve`) |
| POST | `/projects/{id}/status-reports/{reportId}/reopen` | Reopen a returned report as a draft and clear its generated snapshot (`StatusReport.Edit`) |
| POST | `/projects/{id}/status-reports/{reportId}/publish` | Publish an approved report; the frozen snapshot is immutable (`StatusReport.Publish`) |
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

All handover resources and candidate users are scoped to the authenticated tenant. The selected incoming manager must be active and have effective `Handover.Approve`. Manager-initiated requests require the current manager plus `Handover.Manage`; administrative handover and first assignment require `Handover.AdminInitiate`. The current manager changes only after the selected incoming manager accepts.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/projects/{id}/handover-capabilities` | Resolve the current user's available handover actions |
| GET | `/projects/{id}/handover-managers` | List eligible active same-tenant incoming managers |
| GET | `/projects/{id}/handover-requests` | List requests for the Project (`Handover.View`, `Handover.Manage`, or `Handover.AdminInitiate`) |
| POST | `/projects/{id}/handover-requests` | Current manager requests a transfer (`Handover.Manage`) |
| POST | `/projects/{id}/handover-requests/admin` | Administrator starts a transfer for an assigned Project (`Handover.AdminInitiate`) |
| POST | `/projects/{id}/handover-requests/initial-assignment` | Administrator requests the first manager for an unassigned Project (`Handover.AdminInitiate`) |
| GET | `/handover-requests/pending-for-me` | List pending requests addressed to the current manager (`Handover.Approve`) |
| GET | `/handover-requests/{requestId}/review` | Read the bounded projection for the assigned incoming Project manager (`Handover.Approve`) |
| POST | `/handover-requests/{requestId}/accept` | Accept the request and apply the manager assignment (`Handover.Approve`) |
| POST | `/handover-requests/{requestId}/reject` | Reject the request with a reason (`Handover.Approve`) |
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
| POST | `/work-items` | Create work item |
| GET | `/work-items/{id}` | Get work item details |
| PUT | `/work-items/{id}` | Update work item |
| DELETE | `/work-items/{id}` | Delete work item |
| POST | `/work-items/{id}/assign` | Assign work item |
| POST | `/work-items/{id}/status` | Update work item status |

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
| GET | `/meetings` | List meetings |
| POST | `/meetings` | Create meeting |
| GET | `/meetings/{id}` | Get meeting details |
| PUT | `/meetings/{id}` | Update meeting |
| DELETE | `/meetings/{id}` | Delete meeting |
| GET | `/meetings/{id}/agenda` | Get meeting agenda |
| POST | `/meetings/{id}/agenda` | Add agenda item |
| GET | `/meetings/{id}/participants` | List participants |
| POST | `/meetings/{id}/participants` | Add participant |
| GET | `/meetings/{id}/minutes` | Get meeting minutes |
| POST | `/meetings/{id}/minutes` | Add minutes |
| GET | `/meetings/{id}/actions` | Get action items |
| POST | `/meetings/{id}/actions` | Create action item |

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

All routes require JWT authentication. User and tenant identity come from claims; preference and device requests cannot select another user. Push delivery is asynchronous. Email delivery is not implemented.

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

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/audit-log` | List audit entries (paginated, filterable) |
| GET | `/audit-log/{id}` | Get audit entry details |
| GET | `/audit-log/entity/{entityType}/{entityId}` | Get audit trail for specific entity |
| GET | `/audit-log/user/{userId}` | Get audit trail for specific user |
| GET | `/audit-log/activities` | List activity types |

---

## Administration

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | List tenant users and their assigned roles (`users.read` or `users.write`) |
| POST | `/api/users` | Create user and assign tenant roles (`users.write`) |
| PUT | `/api/users/{id}` | Update user profile and role assignments (`users.write`) |
| GET | `/api/users/{id}/permissions` | Read role access and direct overrides (`users.write`) |
| PUT | `/api/users/{id}/permissions` | Save direct `Allow`/`Deny` overrides (`users.write`) |
| GET | `/api/roles` | List role options for user assignment (authenticated) |
| GET | `/api/roles/manage` | List roles manageable by the actor (`users.write`) |
| GET | `/api/roles/permissions` | List permissions the actor may assign (`users.write`) |
| GET | `/api/roles/{id}` | Get a manageable role and its permissions (`users.write`) |
| POST | `/api/roles` | Create a tenant role (`users.write`) |
| PUT | `/api/roles/{id}` | Update role metadata and permission set (`users.write`) |
| DELETE | `/api/roles/{id}` | Soft-delete an unassigned custom role (`users.write`) |
| GET | `/admin/settings` | Get system settings |
| PUT | `/admin/settings` | Update system settings |
| GET | `/admin/license` | Get license information |

### Entity assignment recovery

These administrative discovery routes accept `Project`, `Program`, or `Portfolio` plus a tenant-scoped `PublicId` (never an internal entity key). Discovery is limited to the authenticated tenant. `Governance.AssignmentRecovery.View`, `.Initiate`, and `.Manage` are independently assignable permissions; manager initiation also requires `Handover.AdminInitiate`, while administrative handover additionally requires `.Manage`.

| Method | Endpoint | Description | Authorization |
|--------|----------|-------------|---------------|
| GET | `/api/governance/assignment-recovery/{entityType}` | Tenant-scoped, paginated recovery list, summary, search, and state filter | Any recovery permission |
| GET | `/api/governance/assignment-recovery/{entityType}/{publicId}/manager-candidates` | Eligible incoming managers for the entity | Initiate or Manage |
| GET | `/api/governance/assignment-recovery/projects/{publicId}/sponsor-candidates` | Active same-tenant Sponsor candidates | Manage |
| GET | `/api/governance/assignment-recovery/{entityType}/{publicId}/history` | Assignment and handover history for that entity | Any recovery permission |
| POST | `/api/governance/assignment-recovery/{entityType}/{publicId}/initial-assignment` | Start the canonical first-manager approval flow; manager is not changed until acceptance | Initiate + `Handover.AdminInitiate` |
| POST | `/api/governance/assignment-recovery/{entityType}/{publicId}/administrative-handover` | Start a canonical manager-transfer approval flow | Manage + `Handover.AdminInitiate` |
| POST | `/api/governance/assignment-recovery/projects/{publicId}/sponsor` | Assign an unassigned Sponsor and synchronize the existing project-team projection | Manage |
| POST | `/api/governance/assignment-recovery/{entityType}/{publicId}/reconcile` | Reconcile legacy direct manager, current assignment, and Project PM team projection | Manage |

Incoming-manager decisions remain on the existing canonical My Approvals/handover routes. Recovery does not directly set the manager on first assignment or bypass the acceptance step.

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
