# UPI Task Context Manifest

Task: **ProjectsListPage passes project publicId into ProjectDetailPage route; ProjectDetailPage fetches project shell using route id; inspect API public ID routing and shell response phase fields**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/tests/NEXUS.Api.Tests/Unit/ProjectPublicIdRoutingMiddlewareTests.cs` | 1-687 | 12136 | filename-match:id, filename-match:public, filename-match:routing, module-mentioned, primary-module, symbol-match:ProjectPublicIdRoutingMiddlewareTests |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Api/Finance/PlannedPaymentController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetClosingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetDraftApprovalConfigurationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetLineController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectCashFlowController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialFoundationController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectFinancialReportingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Finance/ProjectPaymentRequestController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Api/Management/ManagementContextAccessor.cs` — REFERENCES_TYPE (inferred-token-match; ManagementContextAccessor)
- `apps/api/src/NEXUS.Api/Management/ManagementContextAccessor.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Management)
- `apps/api/src/NEXUS.Api/Management/ManagementContextMiddleware.cs` — REFERENCES_TYPE (inferred-token-match; ManagementContextMiddleware)
- `apps/api/src/NEXUS.Api/Management/ManagementContextMiddleware.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Management)
- `apps/api/src/NEXUS.Api/Management/ManagementController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Management)
- `apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPublicIdRoutingMiddleware)
- `apps/api/src/NEXUS.Api/Management/ProjectPublicIdRoutingMiddleware.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Management)
- `apps/api/src/NEXUS.Api/Management/ProjectResourceScopeMiddleware.cs` — REFERENCES_TYPE (inferred-token-match; ProjectResourceScopeMiddleware)
- `apps/api/src/NEXUS.Api/Management/ProjectResourceScopeMiddleware.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Management)
- `apps/api/src/NEXUS.Api/Management/VersionController.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Api.Management)
- `apps/api/src/NEXUS.Modules.Audit/Api/ProjectAuditController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/projects/{project.PublicId:D}/overview)
- `apps/api/src/NEXUS.Modules.CostManagement/Api/BudgetController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/budgets/{budget.Id:D})
- `apps/api/src/NEXUS.Modules.CostManagement/Domain/Budget.cs` — REFERENCES_TYPE (inferred-token-match; Budget)
- `apps/api/src/NEXUS.Modules.Identity/Domain/User.cs` — REFERENCES_TYPE (inferred-token-match; User)
- `apps/api/src/NEXUS.Modules.Meetings/Api/MeetingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/meetings/{meetingA.Id:D}/minutes/attachments/{minutesAttachment.Id:D}/content)
- `apps/api/src/NEXUS.Modules.Meetings/Api/MeetingSharingController.cs` — CALLS_API_ROUTE (inferred-route-prefix; /api/meetings/{meetingA.Id:D}/minutes/attachments/{minutesAttachment.Id:D}/content)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/Meeting.cs` — REFERENCES_TYPE (inferred-token-match; Meeting)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/Meeting.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Meetings.Domain)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/MeetingAction.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Meetings.Domain)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/MeetingActionStatuses.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Meetings.Domain)
- `apps/api/src/NEXUS.Modules.Meetings/Domain/MeetingAgenda.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Meetings.Domain)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **12136** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
