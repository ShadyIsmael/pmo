# Authorization Gap Remediation Plan — Phase 02 Proposal

**Status: Design only.** The plan is ordered for a future, explicitly approved Phase 03+. No code, tests, build, UAT, role grant, database, or migration work was performed in this Phase 02 review.

## Prioritized remediation backlog

| Priority | Gap / affected capability | Current source behavior | Proposed correction | Dependencies / acceptance evidence |
|---|---|---|---|---|
| P0 | Reports View enforcement | reports.read exists in the catalog and role seed map. General GET /api/reports and /api/reports/{id} have [Authorize] only; /reports routes have no View gate. | Apply reports.read consistently as Reports.View to route and read APIs. Validate tenant and organization filters in ReportService. Direct URL and direct API without the key must deny. | PO confirms Reports is active; query-scope review; positive/negative/cross-tenant scenarios; no report data exposed before auth. |
| P0 | Financial approval separation | Budget approve/return, fiscal close, settlement, reversal, and carry-forward policies require finances.write plus a dedicated key. Payment Request approval uses finances.write plus assignment; there is no ProjectPaymentRequest.Approve key. | Add distinct Payment Request approval capability. Make screen read access separate. Remove generic financial-write prerequisite for actions that have their own dedicated capability after PO approval. Keep project scope, assignment, active membership, approval eligibility, separation of duties, audit, and no self-approval. | PO financial separation decision; reviewer-only access design; migration grant review; API policy parity. |
| P0 | Program cross-Portfolio permission catalog gap | ProgramService checks literal Program.CreateAcrossPortfolios, but no catalog key/policy/UI entry exists. ProgramController requires Program.Manage and Portfolio.View; service determines assigned vs exceptional Portfolio eligibility. | Register proposed Program.CreateAcrossPortfolios under Programs. Require Program.Create as base operation. Native assignment is normal; the explicit key expands only eligible target Portfolio capability within tenant and organization scope. | PO grants the design; remove role-name shortcut; prove tenant/org boundaries and negative cases before rollout. |
| P0 | Tenant administrator scope derives from role name | ManagementScopeResolver checks system role Name == "admin" to determine tenant administrator scope. | Replace role-name matching with an explicit, trusted, tenant-scoped administrative authority/capability from configurable identity policy. Keep system platform authority separate; do not treat pmo.admin as system admin. | PO policy decision; identity and tenant boundary design; no implicit elevation. |
| P1 | Broad User and Settings write permissions | users.write covers users, role definitions, role assignment, and direct permission overrides. settings.write covers tenant settings and reference-data operations. | Split into the proposed user/role/override and tenant-settings/reference-data actions. Preserve delegation caps and audit. | Endpoint inventory; source consumer mapping; manual per-role grants. |
| P1 | Program.Manage and Portfolio.Manage collapse different lifecycle actions | Source controllers use one Manage key for create, update, archive, and reactivate. | Replace the single gate with Create/Edit/Archive/Reactivate keys. Preserve management assignments and tenant/org scope. | PO agrees the separation is useful; role-by-role grant mapping; archive/reactivate lifecycle policy. |
| P1 | View/action and screen/API inconsistencies | Some policies accept write keys as read aliases; some routes are unguarded or use different keys from APIs. | Apply the two-level contract. Require screen View and the operation key for UI action paths; align API reads/writes. Keep scope and workflow assignment as separate guards. | Dependency policy approval; update roles form validation; route/API parity matrix. |
| P1 | Role-administration privilege concentration | Roles & Permissions is gated by users.write. Source service limits the actor to assigning permissions already held. | Split account management, role definition, user-role assignment, and direct permission overrides. Preserve the existing delegation cap and audit. | Confirm UI/API endpoint separation; privilege escalation scenarios. |
| P2 | Umbrella aliases and lowercase legacy keys | Legacy project/portfolio/resource keys and broad Manage aliases coexist with canonical actions and sometimes policy aliases. | Keep aliases during compatibility; mark deprecated and remove only after role, user, client, dynamic, and historical reference review. | Persisted grant inventory; external-client inventory; telemetry or compatibility window; rollback plan. |
| P2 | Labels, grouping, and Arabic localization | Damaged label delimiter; incomplete translations/descriptions; grouping by Resource duplicates categories. | Store Module/Screen/Level metadata in authoritative catalog; apply proposed bilingual labels/descriptions; make each key visible once. | Data model and UI approval; EN/AR proofreading; backward-compatible catalog DTO. |
| P2 | Plugins route/API access contract | Current inventory finds an authenticated-only Plugins surface without Plugins.View; no tenant permission family exists. | PO decides tenant-facing vs platform-only. If tenant-facing, add Plugins.View and align route/API; if internal platform surface, require platform policy instead. | Product classification first; do not add either permission automatically. |

