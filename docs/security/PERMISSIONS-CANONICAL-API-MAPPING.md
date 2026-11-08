# Permission-to-Route and API Mapping

This mapping describes the current canonical source. The only supported frontend is `apps/web-react`. Route guards control page access and affordances; API policies and service checks remain authoritative. Tenant, Organization, project/resource scope, ownership, and workflow assignment are separate authorization conditions.

The source catalog contains 171 active keys (46 View, 125 Action). These are source counts only; they do not represent persisted Permission rows or role grants. The permission audit matrix preserves its existing row IDs and source references and is updated in place.

## Read routes and tenant-facing registries

| Surface | Route/UI permission | API/service enforcement | Scope |
|---|---|---|---|
| Projects | `Project.View` | Project read endpoints require the same key. | Authenticated Tenant plus assigned project/management scope. |
| Issues | `Issue.View` | Class-level API policy; Issue service fails closed without a valid tenant and assigned project scope. | Current Tenant and assigned projects; soft-deleted rows excluded. |
| Plugin registry | `Plugin.View` | Class-level API policy; registry list/detail filters to current Tenant and excludes deleted rows. | Current Tenant; no platform-wide authority. |
| AI Planning | `Planning.View` | Read policy remains separate from plan editing/lifecycle actions. | Tenant and project management scope. |
| Audit | `Audit.View` | Audit APIs apply the same permission and tenant audit boundary. | Current Tenant; sensitive financial audit details require `ProjectFinancialAuditTrail.View`. |
| Tickets and test management | `WorkItem.View` | Work Item API policies enforce reads; operation keys protect mutations. | Tenant and resource/project assignment scope. |
| Reports | `Report.View` | Report read APIs apply the matching read permission and data scope. | Tenant and authorized management scope. |
| Dashboard | `Dashboard.View` | Home API policy; each widget/data API has its own permission and scope. | Tenant and resource/project scope. |
| Integrations | `Settings.View` | Generic settings route is read-gated; each integration operation has its own API policy. | Current Tenant. |
| Directory integration | `Directory.View` | Connection, mapping, provisioning, test, preview, and execute operations each have distinct policies. Connection create/update/scope specifically require `Directory.ManageConnection`. | Current Tenant; each Action requires `Directory.View`. |

## Project and resource operations

- Project creation, edit, and delete use `Project.Create`, `Project.Edit`, and `Project.Delete` separately.
- Optional draft Annual Budget creation during project setup requires `ProjectAnnualBudget.Create` in addition to project creation. It does not authorize approval or change the lifetime financial ceiling.
- Resource actions use `Resource.Create`, `Resource.Edit`, `Resource.Delete`, `Resource.Assign`, and `Resource.Allocate`. Cost information uses `Resource.ViewCostRate`, `Resource.EditCostRate`, and `Resource.ManageRates`.
- Team operations use `Team.View`, `Team.Create`, `Team.Edit`, and `Team.Delete`. Stakeholder links use `Stakeholders.Read`/`Stakeholders.Manage` and retain project/tenant checks.
- Portfolio and Program use separate View/Create/Edit/Archive/Reactivate keys. `Program.CreateAcrossPortfolios` is Organization-scoped and additionally requires normal `Program.Create` and `Portfolio.View`; it is excluded from the protected tenant Admin baseline.
- Project Plan creation uses `ProjectPlan.Create`, while editing existing plan contents uses `Planning.Edit`; both require `Planning.View` and authorized Project scope. The standalone blank Plan form requires Create only. MPP import into an existing Plan requires Edit; MPP import creating a Plan requires both Create and Edit. `ProjectPlan.Create` is Management-category only; `Planning.Edit` allows Management and Operation categories. Neither is in the protected Admin baseline or automatically granted to existing roles.
- `ProjectPlan.Create` and `Planning.Edit` have an explicit first-grant path only for a same-Tenant custom role, requiring both `Roles.ConfigurePermissions` and `TenantAdministration.ManageScope`. Active Deny, dependencies, role category, and Tenant ownership remain enforced; bootstrap delegation cannot configure system roles.
- Other Action dependencies are enforced by `PermissionCatalog.EnforceDependencies` at effective-permission resolution.

## Users and role delegation

