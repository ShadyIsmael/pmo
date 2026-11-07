# Permission Gaps and Risk Analysis

**Audit date:** 2026-11-01  
**Status:** Audit only; Product Owner review required  
**Implementation changes:** None made

## Executive Summary

The source catalog contains 103 unique permission keys, no exact duplicate key definitions, 27 View/read capabilities, and 76 action capabilities. The supported React router contains 113 rendered route declarations. The permission roles and labels are generated dynamically from tenant permission rows, but the catalog has no stored Module or Screen classification and the Roles & Permissions editor groups by raw Resource.

The most urgent architecture findings are:

1. **Critical — role-name authorization:** ManagementScopeResolver treats the persisted system role named admin as a tenant administrator and grants broad management scope. This is a hard-coded role-name authorization branch.
2. **High — approval too broad:** Payment Request decisions require finances.write and an approver assignment. There is no ProjectPaymentRequest.Approve key. Annual Budget and closing approvals have dedicated keys, but their API policies also require finances.write.
3. **High — cross-portfolio key is missing:** ProgramService checks Program.CreateAcrossPortfolios, but the key cannot be seeded, assigned in Roles & Permissions, or enforced by a registered policy.
4. **High — report read is not permission-gated:** ReportController GET endpoints require authentication only, although reports.read exists. The /reports screen has no view route gate.
5. **Medium — route/API capability mismatches:** Some front-end routes require exact View/Read keys while API policies accept broader action keys; project routes use projects.read while project APIs use Project.View. Reports, audit, plugins, and AI routes also differ from their API read policies.
6. **Medium — broad action grants:** users.write, finances.write, settings.write, Resource.Manage, Team.Manage, Directory.Manage, Planning.Manage, Meeting.Manage, Governance.AssignmentRecovery.Manage, and ChangeRequest.Manage each cover multiple operations or alias narrower capabilities.
7. **Medium — labels and grouping:** Four resource labels and ten action labels are missing from both localization catalogs; 92 permission descriptions lack localized UI entries; identical permission text appears under separate raw-resource groups; the display separator is a replacement character.

No database was accessed. Existing persisted tenant grants, role memberships, user overrides, and runtime effective permissions are **NOT VERIFIED**. The seed role map must not be treated as runtime authorization evidence.

## Prioritized Findings

### F-01 — Tenant administrator scope is authorized by role name

**Severity:** Critical  
**Classification:** Authorization scope bypass / role-name dependency

ManagementScopeResolver.cs:13 declares TenantAdministratorRoleName = "admin". IsTenantAdministratorAsync checks an active UserRole assignment with that exact role name at lines 122-127. ManagementContextMiddleware uses that result for management context resolution, and ProgramService uses it to admit tenant-wide program creation.

This is authorization by a hard-coded role name. It bypasses the rule that capability and data scope remain independent and that administrative authority must not be implicitly granted by a role label. The source-seeded admin role and pmo.admin role are not equivalent: their permission mappings differ, and the scope check is tied only to admin.

**Target:** Replace this branch only in a later approved phase with an explicit, tenant-scoped administrative scope capability and a compatibility plan. Do not substitute pmo.admin or another role name. Keep native Portfolio/Program/Project assignments and tenant boundaries.

### F-02 — Payment Request decisions share Finances.Write with preparation

**Severity:** High  
**Classification:** Missing operation permission / overly broad

ProjectPaymentRequestController.cs:153-197 protects create, update, submit, decide, and cancel with Finances.Write. ProjectPaymentRequestWorkflow.cs:663-700 checks that a decider is an active assigned approver; lines 591 and 688 also require active Project Team eligibility and finances.write. The record is tenant/project scoped, and workflow ordering is checked.

The assignment and resource checks are valuable, but the same broad capability covers both preparation and approval. A user who must only approve assigned requests cannot be granted a dedicated permission, while a financial writer who is assigned gains approval capability.

**Target:** Add a proposed ProjectPaymentRequest.Approve operation key only after Product Owner approval. Keep exact assigned-approver, active Project Team, tenant, project, and workflow-state checks. Do not grant it to every finance writer.

