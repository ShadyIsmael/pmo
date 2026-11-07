# Workflow Lookup Authorization

## Design rule

Lookup authorization follows the canonical business operation, not a blanket user, organization, or administration View permission. A workflow lookup policy is an API policy composition, not a catalog permission: it accepts the effective permission for the operation that consumes the lookup and, where useful, the existing entity View permission. It does not create aliases, grant roles, bypass authorization middleware, or authorize the subsequent business mutation.

General entity listing endpoints keep their existing View authorization and full response contract. When a workflow operation needs only reference data, use a separately authorized projection or purpose-specific route. Return only eligible candidates within the server-derived Tenant and applicable Organization, Project, resource, or assignment scope.

Authenticated API requests pass through `UserPermissionClaimsMiddleware`, which removes permission snapshots and resolves current effective permissions before endpoint authorization. This ensures first-page-load requests with no application cache are authorized from the source of truth and that a current direct Deny takes effect during an existing session.

## Implemented mappings

| Workflow lookup | Lookup policy | Effective capability | Projection |
|---|---|---|---|
| Project Portfolio options | `WorkflowLookup.Project.Portfolios` | `Project.Create` / `Project.Edit`, or `Portfolio.View` | Portfolio ID, name, currency; tenant/management-scope filtered |
| Project Program options | `WorkflowLookup.Project.Programs` | `Project.Create` / `Project.Edit`, or `Program.View` | Program ID, name, Portfolio ID, currency; tenant/management-scope filtered |
| Project Organizational Unit options | `WorkflowLookup.Project.OrganizationalUnits` | `Project.Create` / `Project.Edit`, or `Settings.View` | Unit ID and name; active tenant units |
| Project Currency options | `WorkflowLookup.Project.Currencies` | `Project.Create` / `Project.Edit`, or `ReferenceData.View` | ID, code, name, symbol, base flag; no exchange rate |
| Project user options | `WorkflowLookup.Project.Users` | `Project.Create` / `Project.Edit` / `Project.View` | Generic user projection remains active same-tenant users. The dedicated `GET /api/users/workflow-lookups/projects/managers` route additionally requires an active Management-category role and effective `Handover.Approve`. |
| Program creation Portfolio options | `WorkflowLookup.ProgramCreate.Portfolios` | `Program.Create` only | Active Portfolio options from the server-derived Program creation scope |
| Program edit Portfolio options | `WorkflowLookup.ProgramEdit.Portfolios` | `Program.Edit` only | Active Portfolio options in the server-derived native Portfolio assignment scope |
| Portfolio manager candidates | `WorkflowLookup.Portfolio.Managers` | `Portfolio.Create` / `Portfolio.Edit` / `Handover.AdminInitiate` | Active same-tenant users with an active Management-category role and effective `Handover.Approve` plus `Portfolio.View`; ID/full name |
| Program manager candidates | `WorkflowLookup.Program.Managers` | `Program.Create` / `Program.Edit` / `Handover.AdminInitiate` | Active same-tenant users with an active Management-category role and effective `Program.View` plus `Handover.Approve`; ID/full name |
| Meeting user options | `WorkflowLookup.Meeting.Users` | `Meeting.Create` / `Meeting.Edit` / `Meeting.Manage`, or `Meeting.View` | User ID and full name |
| Meeting Project options | `WorkflowLookup.Meeting.Projects` | `Meeting.Create` / `Meeting.Edit`, or `Meeting.View` | Project ID, public ID, name, code; accessible project references |
| Work Item user options | `WorkflowLookup.WorkItem.Users` | `WorkItem.Create` / `WorkItem.Edit`, or `WorkItem.View` | User ID and full name |
| Risk user options | `WorkflowLookup.Risk.Users` | `Risk.Create` / `Risk.Edit`, or `Risk.View` | User ID and full name |
| Resource user options | `WorkflowLookup.Resource.Users` | `Resource.Create` / `Resource.Edit`, or `Resource.View` | User ID and full name |
| Resource Organizational Unit options | `WorkflowLookup.Resource.OrganizationalUnits` | `Resource.Create` / `Resource.Edit`, or `Settings.View` | Unit ID and name |
| Business Currency options | `WorkflowLookup.Business.Currencies` | Portfolio/Program/Resource operations, resource cost-rate View/Edit, or `ReferenceData.View` | ID, code, name, symbol, base flag; no exchange rate |
| Handover manager candidates | `WorkflowLookup.Handover.ManagerCandidates` | `Handover.AdminInitiate` | Active same-tenant users with an active Management-category role and effective `Handover.Approve`; ID/full name |
| Payment Obligation Project options | `WorkflowLookup.PaymentObligation.Projects` | `PaymentObligation.View` / `PaymentObligation.Create` / `PaymentObligation.Edit` | Project ID, public ID, name, code, currency; tenant/management-scope filtered |

