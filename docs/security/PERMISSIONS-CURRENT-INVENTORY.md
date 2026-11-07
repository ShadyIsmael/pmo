# Current Permissions Inventory

**Audit date:** 2026-11-01  
**Status:** Audit only; Product Owner review required  
**Implementation changes:** None made

## Executive Summary

The current source-seeded permission catalog contains **103 unique Resource.Action keys**. The seed list contains 103 definitions and the tenant/resource/action database index is unique, so there are **0 exact duplicate key definitions** in the source catalog. For the two-level review, 27 keys are classified as view/read capabilities and 76 as operation capabilities. This is a key count, not a count of every button, modal command, or low-level UI event.

The React router contains **113 rendered route declarations** (excluding redirect and fallback routes). These include nested tabs and alternate paths that reuse components; they are not 113 unique React components. All route declarations in the supported frontend, apps/web-react, were reviewed. Route and key-level findings are summarized below; the CSV contains one row per catalog key with labels, seed-role mappings, policy references, and evidence.

Key findings:

- The source catalog has no exact duplicate key definitions. It has **semantic overlaps** between legacy lower-case keys and newer canonical keys, and between broad Manage/Write grants and narrower actions.
- Nine legacy keys appear unused by an implemented feature authorization check: projects.write, projects.delete, portfolios.read, portfolios.write, resources.read, resources.write, reports.read, reports.export, and manager.handover. Treat these as **orphan candidates**, not deletion instructions.
- ProgramService checks for Program.CreateAcrossPortfolios, but that key is absent from the 103-key catalog and Roles & Permissions. This is a missing catalog/UI entry.
- Payment Request approval is currently authorized by finances.write plus workflow assignment and active Project Team checks. That allows a financial writer to approve if assigned and does not provide a separate approval-only capability.
- Annual Budget create, draft edit, submit, revise, and approval-configuration update use Finances.Write. Budget approve and fiscal-year closing actions use dedicated keys, but their policies also require finances.write. Review-only users therefore need broad financial write access.
- ManagementScopeResolver grants tenant-wide management scope by checking the system role name admin. This is a role-name security exception and conflicts with the repository instruction not to authorize by hard-coded role names.
- The general Reports and Plugins API controllers require authentication but have no resource-specific read policy. The reports.read and reports.export keys are not used by those routes.
- The Roles & Permissions UI groups records by raw Resource, without a Screen/View versus Action hierarchy. It renders independent permission checkboxes and does not present scope inheritance or permission dependencies.
- English and Arabic permission labels are generated from shared Resource and Action translations. Both catalogs lack 4 Resource labels and 10 Action labels. Only 11 of the 103 permission descriptions have localized descriptions in each language, and the UI does not fall back to the API description field.
- The permission label builder contains a replacement character as the label separator, so permission names can display as a damaged glyph between the resource and action.

## Method, Boundaries, and Evidence

The review read repository instructions and the permission, identity, tenancy, management perspective, administration, and module documentation before inspecting source. Documentation was treated as context where it agreed with the current code; several module catalogue descriptions are older than the current implementation.

Documentation reviewed includes docs/architecture/permission-model.md, SECURITY-ARCHITECTURE.md, multi-tenancy.md, management-perspective-foundation.md, module-catalogue.md, and ADR-008-RISK-WORKFLOW-V1.md; docs/modules/AUTHENTICATION-AND-IDENTITY.md, PLATFORM-SETTINGS.md, PORTFOLIO-MANAGEMENT-CORE.md, PROJECT-MANAGEMENT-CORE.md, PROJECT-ANNUAL-BUDGET-FOUNDATION.md, PROJECT-FINANCE-WORKSPACE.md, FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md, FINANCIAL-TARGET-BUSINESS-MODEL.md, RESOURCE-MANAGEMENT-CORE.md, RESOURCE-CAPACITY-AND-ALLOCATION.md, DOCUMENT-MANAGEMENT-CORE.md, MEETINGS.md, AUDIT-TRAIL.md, and CONFIGURABLE-DASHBOARD-POV.md; plus docs/quality/MASTER-UAT-CHECKLIST.md.

The source inventory covered:

- Permission entities, seed definitions, role-to-permission seed associations, permission policies, identity administration services/controllers, effective permission middleware, management scope resolution, and resource-scope middleware.
- All supported React route declarations, permission constants, Roles & Permissions grouping/labels, route guards, and feature permission checks.
- Module routes and APIs for projects, portfolios/programs, resources/teams, planning/work items, risks, meetings, documents, workflows, status reports, financial workflows, user/role administration, integrations, governance recovery, reporting, plugins, audit, notifications, and account/self-service workspaces.