### F-03 — Annual Budget approval and closing reviewers require broad financial write

**Severity:** High  
**Classification:** Overly broad dependency / least-privilege conflict

ProjectAnnualBudgetController.cs uses Finances.Write for create, draft update, submit, revise, and approval-configuration update. The approve endpoints use ProjectAnnualBudgets.Approve. Program.cs:180-200 requires both finances.write and the specific budget, closing, settlement, or reversal permission. Closing decisions use ProjectAnnualBudgets.CloseApprove.

This separation is incomplete: the specific action key does not permit an approval-only user to act without the broad finance writer grant. ProjectPaymentRequestController has the same issue for payment request decisions.

**Target:** Decide which preparation, configuration, revision, approval, close, and carry-forward actions require independent capabilities. Remove the Finances.Write conjunction from approval-only policies only after policy approval and assignment/scope review. Preserve self-approval prevention, approver assignment, and audit.

### F-04 — Program.CreateAcrossPortfolios is referenced but absent from the catalog

**Severity:** High  
**Classification:** Missing catalog and UI entry; incomplete capability configuration

ProgramService.cs:70-75 and 274-283 checks the literal Program.CreateAcrossPortfolios and uses it to expand the eligible Portfolio set within the current tenant. No matching permission definition exists in SeedDataProvider.cs:1054-1157; no current Permissions constant or Program.cs policy registers it. ProgramController.cs:31-46 requires Program.Manage and Portfolio.View on create and eligible-Portfolio endpoints.

The current code therefore contains an unassignable exception. With no Portfolio Manager assignment, ordinary callers receive no eligible portfolios unless they meet the separate tenant-administrator role-name branch. A user cannot be granted the cross-Portfolio capability through Roles & Permissions.

**Target:** Product Owner to decide whether the exception is supported. If approved, register an explicit key and policy with tenant boundary and active Portfolio validation. Do not infer this grant from Program.Manage or a role name.

### F-05 — General Reports read does not use reports.read

**Severity:** High  
**Classification:** Missing enforcement / orphaned read key