The generic `GET /api/users/lookup` remains protected by `Users.View` and retains its existing full-selector projection. Create Project uses `GET /api/users/workflow-lookups/projects`, which returns the generic active same-tenant user projection and does not accept a role-name filter, email, or job title. Project Manager selection uses the separate `GET /api/users/workflow-lookups/projects/managers` route, which returns only active same-tenant users with an active Management-category role and effective `Handover.Approve`. Lookup policies and operation keys are not written into the permission catalog and require no role migration or grant change.

## Scope and rollout requirements

Tenant filters are necessary but are not a substitute for the actor's target-entity scope. User candidate queries explicitly constrain active, non-deleted System Users to the authenticated Tenant. Manager routes also apply their approved eligibility and current effective permission rules: Project Manager requires an active Management-category role and `Handover.Approve`; Portfolio Manager additionally requires `Portfolio.View`; Program Manager additionally requires `Program.View`. The consuming create or handover service revalidates candidate eligibility, including the Management category at incoming acceptance; lookup results are not authority. The actor's operation permission and management context independently constrain which target entity may be created or assigned.

Program manager candidate selection does not consult Resource records or Project Team membership. There is no general User-to-Organization membership relationship in the current model. `OrganizationSettingsGrant` grants configuration access only, and Resource organizational-unit membership must not be used to infer a User's eligibility for a management appointment. Manager candidates are therefore Tenant-scoped; Organization scope is enforced on the actor and target entity where the operation supports it. A narrower candidate Organization-affiliation rule requires an approved User-Organization model and policy. Project team membership and Resource Allocation continue to use Resource records.

The Project, Portfolio/Program manager, Program edit, Meeting, Payment Obligation, Resource, Work Item, Risk, and Handover lookup routes now use workflow-specific policies and projections. Existing full-list endpoints retain their View policies. Project creation and advanced-search manager selectors call the dedicated manager-candidate route and no longer send a manager role-name filter. Program edit Portfolio candidates are independently authorized by `Program.Edit` and filtered to the same native Portfolio assignment scope used by the server-side update operation. A `Resource.Create`-only user can reach the creation form without loading or exposing the Resource directory. A `Resource.Edit`-only user still cannot select an existing Resource from this page: the current detail/list endpoints remain `Resource.View`-only, and no purpose-scoped edit candidate/detail flow exists yet. Do not weaken those endpoints; add a minimal, scope-filtered edit workflow before claiming edit-only page access. Other Finance screens and any remaining workflow consumers need an explicit inventory. Reuse this policy/projection pattern; do not add a blanket lookup permission or change role grants.

## Verification

Automated coverage checks lookup policy allow/deny behavior, endpoint-policy bindings, role-filtered manager routes, and minimal user/project projections. The request middleware rebuilds effective claims so explicit Deny remains authoritative. Focused React tests exercise first-load lookup requests and operation-only Program/Payment workflows. Browser cold-cache behavior, active-session Deny, complete business-operation flows, and cross-Tenant/Organization/resource scenarios remain unverified in [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md).