No database was queried. Seed role mappings below are **source-seeded associations only**. Persisted Development, tenant-specific, user override, and effective runtime grants are **NOT VERIFIED**. A development seed map is not evidence of persisted grants.

## Catalog Source and Role Associations

The definitions are constructed in SeedDataProvider.cs:1054-1157. There is no separate source-controlled Permission Catalog type with category or screen metadata. Permission stores Name, Description, Resource, and Action; the database enforces unique TenantId + Resource + Action in Configurations/PermissionConfiguration.cs:17-18.

SeedDataProvider adds missing permissions by Resource/Action and adds missing associations to existing seeded roles; it does not rebuild existing role grants from scratch (SeedDataProvider.cs:1159-1164, 1192-1227). It also contains a specific soft-delete path for three old StatusReport keys at lines 1229-1244; that is evidence of prior catalog cleanup, not authorization to remove any keys in this audit.

Source-seeded role names are admin, executive, portfolio.manager, project.manager, team.member, pmo.admin, finance.controller, and resource.manager (SeedDataProvider.cs:1166-1189). The admin seed path includes all catalog permissions except the eight explicitly excluded sensitive financial/approval keys (lines 1200-1207). The per-key seed role associations are in the CSV. Runtime grants remain NOT VERIFIED.

The tenant permission identifiers are distinct from platform policies such as PlatformSettings and PlatformTenants, which are registered separately in the API. Do not treat platform policy names as missing tenant catalog keys.

## Screen and View Inventory

The route census is AppRoutes.tsx:115-264. It includes the following actual screen families and route gates. A React route guard controls client navigation and visibility; the API policy or service scope remains the enforcement boundary for data and writes.