## Required authorization acceptance scenarios for later implementation

These are planned verification scenarios; they were not run during Phase 02.

1. User with a View key can read only records inside tenant, organization, and authorized management scope; direct route and API agree.
2. User with an Action but without the parent View cannot create an invalid role grant; runtime denies the inconsistent set.
3. Annual-budget preparer can create/edit/submit but cannot approve, override approval route, close fiscal year, carry forward, settle, or reverse.
4. Annual-budget reviewer with View + Approve but no financial edit keys can open assigned review records, approve/return only an assigned eligible item, and cannot list unrelated budgets or self-approve.
5. Payment-request reviewer with View + ProjectPaymentRequest.Approve but no edit keys can see only assigned requests and approve/reject only their assigned step.
6. Settlement recorder cannot approve Payment Requests or reversals. Reversal requester cannot approve their own reversal.
7. Project/Program/Portfolio operation keys do not grant records outside native assignments. Program.CreateAcrossPortfolios expands the eligible Portfolio set only inside tenant/organization bounds and still requires Program.Create.
8. Tenant administrator capability is tenant-scoped; pmo.admin does not become a platform/system administrator. A tenant user cannot access Platform Settings or another tenant.
9. Role editor cannot assign any permission the acting administrator does not hold; role changes, direct overrides, and user-role assignments create audit evidence.
10. Reports route and API deny without reports.read and return only tenant/org-authorized data.

## Implementation dependency order (after approval)

1. Record PO decisions for key additions, splits, scope, dependency behavior, and legacy mappings.
2. Confirm source call sites and role/runtime grant data read-only; inventory direct overrides and external clients.
3. Define canonical catalog metadata and localized names/descriptions without changing existing persisted keys.
4. Implement backend policy and service checks first, including tenant/org/resource/workflow scope.
5. Implement route and control checks to match the backend contract; update Roles & Permissions metadata and dependencies.
6. Rehearse additive compatibility mapping in an isolated environment; compare effective access before/after per role and tenant.
7. Roll out with a reviewed role-by-role grant ledger and rollback point; deprecate old keys only after all consumers and grants are reconciled.
8. Run security-focused automated scenarios and UAT only in the later approved implementation phase.

## Canonical frontend/backend mapping proposal