| Operation | Canonical permission |
|---|---|
| Read user/access records | `Users.View` |
| Create user or edit profile | `Users.ManageAccounts` |
| Reset local password | `Users.ResetPassword` |
| Activate/deactivate account | `Users.SetStatus` |
| Assign/remove user roles | `Users.AssignRoles` |
| Manage direct Allow/Deny overrides | `UserPermissions.Manage` |
| Read, create, edit, configure, or delete tenant roles | `Roles.View`, `Roles.Create`, `Roles.EditDetails`, `Roles.ConfigurePermissions`, `Roles.Delete` |

Every mutation is tenant-scoped and bounded by actor delegation authority, with only the explicit bootstrap first-grant exception described above. Role names do not grant permissions or scope. Configuration requires active, dependency-valid keys; sensitive grants cannot exceed the actor's effective authority. The protected Admin baseline is locked and derives from current eligible catalog entries.

The shared `GET /api/users/lookup` route remains `Users.View`-only and returns its existing user selector projection. Dedicated workflow routes now authorize Project, Portfolio/Program manager, Program Portfolio edit, Meeting, Work Item, Risk, Resource, and Handover user options by their canonical operation or assignment permission and return minimal projections. Project, Meeting, Program, and Payment Obligation reference routes likewise return purpose-specific fields; full listing endpoints retain their existing View policies. No blanket `Users.Lookup` key or role grant was added. Other Finance screens and narrower Organization/Project/resource candidate eligibility remain open; see F-11 and [Workflow Lookup Authorization](WORKFLOW-LOOKUP-AUTHORIZATION.md).

## Settings and reference data

- Tenant/Organization settings read uses `Settings.View`; supported tenant-configuration writes use `Settings.ManageTenantConfiguration` with the applicable Organization/Tenant checks.
- Reference-data reads use `ReferenceData.View`. Create/Edit/Delete each require `ReferenceData.Create`, `ReferenceData.Edit`, or `ReferenceData.Delete`, along with tenant ownership and immutable/reference-integrity checks.
- No Import/Export permission is active because no supported matching operation is present.

## Financial workflows

| Workspace/action | Canonical permission boundary |
|---|---|
| Finance shell | `ProjectFinance.View` and `ProjectFinance.Manage`; shell access does not authorize child data or actions. |
| Annual Budget reads/drafts | `ProjectAnnualBudget.View`, `Create`, `EditDraft`, `SubmitForReview`; approved revision/configuration use `ReviseApproved` and `ManageApprovalConfiguration`. |
| Annual Budget decisions | `ProjectAnnualBudget.Approve`, `Close`, `CloseApprove`, and `CarryForward` remain distinct. Assigned-review APIs provide only the exact assigned record. |
| Payment Requests | `ProjectPaymentRequest.View`, `Create`, `EditDraft`, `Submit`, and `Cancel`; approval uses `ProjectPaymentRequest.Approve`. |
| Settlements | `ProjectSettlement.View` reads; `PaymentSettlement.Record` records settlement. |
| Reversals | `ProjectPaymentReversal.Create` and `ProjectPaymentReversal.Approve` are separate. |
| Fiscal Year Closing | `ProjectAnnualBudgetClosing.View`; approval/close/carry-forward actions use their dedicated Annual Budget keys. |
| Financial activity audit | `ProjectFinancialAuditTrail.View`; generic `Audit.View` alone does not expose these details. |
| Budget revisions | `BudgetRevision.View` and `BudgetRevision.Approve`; project scope, workflow state, maker/checker, and allocation validation remain. |
| Lifetime ceiling changes | `ChangeRequest.Approve` plus `ProjectFinancialCeiling.ApproveChange` at the final assigned approval step. No ordinary Project edit path changes the ceiling. |

No broad finance permission authorizes approval, settlement recording, reversal approval, closing, carry-forward, approval override, or ceiling change.

## Status report export

The API endpoint `GET /api/projects/{projectId}/status-reports/{reportId}/export` requires `StatusReport.Export`, enforces existing project/report scope, and returns only Published reports. React PDF, Excel, and print actions first fetch this authorized response. The export permission is enforced server-side as well as in the UI.

## Persisted compatibility and database status

Retired aliases are absent from the active catalog and do not create effective permissions. The Development-only `--rebuild-canonical-permissions` operation archives retired permission rows and their role/direct-override links; it does not fan aliases into new grants or change active canonical role/user grants. It requires explicit target identity, no model changes or pending migrations, and a copy-only checksum backup on a different volume/share that passes `RESTORE VERIFYONLY`.

The operation was not run. Current Development identity, migration state, physical schema, persisted grant inventory, and backup are not verified in this task. No schema migration or database write was made.