ReportController.cs has class-level [Authorize] and only GET routes; it does not require reports.read. The /reports and /reports/* routes in AppRoutes.tsx:249-250 have no permission gate. reports.read is present in seed data and role mappings but no active feature authorization use was found. reports.export has no matching endpoint in ReportController.

This currently makes report definition listing/detail available to any authenticated caller admitted by surrounding tenant/query controls, while the configured reports.read grant has no effect. Tenant filtering may still constrain the returned rows, but it does not replace an explicit read capability.

**Target:** If general Reports is an active module, apply the existing reports.read key to the read API and route, label it Reports.View, and verify query scope. Confirm whether export exists elsewhere before keeping reports.export.

### F-06 — Frontend route checks and backend policies disagree

**Severity:** Medium  
**Classification:** Inconsistent screen/API enforcement

| Screen/capability | Frontend behavior | API behavior | Risk/effect |
|---|---|---|---|
| Project list/detail | Requires legacy projects.read (AppRoutes.tsx:123, 129) | Project endpoints use Project.View | Custom roles can have API access but be blocked by the route, or enter the route without the canonical API read grant. |
| Resource reads | Routes use exact Resource.View | Resource.View API policy accepts Resource.View, Resource.Assign, Resource.Allocate, or Resource.Manage (Program.cs:141-143) | Assignment-only users can call read API but cannot see the route. |
| Team reads | Route uses exact Team.View | Team.View API policy also accepts Team.Manage (Program.cs:150) | Team.Manage-only users can call API but may be blocked by the route. |
| User directory reads | Frontend checks Users.Read | Users.Read API policy accepts users.read or users.write (Program.cs:155) | Write-only user administrators can call API but may be hidden from UI. |
| Settings reads | Frontend checks Settings.Read | Settings.Read API accepts settings.read or settings.write (Program.cs:158) | Write-only settings administrators can call API but may be hidden from UI. |
| Financial reads | Frontend requires Finances.Read | Finances.Read API accepts finances.read or finances.write (Program.cs:178) | Write-only finance users can call API but may be hidden from UI. |
| Audit | Route subtree has no gate | AuditController uses Audit.View | The page can render before an API call is denied. |
| AI workspace | Route subtree has no gate | AI planning API uses Planning.View/Manage | Route visibility does not match feature policy. |
| Plugins | /plugins route has no gate | PluginController GET endpoints require authentication only | No dedicated tenant View capability exists; whether this is acceptable is a PO decision. |
| Directory integrations | Route accepts any DirectoryAccessPermissions member | API has Directory-specific view/action policies | The broad route gate is suitable for the feature shell only if each sensitive control and API remains action-gated. |
| Directory read surfaces | Directory route accepts any Directory action | Directory.View policy accepts Directory.View plus action capabilities; DirectoryIntegrationController read endpoints use Directory.View | An action-only grant currently includes connection, scope, mapping, provisioning, and sync-history reads. Confirm that this implied View dependency is intended. |

UI checks are navigation and presentation controls; backend policies, service checks, and scope validation must remain authoritative.

### F-07 — Broad keys grant independent operations

**Severity:** Medium to High  
**Classification:** Overly broad capability candidates

| Key | Current reach observed in source | Suggested review |
|---|---|---|
| users.write | User create/update, role CRUD and role permissions, direct user permission overrides (UsersController.cs; RolesController.cs) | Separate user lifecycle, role administration, and direct permission administration if different administrators are intended. |
| finances.write | Budget, revision, commitment, annual-budget, payment-request, and other financial writes; also a prerequisite for selected sensitive operations | Decide between finance-preparer scope and separate operation keys. |
| settings.write | Tenant/organization/reference configuration and API policies such as WorkItemStatus.Manage and VendorCategory.Manage | Separate configuration families if their owners differ. |
| Resource.Manage | Umbrella alias for view, assignment, create/edit/delete, and cost-rate policies (Program.cs:141-149) | Show full effective reach; reserve for resource administrators. |
| Team.Manage | Alias for Team.View/Create/Edit/Delete (Program.cs:150-154) | Show as an explicit umbrella permission or retire after reviewed mapping. |
| Directory.Manage | Alias for test, preview, execute, mappings, and provisioning policy operations (Program.cs:160-177) | Keep execution and identity-provisioning actions independently grantable. |
| Planning.Manage | Broad planning-data management alongside separate Planning.Lifecycle | Decide whether lifecycle/baseline authority should remain isolated. |
| Meeting.Manage | Minutes, comments, and work-item links; Meeting.Edit separately edits meeting/attendees | Confirm this operation bundle matches product roles. |
| Governance.AssignmentRecovery.Manage | Reconcile governance references, assign sponsors, and initiate administrative handovers (seed description) | Keep distinct from native management assignments and Handover approval. |
| ChangeRequest.Manage | Create, update, and cancel; ChangeRequest.Approve is separate | Acceptable only if preparer operations are intended to be bundled. |

This list identifies review candidates, not a mandate to create one key per endpoint. Existing umbrella grants must not be broadened.

### F-11 — User picker lookup lacks workflow-specific authorization and projection

**Severity:** Medium review  
**Classification:** Partial remediation; no new permission is introduced

The shared `UsersController.GetLookup` still requires `Users.View` and returns user ID, full name, email, and linked Resource job title. That endpoint remains unsuitable as a cross-workflow permission shortcut.

Project workflows now have a separate `GET /api/users/workflow-lookups/projects` endpoint. Its `WorkflowLookup.Project.Users` API policy accepts the current effective `Project.View`, `Project.Create`, or `Project.Edit` permission and returns only ID/full name. Its optional role filter controls candidate eligibility only; it is not authorization. The current Project references use analogous workflow policies and limited projections. No `Users.Lookup` permission, legacy alias, role-name authorization, or additional role grant was added.

Remaining picker consumers have not yet been comprehensively mapped across Meetings, Finance, Resources, Handover, and other workflows. For each, the implementation must:

1. Bind the lookup to the exact canonical operation and any existing eligibility or assignment permissions; do not make a lookup permission a substitute for authorization of the eventual write.
2. Derive Tenant plus applicable Organization/Project/resource scope on the server and filter candidates before serialization.
3. Use a purpose-scoped route or projection with only fields required by that form; retain the full listing endpoint's current View policy.
4. Verify empty-cache first request, exact Allows, unauthorized callers, direct Active Deny during an active session, cross-scope isolation, and serialized field minimization.

The Product Owner rule establishes that an operation permission should authorize the lookup needed by that operation; it does not approve any broader grant or relax data scope. Workflow policies and projection contract tests now cover Project, Program edit Portfolio, Portfolio/Program manager, Meeting, Payment Obligation, Resource, Work Item, Risk, and Handover lookup routes. Other Finance screens remain unmapped; active-user eligibility is not yet verified at Organization/resource scope. Live cold-cache, session Deny, cross-scope, and full business-operation UAT remain unverified. Keep this gap open until remaining workflows and runtime scenarios are covered; see [Workflow Lookup Authorization](WORKFLOW-LOOKUP-AUTHORIZATION.md) and the PPL scenarios in the Master UAT Checklist.

### F-08 — Legacy keys overlap canonical capabilities and are likely orphaned

**Severity:** Medium  
**Classification:** Semantic duplicate / orphan candidate

The lower-case legacy resources coexist with canonical resources: projects.read and Project.View; portfolios.read and Portfolio.View; resources.read and Resource.View. manager.handover overlaps with Handover.Manage and Handover.Approve. The distinct keys must not be merged by label alone because stored role and user assignments are keyed by permission records.

Nine keys had no active permission-check use outside seed/constants/localization after exact token review: projects.write, projects.delete, portfolios.read, portfolios.write, resources.read, resources.write, reports.read, reports.export, manager.handover. projects.delete and Project.Delete also create the only exact duplicate EN permission display string. Reports keys need product resolution because reports.read should likely protect the active read API.

### F-09 — Permission grouping and translations are incomplete

**Severity:** Medium  
**Classification:** Wrong grouping / inaccurate or missing display metadata

The Permission entity has no Module/Screen category. RolesPage groups by exact raw Resource string, so lower-case projects and canonical Project appear as separate groups even though both translate to Projects. There are four missing Resource translations and ten missing Action translations in both language catalogs. The generic fallback exposes raw machine tokens such as ExecuteSync or ManageProvisioningPolicy.

Only 11 permission descriptions exist in each localized description map. The API DTO includes a description, but permission-labels.ts returns an empty string if a localized description key is absent. The visible permission delimiter in permission-labels.ts:25 is a replacement character.

Corrected bilingual label proposals are recorded in the CSV and target matrix. No translation files were changed.

### F-10 — Current permission descriptions and key/action terminology are uneven

**Severity:** Low to Medium  
**Classification:** Ambiguous display description

The seed catalog uses View, Read, Manage, Write, AdminInitiate, Approve, CloseApprove, CarryForward, Override, Publish, and other actions, while the UI displays generic Resource + Action tokens. Several descriptions describe a bundle (Resource.Manage, Meeting.Manage, Governance.AssignmentRecovery.Manage); others have no source description (legacy keys). The action labels should name the business effect, not flatten distinct actions into “Manage”.

Use natural bilingual labels from the target matrix. In particular, distinguish approve, review/decide, override, administer, submit, and view. Do not translate distinct approval and operation permissions with the same generic Arabic label.

## Duplicate and Catalog Checks

| Check | Result | Detail |
|---|---:|---|
| Unique source permission keys | 103 | Seed Resource/Action pairs. |
| Exact duplicate key definitions | 0 | No duplicate Resource/Action pair in the source seed. |
| Exact duplicate EN display label pairs | 1 | projects.delete and Project.Delete. |
| Semantic overlap clusters | 4 | Legacy/canonical pairs; manager.handover/Handover actions; umbrella/bounded capabilities; duplicate Projects/Delete display. |
| Missing catalog keys referenced in source | 1 | Program.CreateAcrossPortfolios. |
| Existing catalog keys with no Roles & Permissions surface | 0 found | Role editor receives the active catalog dynamically, but only keys held by the acting administrator are assignable. |
| Orphan candidates | 9 | Listed in F-08; no runtime grants were inspected. |
| Permission keys missing from frontend constant file | 1 | manager.handover; it remains dynamically displayable in the role editor but is unused by active feature controls. |
| View/read keys | 27 | Includes own/team timesheet read capabilities. |
| Action keys | 76 | All existing catalog entries not classified as View. |

## Scope Assessment

| Scope | Current evidence | Assessment |
|---|---|---|
| Platform | PlatformSettings and PlatformTenants use separate platform claims/policies in the API | Keep separate from tenant role grants. |
| Tenant | Tenant context middleware, user/tenant claim validation, tenant-specific permission resolution | Tenant boundary is a separate dimension from operation permissions. |
| Organization | Organization settings and user grants check active organization scope | Keep explicit organization relationship checks. |
| Portfolio/Program/Project | ManagementScopeResolver resolves native management assignments and inherited resource contexts; ProjectResourceScopeMiddleware validates project ownership and child relationships | Retain the resolved scope checks for every operation. |
| Native Manager assignments | Resolver derives portfolio/program/project contexts from assignment data and validates selected perspective | Do not infer scope from the permission name or role category. |
| Approval-specific assignments | Payment request and annual closing workflows require active, eligible, assigned approvers and workflow state | Preserve assignment checks even if new approval capabilities are added. |
| Program creation across portfolios | ProgramService currently supports a missing explicit capability and a hard-coded admin scope exception | Requires an explicit PO decision and an authorization model with tenant boundary. |
| Read-only view-as | Management context middleware rejects writes in effective read-only mode | Preserve this guard independently of role permissions. |

## Existing Grants and Migration Risks

- Permission records are tenant-scoped and keyed by Resource/Action. RolePermissions and user permission overrides may be tied to permission IDs. A rename can make old rows appear orphaned or create duplicate effective capabilities.
- Seed behavior is additive for permissions and existing role associations. It is not a role reset; obsolete StatusReport keys have a specific soft-delete path. Future key migration must not reuse that cleanup mechanism without a reviewed grant map.
- Tenant-specific custom roles may not match the source-seeded role map. Their actual grants were not queried.
- Effective permission claims are recalculated by UserPermissionClaimsMiddleware from current data on requests; it removes permission claims embedded in long-lived JWTs. Any other permission caches must be identified before migration.
- Direct user overrides are separately represented by user permission override records and must be included in migration analysis.
- Historical audit records may contain old key strings or IDs. Preserve those values and record explicit old-to-new mapping.
- Splitting a broad key may reduce access if old grants are not mapped; mapping one broad permission to every new action can instead expand access. Require tenant-by-tenant review and explicit approval of each mapping.
- Do not delete, merge, reset, or rewrite a permission or role in this audit phase.

## Product Owner Decisions

1. Confirm whether Program.CreateAcrossPortfolios is a product capability and define eligible grant holders.
2. Replace the hard-coded admin scope rule with a reviewed capability model in a later phase.
3. Approve a distinct ProjectPaymentRequest.Approve permission and whether annual-budget approvers can act without Finances.Write.
4. Decide annual-budget create, edit-draft, submit, revise-approved, persistent approval-configuration, and one-submission route-override boundaries.
5. Confirm Report and Plugin view audiences and whether reports.export is an active operation.
6. Approve treatment of legacy keys and the nine orphan candidates after runtime grants are inventoried.
7. Confirm whether AI Workspace should use Planning.View or a dedicated View key.
8. Decide how independent action grants interact with screen View and reviewer inbox access.
9. Approve module/screen grouping, label wording, Arabic translations, missing descriptions, and sensitive permission markers.
10. Approve a compatibility window, additive mapping, tenant grant reconciliation, audit plan, and retirement criteria before any future key migration.

## Audit Limits

This phase is source-backed and read-only. It did not query persisted Development or tenant role grants, apply migrations, run builds/tests, or perform browser UAT. Therefore runtime roles, actual user permissions, policy execution against a database, and route behavior under live identities are not verified. The deliverables identify these as review/migration inputs rather than assumed facts.