| Surface | Current frontend check | Current backend check | Classification | Proposed canonical contract |
|---|---|---|---|---|
| Projects | projects.read route key in navigation/direct route checks. | Project.View policy on project APIs. | Legacy compatibility alias plus mismatched key. | Project.View is canonical Level 1. Keep projects.read as a temporary alias only until all route/API consumers and grants are reviewed; then merge/deprecate it. |
| Resources | Resource.* component checks; some screens accept Resource.Manage. | Resource.View policy also accepts write/action aliases such as Assign/Allocate/Manage in current policy configuration. | Broad read alias and umbrella action. | Require Resource.View for reads and the specific existing action key for mutations; View dependency is explicit in role configuration. Deprecate Resource.Manage after manual mapping. |
| Teams | Team page uses Team.View/Manage. | Team.View policy accepts Team.Manage in current setup. | Broad read alias. | Team.View gates reads; Team.Create/Edit/Delete gate their respective operations. Remove Manage-as-View compatibility only after role/client review. |
| Users and roles | Routes and RolesPage use users.read/users.write. | Users.Read currently accepts users.write; user/role mutations share Users.Write. | Write-to-read alias and overly broad administration gate. | users.read is Level 1; split account, role definition, role assignment, direct override, and password reset. Action grants do not bypass screen View. |
| Tenant/organization settings | Settings.Read/Write checks. | Settings.Read currently accepts settings.write; Platform Settings uses separate platform access policies. | Write-to-read alias; tenant/platform boundary must remain explicit. | settings.read gates tenant settings screen; split tenant configuration from reference data. Platform Settings remain platform-only. |
| Financial screens | Finances.Read/Write on the project finance tab; specific buttons combine Finance.Write with action keys. | Finances.Read accepts finances.write; sensitive policies combine finance write with operation-specific keys. | Write-to-read alias; sensitive action unnecessarily depends on broad edit; no Payment Request approval key. | Keep finances.read for overview/residual finance reads; add per-screen View keys for budgets, requests, and closing; use narrow operation keys and assigned-record read filters. |
| Reports | /reports route has no permission guard. | reports.read exists, but ReportController GET actions require only [Authorize]. | Missing route and backend resource authorization. | Apply reports.read to route, list, and detail. Confirm tenant/org query scope. |
| Audit | Audit route can render before API denial. | Audit.View is enforced by the audit API. | Missing frontend guard; backend permission exists. | Use Audit.View on route and API. |
| Directory integration | Directory route currently accepts any DirectoryAccessPermissions key; combined integrations route accepts Settings.Read or Directory keys. | Directory policies include broad Directory.Manage aliases for specific operations. | Broad compatibility aliases and route/API mismatch. | Directory.View is required for screen reads; exact existing action keys gate test, preview, sync, mapping, and provisioning operations. Keep each key once under Directory Integration. |
| AI workspace | /ai-workspace route has no route guard. | AI planning API uses Planning.View/Manage. | Missing route guard; API is guarded but screen contract is not aligned. | Use Planning.View at route and match API read policy; keep AI planning operations under the existing Planning/WorkItem actions as the source-specific operation map confirms. |
| Plugins | Current route/API use authentication without a tenant Plugins.View key. | No Plugins.View policy or catalog family found in Phase 01. | Missing View if tenant-facing; otherwise wrong authorization plane. | PO classifies as tenant module or platform operator surface before adding a key. Tenant module requires Plugins.View on route and APIs; platform surface requires platform policy. |
| Platform Settings / Administration hub | Platform Settings card visibility currently consults tenant Settings.Read, while the target page has platform access checks. | /api/settings/access and platform read/write policies use platform authority. | Incorrect frontend grouping/predicate; backend plane is separate. | Filter card and direct route using platform authority. Never infer platform permission from tenant Settings.Read or role name. |

These mappings correct the contract; they do not rename current keys or change source in Phase 02. The CSV retains the exact Phase 01 frontend/backend evidence per existing permission.


## Home/Dashboard screen View proposal

The /home route currently has no screen-level View key, while dashboard widgets and their data APIs use their own checks. Add Dashboard.View for screen entry and keep every widget API's permission and tenant/organization/resource scope checks. The key is a proposed addition pending PO approval and receives no automatic role grant.

Program cross-Portfolio target enforcement also needs an explicit organization-boundary check. Phase 01 source evidence shows the current ProgramService exceptional query filters active Portfolios by TenantId; the PO target rule requires eligible Organization as well as Tenant scope. Do not ship the new capability until that boundary is resolved and verified.

## Project lifetime financial ceiling approval gap

ProjectService rejects direct Budget updates and requires an approved Project Change Request. The current change-request approval policy is ChangeRequest.Approve; the service applies BudgetChangeAmount when the CR is approved. Proposed correction: preserve ChangeRequest.Manage for proposing a CR, require ChangeRequest.Approve for the decision, and additionally require ProjectFinancialCeiling.ApproveChange when that CR changes the lifetime ceiling. Keep workflow assignment, project/tenant/organization scope, no-self-approval, and audit checks. Do not create a separate request key unless the PO chooses to restrict who may propose a ceiling change; the CR request itself does not change the ceiling.

Add one authorization scenario: Project.Edit and ChangeRequest.Manage may not directly change the approved lifetime ceiling; CRs may propose a BudgetChangeAmount, but only an assigned reviewer with ChangeRequest.Approve and ProjectFinancialCeiling.ApproveChange may approve it. Preserve ceiling checks and financial audit events.