| Screen family | Representative routes | Current frontend gate | API/source assessment |
|---|---|---|---|
| Home and dashboards | /home | Authenticated shell; no route permission | Dashboard widgets and data APIs have their own checks; screen-level View permission is absent. |
| Projects | /projects, /projects/:id and nested overview, plan, team, financials, risks/issues, reports, meetings, handover, history, snapshots, tasks, documents, workflows | Parent list/detail requires legacy projects.read; selected tabs add narrower checks | Project APIs use canonical Project.View and operation policies. Frontend legacy route key and backend canonical read key can diverge for custom roles. |
| Project board, Gantt, matrix, templates | /projects/board, /projects/gantt, /projects/matrix, /projects/templates and nested aliases | projects.read plus WorkItem.View, Planning.View, Resource.View, or Document.View | API capabilities are resource-specific; parent project routes retain legacy projects.read. |
| Portfolios and Programs | /portfolios, /portfolios/:id, /portfolios/programs, /portfolios/programs/:id, /portfolios/optimization | Portfolio.View or Program.View | APIs use Portfolio.View/Manage and Program.View/Manage, with tenant management scope and native portfolio/program assignments. Program cross-portfolio exception is described below. |
| Resources and Teams | /resources, vendors, teams, capacity, utilization, cost-utilization | Resource.View or Team.View | API policies alias several Resource actions and Team.Manage into View; the frontend route check is narrower. |
| Meetings | /meetings, /meetings/new, detail/minutes/actions | Meeting.View or Meeting.Create | API policies and meeting actions are separate. Meeting.Manage covers minutes, comments, and work-item links. |
| Work items and planning | /work-items, /work-items/:id, project plan and board/Gantt routes | WorkItem.View / Planning.View, with project parent gate on nested routes | WorkItem and Planning actions are individually represented, but Planning.Manage is broad. |
| Risks and issues | /risks-issues/risks, detail, dashboard, matrix, /risks-issues/issues | Risk.View or WorkItem.View | Risk operations use Risk.*; issue operations are represented through WorkItem.*. |
| Financial management | /costs/payments and project financial/annual-budget/payment-request tabs | Finances.Read or Finances.Write; component controls add specific budget, settlement, and reversal permissions | See action-level map below. Several financial read/write policies intentionally or accidentally alias broader keys. |
| Documents and templates | /documents, project documents, /projects/templates | Document.View | Document.Create/Edit/Archive/TemplateManage policies protect operations. |
| Workflow administration | /workflows, /workflows/:id, versions, project workflow tab | Workflow.View | Workflow.Manage and Workflow.Publish protect draft/publish operations. |
| Project status reports | /projects/:id/status-reports; reporting configuration route | StatusReport.View; configuration uses StatusReport.ManageConfiguration | Create/Edit/Publish/Export are separate keys; route controls do not replace API checks. |
| User directory and roles | /administration/access/users, user permissions, /administration/access/roles | Users.Read or Users.Write | API uses Users.Read/Write and Users.ResetPassword. User and role administration are both controlled by broad users.write. |
| Organization, tenant, reference settings | /administration/organization, tenant-settings, organization-settings, reference-data, currencies, license | Settings.Read | Settings.Write is an API write capability; tenant and organization data boundaries are separately enforced. |
| Platform settings | /administration/settings | No tenant route guard; component calls settings.access and redirects without platform read access | Platform settings use separate platform policies/claims and are not part of the tenant catalog. The AdministrationHub card itself is shown using tenant Settings.Read, which can disagree with platform access. |
| Directory integration | /administration/integrations/directory, /administration/integrations | Any of DirectoryAccessPermissions for directory route; Settings.Read or any Directory key for combined integrations route | API has Directory-specific policies; Directory.Manage aliases several specialized operations. |
| Governance and assignment recovery | /administration/governance and entity-assignment-recovery | Fresh permission check; governance hub permits Settings.Read, Workflow.View, or assignment-recovery permissions | Assignment recovery uses its own permission family. Handover decisions also require assigned actor/resource checks. |
| Reports | /reports and /reports/* | No route permission | ReportController only has [Authorize] on GET endpoints; reports.read is not applied, and no export endpoint was found in that controller. |
| Audit | /security-audit, audit-log, policies, data-classification | No route permission | AuditController uses Audit.View. The route can render before its API is denied. |
| Plugins | /plugins and /administration/plugins | General /plugins has no route gate; administration path uses Settings.Read | PluginController only requires authentication for list/detail GETs; there is no Plugins.View tenant key. |
| Notifications and integrations | /notifications, /integrations | No route permission | Notification APIs are authenticated and user/tenant-scoped. Integration APIs have Settings or Directory policies. |
| AI workspace | /ai-workspace and nested paths | No route permission | AI planning API requires Planning.View/Manage. This is a route/API presentation mismatch, not evidence of an API bypass. |
| Account, stakeholder, and self-service | /account/*, stakeholder invitation/projects, /my-work/* | Authenticated shell; generally no module route permission | Account and assigned-work operations are self-service or assignment-scoped. Verify each API boundary before treating the route as a missing permission. |
| Tickets and test management | /tickets, /test-management | No route permission | Route components are present; this review did not find a dedicated tenant permission family for these screens. Confirm whether these are active business modules before catalog design. |

### Screen/View classification and direct-URL behavior

These classifications are based on route guards and API source, not browser execution. A route guard blocks direct client navigation when its permission predicate fails; an unguarded route can render inside the authenticated application shell, but its API may still reject data access.

| Screen family | Classification | Direct URL behavior from source |
|---|---|---|
| Home and dashboards | INCONSISTENT | Authenticated shell renders the page; widget/data APIs apply separate checks. |
| Project list/detail and nested workspace | INCONSISTENT | Direct route checks projects.read; project APIs use Project.View. |
| Project board/Gantt/resource matrix/templates | INCONSISTENT | Direct route checks legacy projects.read plus the feature key; template route is Document.View only. |
| Portfolio and Program screens | EXISTING | Direct route checks Portfolio.View or Program.View; APIs use corresponding policies and scope. |
| Resource screens | INCONSISTENT | Route requires Resource.View; API Resource.View accepts Resource.Assign, Allocate, or Manage as well. |
| Team screens | INCONSISTENT | Route requires Team.View; API Team.View accepts Team.Manage as well. |
| Meetings | EXISTING | Direct route checks Meeting.View or Meeting.Create; APIs have specific policies. |
| Work items and planning | EXISTING | Direct route checks WorkItem.View or Planning.View; project tabs also inherit the parent project route mismatch. |
| Risks and issues | EXISTING | Direct route checks Risk.View or WorkItem.View; APIs use matching families. |
| Project Financials / Annual Budgets / Payment Requests | INCONSISTENT | Project route checks projects.read; finance component checks Finances.Read/Write and specific operations; APIs require tenant/project scope. |
| Documents and templates | EXISTING | Direct route checks Document.View; APIs use document policies. |
| Workflow administration | EXISTING | Direct route checks Workflow.View; APIs use Workflow.View/Manage/Publish. |
| Project Status Reports | EXISTING | Direct route checks StatusReport.View; configuration route checks ManageConfiguration. |
| User directory and Roles & Permissions | INCONSISTENT | Direct route uses Users.Read/Write; API Users.Read accepts users.write, and all user/role writes share users.write. |
| Tenant/Organization settings | INCONSISTENT | Direct route checks Settings.Read; API Settings.Read accepts settings.write. Platform settings use separate platform policy and are not guarded by a tenant permission. |
| Administration hub | EXISTING | Direct route renders the hub; cards are filtered by tenant permissions, with the platform-settings card currently keyed to tenant Settings.Read. |
| Platform General Settings | INCONSISTENT | Direct URL reaches a component that loads settings.access and redirects when PlatformSettings read access is absent; the hub card gate uses tenant Settings.Read instead. |
| Directory Integration | INCONSISTENT | Direct route accepts any Directory key; API Directory.View also accepts action keys. |
| Governance and Assignment Recovery | EXISTING | Direct route uses a fresh union of governance/action keys; service APIs enforce scopes and assignment/workflow conditions. |
| General Reports | INCONSISTENT | Direct URL renders for an authenticated user; GET API has only [Authorize], while reports.read is unused. |
| Audit Trail | INCONSISTENT | Direct URL renders the page shell; Audit API requires Audit.View. |
| Plugins | MISSING | /plugins direct URL renders for an authenticated user; API list/detail requires authentication only; no tenant Plugins.View key exists. |
| Notifications and Integrations | INCONSISTENT | Direct routes render for authenticated users; integration APIs have Settings/Directory policies, notification APIs use user/tenant scope. |
| AI Workspace | INCONSISTENT | Direct URL renders the page shell; AI planning APIs require Planning.View/Manage. |
| Account, Stakeholder, My Work | EXISTING | Authenticated/self-service or assigned-work routes; verify each object-level API boundary. |
| Tickets and Test Management | MISSING | Direct URL renders an authenticated route; no dedicated current permission family or confirmed API policy was found. Product activity status is unconfirmed. |

## Action Inventory Summary

The 76 current action-level keys and the 27 read/view keys are fully enumerated in PERMISSIONS-CURRENT-INVENTORY.csv. Business operations visible in the current source include, among others:

- Projects: create, edit, delete, archive/reactivate, snapshots, planning, member assignment, change requests, status reports, handovers, documents, meetings, and project workflow operations.
- Portfolios/programs: create, update, archive/reactivate, manage assignments and scopes, and view optimization/rollups.
- Planning/work items: create, edit, delete, lifecycle/baseline changes, assign resources, time entry, issue/risk follow-through.
- Resources/teams: create/edit/deactivate, assign/allocate, rate visibility/edit, team and membership administration, capacity/utilization reads.
- Financial workflows: annual budget create/edit draft/submit/revise/approve; approval plan override; fiscal close prepare/submit/cancel/approve and carry-forward; payment request create/edit/submit/approve/cancel; settlement record; reversal create/update/submit/approve/cancel.
- Governance and collaboration: handover request/cancel/approve/admin-initiate; assignment recovery view/initiate/manage; stakeholder management; meeting create/edit/delete/manage minutes/comments/links.
- Documents/workflows/status reporting: upload/version/archive/template management; workflow draft/publish/retire; status report create/edit/publish/export and configuration.
- Administration/integrations: users, roles, direct user permission changes, password reset, settings/reference data, directory connection tests, sync preview/execute, mappings, provisioning policy.

The listed actions are business operations, not ordinary UI interactions such as sorting, opening a tab, search, or dismissing a dialog. APIs that currently rely on authentication or another broad capability are called out in the gap analysis rather than assigned invented current permissions.

## High-Impact Operation Trace

This trace records the current API method, controller/service workflow, visible control, key/policy, resource boundary, intended actor, and audit evidence for the operations with the greatest access impact. It supplements the one-row-per-key CSV. General per-key route/policy references are in that CSV.

| Screen / action | UI control | API endpoint and method | Application service/workflow | Current key and policy | Scope / actor | Audit evidence |
|---|---|---|---|---|---|---|
| Annual Budget: view page/configuration | Annual budget workspace | GET /api/projects/{projectId}/annual-budgets; GET .../approval-configuration | ProjectAnnualBudgetWorkflow.GetPageAsync / GetApprovalConfigurationAsync | Finances.Read / Finances.Read | Current tenant + project; read-only finance user | Audit not required for read. |
| Annual Budget: create | Create button; ProjectAnnualBudgetWorkspace.tsx:332, 401 | POST /api/projects/{projectId}/annual-budgets | ProjectAnnualBudgetWorkflow.CreateAsync | finances.write through Finances.Write | Current tenant + project; preparer | Workflow uses financial audit entries; exact audit event is in ProjectAnnualBudgetWorkflow.cs. |
| Annual Budget: edit draft | Edit button; ProjectAnnualBudgetWorkspace.tsx:350 | PUT /api/projects/{projectId}/annual-budgets/{itemId} | ProjectAnnualBudgetWorkflow.UpdateDraftAsync | finances.write through Finances.Write | Current tenant + project; draft owner/eligible preparer | Workflow uses financial audit entries. |
| Annual Budget: submit for review | Submit for Review button; ProjectAnnualBudgetWorkspace.tsx:351, 486 | POST /api/projects/{projectId}/annual-budgets/{itemId}/submit | ProjectAnnualBudgetWorkflow.SubmitForReviewAsync | finances.write through Finances.Write | Current tenant + project; eligible preparer; configured approvers | Workflow records SubmittedForReview and approval plan data. |
| Annual Budget: revise approved | Revision button; ProjectAnnualBudgetWorkspace.tsx:352 | POST /api/projects/{projectId}/annual-budgets/{itemId}/revisions | ProjectAnnualBudgetWorkflow.ReviseApprovedAsync | finances.write through Finances.Write | Current tenant + project; eligible preparer | Workflow records RevisionSubmitted. |
| Annual Budget: approve or return for changes | Approve and Return for Changes buttons; ProjectAnnualBudgetWorkspace.tsx:356-361 | POST /api/projects/{projectId}/annual-budgets/{itemId}/review/{requestId}; POST .../{itemId}/approve | ProjectAnnualBudgetWorkflow.DecideReviewAsync / ApproveAsync | ProjectAnnualBudget.Approve and finances.write; Program.cs ProjectAnnualBudgets.Approve requires both | Current tenant + project; active eligible assigned Project Team reviewer; self-approval is prevented | Workflow records Approved and ReturnedForChanges. |
| Annual Budget: customize one submission route | Per-submission override control; ProjectAnnualBudgetWorkspace.tsx:204, 306 | POST .../{itemId}/submit, with override fields | ProjectAnnualBudgetWorkflow.SubmitForReviewAsync / override-plan validation | ProjectApproval.Override is checked in the workflow; submit endpoint also requires Finances.Write | Current tenant + project; preparer with reason; applies to one submission | Workflow captures default/effective approval plan and reason in audit snapshot. |
| Annual Budget: change persistent approval configuration | Configuration editor within annual-budget workspace | PUT /api/projects/{projectId}/annual-budgets/approval-configuration | ProjectAnnualBudgetWorkflow.SaveApprovalConfigurationAsync | Finances.Write only | Current tenant + project; finance writer | Workflow audit evidence exists in the finance workflow; operation remains broad. |
| Fiscal-year closing: prepare/submit/cancel | Prepare form and closing action buttons; ProjectAnnualBudgetYearEndClosing.tsx:214-278 | POST /api/projects/{projectId}/annual-budget-closings; POST .../{closingId}/submit; POST .../{closingId}/cancel | ProjectAnnualBudgetClosingWorkflow.PrepareClosingAsync / SubmitClosingAsync / CancelClosingAsync | ProjectAnnualBudget.Close plus finances.write in API policy | Current tenant + project; preparer; assigned approvers are selected separately | Workflow appends YearEndClosingPrepared and YearEndClosingSubmitted audit events. |
| Fiscal-year closing: approve/reject | Approve and Reject buttons; ProjectAnnualBudgetYearEndClosing.tsx:308-312 | POST /api/projects/{projectId}/annual-budget-closings/{closingId}/decisions | ProjectAnnualBudgetClosingWorkflow.DecideClosingAsync | ProjectAnnualBudget.CloseApprove plus finances.write | Current tenant + project; active assigned eligible approver; preparer cannot approve same closing | Workflow uses the shared approval record and closing audit entries. |
| Payment Request: create/edit/submit/cancel | Request form and row actions; ProjectPaymentRequests.tsx:52-75 and request action controls | POST /api/projects/{projectId}/payment-requests; PUT .../{requestId}; POST .../{requestId}/submit; POST .../{requestId}/cancel | ProjectPaymentRequestWorkflow.CreateAsync / UpdateAsync / SubmitAsync / CancelAsync | Finances.Write | Current tenant + project; finance writer/preparer | Workflow records payment request changes in the project finance audit trail. |
| Payment Request: approve/reject | Decision control on assigned request | POST /api/projects/{projectId}/payment-requests/{requestId}/decisions | ProjectPaymentRequestWorkflow.DecideAsync | Finances.Write; no ProjectPaymentRequest.Approve key | Current tenant + project; active Project Team member with exact assigned approver slot | Workflow validates approval mode/assignment and records decision. |
| Payment settlement: record actual settlement | Settlement form; ProjectPaymentRequests.tsx:131-157 | POST /api/projects/{projectId}/payment-requests/{requestId}/settlements | ProjectPaymentRequestWorkflow.RecordSettlementAsync | PaymentSettlement.Record and finances.write | Current tenant + project/request; finance settlement operator | Workflow records settlement/audit data. |
| Payment reversal: create/update/submit/cancel and approve/reject | Reversal workflow controls in ProjectPaymentRequests.tsx | POST/PUT /api/projects/{projectId}/payment-requests/{requestId}/reversals... | ProjectPaymentRequestWorkflow reversal methods | ProjectPaymentReversal.Create or ProjectPaymentReversal.Approve; both policies also require finances.write | Current tenant + project/request; creator and separately assigned eligible approver | Workflow records reversal decisions and audit. |
| Program: create within assigned Portfolio | Create Program form; ProgramsPage route | POST /api/programs; GET /api/programs/eligible-portfolios | ProgramService.CreateAsync / GetEligiblePortfoliosForCreationAsync | Program.Manage + Portfolio.View; service checks native Portfolio assignment or tenant-admin condition | Same tenant + active Portfolio; Portfolio Manager assignment or current tenant admin | ProgramService also checks missing Program.CreateAcrossPortfolios for expanded scope. |
| User/role administration: create/update user, role, permissions | User forms and role permission checkboxes; RolesPage.tsx:250-277 | POST/PUT /api/users; POST/PUT/DELETE /api/roles; PUT /api/users/{id}/permissions | UserAdministrationService / RoleAdministrationService | Users.Write, implemented by users.write | Same tenant; administrator limited to own effective permissions for delegated role grants | RoleAdministrationService writes audit changes; direct user permission changes use Users.Write. |
| User lookup for people pickers | User/role/resource selectors across the UI | GET /api/users/lookup?role=... | UsersController.GetLookup / UserRepository.GetActiveAsync or GetByRoleNameAsync | Current source requires `Users.View` | Returns ID, full name, email, and linked Resource job title; relies on tenant-scoped repository/DbContext behavior and has no per-workflow or explicit Organization/Project context | Current gate/projection is not an approved policy for all workflow pickers; workflow mapping, scope, and field-minimization decision pending (see F-11). |
| Directory: execute sync or change mappings/provisioning policy | Directory integration controls | POST /api/directory-integrations/connections/{id}/sync; PUT .../field-mappings; PUT .../provisioning-policy | DirectoryIntegrationService / DirectorySynchronizationService | Directory.ExecuteSync, Directory.ManageMappings, Directory.ManageProvisioningPolicy; Directory.Manage aliases several operations | Tenant connection; directory administrator/integration operator | Audit behavior for these endpoint operations was not independently verified in this source pass. |
| General Reports: list/details | Reports page | GET /api/reports and GET /api/reports/{id} | ReportService.GetPagedAsync / GetByIdAsync | [Authorize] only; reports.read is unused here | Tenant query scope must be checked in service; authenticated tenant caller currently passes controller policy | No separate report permission/audit action observed in controller. |
| Plugin metadata: list/details | /plugins page | GET /api/plugins and GET /api/plugins/{id} | PluginRegistry.GetPagedAsync / GetByIdAsync | [Authorize] only; no Plugins.View key | Tenant plugin/read policy needs PO confirmation | No mutating endpoint in this controller. |

The annual-budget approval workspace exposes both Approve and Return for Changes controls under the same current key. This is one reviewed business decision capability with two outcomes, not evidence that each button needs a separate key. Conversely, a distinct approval-only actor is currently blocked by the additional Finances.Write requirement.

DirectoryIntegrationController’s Directory.View policy is also intentionally broad in Program.cs: it accepts the view key and several Directory action keys. The directory route guard likewise accepts any DirectoryAccessPermissions key. This makes action-only directory grants include access to the connection/scope/mapping/provisioning read surfaces; confirm this dependency is intentional.

## Duplicate, Missing, and Orphan Findings

| Finding type | Count | Source-backed result |
|---|---:|---|
| Exact duplicate key definitions | 0 | 103 unique Resource/Action pairs. |
| Semantic overlap clusters | 4 | Legacy project/portfolio/resource keys versus canonical keys; manager.handover versus Handover actions; broad Manage/Write aliases versus fine actions; Projects Delete has an exact display collision with the legacy projects.delete label. |
| Exact duplicate displayed EN permission labels | 1 pair | projects.delete and Project.Delete both render as “Projects [separator] Delete”. |
| Missing catalog entries referenced in implementation | 1 | Program.CreateAcrossPortfolios is checked in ProgramService.cs:70-75 and 274-283, but is not seeded, exposed in Permissions.ts, or registered as a policy. |
| Existing catalog keys omitted from the dynamic Roles & Permissions response | 0 found in source | The API returns active tenant catalog rows held by the acting administrator. This does not prove every tenant database has all 103 rows. |
| Missing UI entry for missing catalog key | 1 | Program.CreateAcrossPortfolios is not a catalog row and cannot be selected in Roles & Permissions. |
| Orphan permission candidates | 9 | projects.write, projects.delete, portfolios.read/write, resources.read/write, reports.read/export, manager.handover. The CSV records these as candidates; preserve until PO decisions and runtime grant inventory. |
| Sensitive actions without an adequately distinct capability | At least 1 | Payment Request decisions use Finances.Write, plus an assigned eligible Project Team approver check; no ProjectPaymentRequest.Approve key exists. |
| Broad capability candidates | 10 | users.write, finances.write, settings.write, Resource.Manage, Team.Manage, Directory.Manage, Planning.Manage, Meeting.Manage, Governance.AssignmentRecovery.Manage, ChangeRequest.Manage. See the target matrix for proposed handling. |

## Key Source Findings

### Roles & Permissions surface

RolesController exposes the permission catalog under Users.Write (RolesController.cs:45-56). RoleAdministrationService loads active tenant permission rows but returns only rows already in the actor’s effective permission set (RoleAdministrationService.cs:58-79); this is a deliberate delegation cap. Role grants are validated against the actor’s effective permissions (RoleAdministrationService.cs:342 onward).

RolesPage groups by raw permission.resource and renders checkboxes (RolesPage.tsx:250-277). It does not distinguish Level 1 views from Level 2 actions, and it does not show inherited management scope or action dependencies. Permission descriptions use only the localized descriptions map; getPermissionDescription does not fall back to PermissionCatalogItemDto.description (permission-labels.ts:18-21).

### Capability is separate from scope

Permission keys identify a capability; they do not encode Tenant, Organization, Portfolio, Program, Project, native manager assignment, or approval assignment. ManagementScopeResolver builds portfolio/program/project scopes from effective-dated assignments and supports explicit current-context resolution. ProjectResourceScopeMiddleware and management context middleware enforce tenant/entity boundaries and read-only view-as behavior.

The resolver also defines TenantAdministratorRoleName = "admin" and treats a matching active system role assignment as tenant administrator (ManagementScopeResolver.cs:13, 122-127). This role-name special case should be removed or replaced with a reviewed explicit capability in a later authorized phase. Do not substitute pmo.admin or another role name.

ProgramService uses native Portfolio Manager assignments for program creation and also checks the missing Program.CreateAcrossPortfolios key (ProgramService.cs:70-95, 264-305). ProgramController requires Program.Manage and Portfolio.View for creation (ProgramController.cs:31-46). The service’s tenant administrator exception is separate from permission capability and must be resolved by the PO.

### Financial approval separation

ProjectAnnualBudgetController uses Finances.Write for create, draft update, submit, revise, and approval-configuration update, while approve uses ProjectAnnualBudgets.Approve (controller lines 41-111). Program.cs requires both finances.write and the dedicated key for annual budget approve/close/close-approve/carry-forward and settlement/reversal actions (Program.cs:180-200).

ProjectPaymentRequestController authorizes create, update, submit, decide, and cancel with Finances.Write (ProjectPaymentRequestController.cs:153-197). ProjectPaymentRequestWorkflow also requires an active assigned Project Team approver with finances.write before deciding (workflow lines 663-700). Assignment and project scope checks are meaningful controls, but the capability is broader than approval and prevents clean approval-only grants.

### Dynamic permissions and session claims

UserPermissionClaimsMiddleware removes permission and role claims carried in a long-lived token and rebuilds effective permission and role claims from current tenant data for each request (UserPermissionClaimsMiddleware.cs:22-73). This reduces stale JWT permission risk. Runtime persistence, tenant role rows, user overrides, and any other caches were not queried.

## Translation and Classification Review

The catalog label builder reads resource and action labels by the exact Resource and Action strings. If a translation is missing, it falls back to the raw source token (permission-labels.ts:10-16). Current gaps in both en.json and ar.json:

- Resource labels missing: Directory, StatusReport, Governance.AssignmentRecovery, Audit.
- Action labels missing: reset-password, Delete, TestConnection, PreviewSync, ExecuteSync, ManageMappings, ManageProvisioningPolicy, Export, ManageConfiguration, Initiate.
- Localized permission descriptions: 11 of 103 keys in each language. Seed descriptions exist for most canonical entries, but the Roles & Permissions UI omits those source descriptions when no matching localized string exists.
- The EN/AR labels use two separate Resource namespaces for old lower-case resources and canonical resources. This creates duplicate group headings such as Projects, Portfolios, and Resources even when the displayed resource names are equal.
- The permission display separator in permission-labels.ts:25 is a replacement character, not a normal separator.

Proposed professional EN/AR translations for missing labels are in the CSV. They are display recommendations only; they do not change keys.

## Source List

- Repository instructions: AGENTS.md.
- Documentation: docs/architecture/permission-model.md; docs/architecture/SECURITY-ARCHITECTURE.md; docs/architecture/multi-tenancy.md; docs/architecture/management-perspective-foundation.md; docs/architecture/module-catalogue.md; docs/architecture/ADR-008-RISK-WORKFLOW-V1.md; docs/modules/AUTHENTICATION-AND-IDENTITY.md; PLATFORM-SETTINGS.md; PORTFOLIO-MANAGEMENT-CORE.md; PROJECT-MANAGEMENT-CORE.md; PROJECT-ANNUAL-BUDGET-FOUNDATION.md; PROJECT-FINANCE-WORKSPACE.md; FINANCIAL-MANAGEMENT-PLANNED-PAYMENTS.md; FINANCIAL-TARGET-BUSINESS-MODEL.md; RESOURCE-MANAGEMENT-CORE.md; RESOURCE-CAPACITY-AND-ALLOCATION.md; DOCUMENT-MANAGEMENT-CORE.md; MEETINGS.md; AUDIT-TRAIL.md; CONFIGURABLE-DASHBOARD-POV.md; docs/quality/MASTER-UAT-CHECKLIST.md.
- Permission/identity source: apps/api/src/NEXUS.Modules.Identity/Domain/Permission.cs; apps/api/src/NEXUS.Persistence/SeedDataProvider.cs; apps/api/src/NEXUS.Persistence/Configurations/PermissionConfiguration.cs; apps/api/src/NEXUS.Modules.Identity/Infrastructure/RoleAdministrationService.cs; apps/api/src/NEXUS.Modules.Identity/Infrastructure/UserPermissionClaimsMiddleware.cs; apps/api/src/NEXUS.Modules.Identity/Api/RolesController.cs; apps/api/src/NEXUS.Modules.Identity/Api/UsersController.cs.
- Authorization and scope: apps/api/src/NEXUS.Api/Program.cs; apps/api/src/NEXUS.Persistence/Management/ManagementScopeResolver.cs; apps/api/src/NEXUS.Api/Management/ManagementContextMiddleware.cs; apps/api/src/NEXUS.Api/Management/ProjectResourceScopeMiddleware.cs; apps/api/src/NEXUS.Modules.Portfolio/Application/ProgramService.cs; apps/api/src/NEXUS.Modules.Portfolio/Api/ProgramController.cs.
- Frontend and labels: apps/web-react/src/routes/AppRoutes.tsx; apps/web-react/src/lib/permissions.ts; apps/web-react/src/features/administration/RolesPage.tsx; apps/web-react/src/features/administration/permission-labels.ts; apps/web-react/src/i18n/messages/en.json; apps/web-react/src/i18n/messages/ar.json.
- Financial workflows: apps/api/src/NEXUS.Api/Finance/ProjectAnnualBudgetController.cs; ProjectAnnualBudgetWorkflow.cs; ProjectAnnualBudgetClosingController.cs; ProjectPaymentRequestController.cs; ProjectPaymentRequestWorkflow.cs; ProjectPaymentReversalWorkflow.cs.
- Other API examples: apps/api/src/NEXUS.Modules.Reporting/Api/ReportController.cs; apps/api/src/NEXUS.Modules.PluginManagement/Api/PluginController.cs; apps/api/src/NEXUS.Modules.Audit/Api/AuditController.cs; apps/api/src/NEXUS.Modules.Integrations/Api/DirectoryIntegrationController.cs; apps/api/src/NEXUS.Modules.Meetings/Api/MeetingController.cs; apps/api/src/NEXUS.Modules.Documents/Api/DocumentController.cs.
