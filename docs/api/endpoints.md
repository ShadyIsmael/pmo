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
| GET | `/projects/{id}/members` | List project team members |
| POST | `/projects/{id}/members` | Add team member |
| DELETE | `/projects/{id}/members/{uid}` | Remove team member |
| GET | `/projects/{id}/phases` | List project phases |
| GET | `/projects/{id}/stakeholders` | List stakeholders |
| GET | `/projects/{id}/work-breakdown` | Get WBS |

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
| GET | `/notifications` | List notifications for current user |
| POST | `/notifications` | Create notification |
| GET | `/notifications/{id}` | Get notification details |
| PUT | `/notifications/{id}/read` | Mark notification as read |
| PUT | `/notifications/read-all` | Mark all notifications as read |
| GET | `/notifications/preferences` | Get notification preferences |
| PUT | `/notifications/preferences` | Update notification preferences |
| GET | `/notifications/templates` | List notification templates |

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
| GET | `/admin/users` | List all users |
| POST | `/admin/users` | Create user |
| GET | `/admin/users/{id}` | Get user details |
| PUT | `/admin/users/{id}` | Update user |
| DELETE | `/admin/users/{id}` | Delete user |
| POST | `/admin/users/{id}/roles` | Assign role to user |
| DELETE | `/admin/users/{id}/roles/{rid}` | Remove role from user |
| GET | `/admin/roles` | List roles |
| POST | `/admin/roles` | Create role |
| GET | `/admin/roles/{id}` | Get role with permissions |
| PUT | `/admin/roles/{id}` | Update role |
| DELETE | `/admin/roles/{id}` | Delete role |
| GET | `/admin/permissions` | List all permissions |
| POST | `/admin/roles/{id}/permissions` | Assign permissions to role |
| DELETE | `/admin/roles/{id}/permissions/{pid}` | Remove permission from role |
| GET | `/admin/settings` | Get system settings |
| PUT | `/admin/settings` | Update system settings |
| GET | `/admin/license` | Get license information |

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
