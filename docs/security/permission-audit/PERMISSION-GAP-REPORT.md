# Permission gap report

**Evidence date:** 2026-11-04. This report retains the earlier audit findings and records current P0 financial-approval source, database-gate, and API-health evidence. Only the focused closure scope below was rechecked; prior broad audit rows are not implicitly revalidated.

## Current code-defined catalog

`PermissionCatalog.cs` currently contains 160 permission entries (43 View and 117 Action); this replaces the stale 158/42/116 counts in the prior audit. This includes `ProjectFinancialFoundation.View`, `CostUtilization.Export`, `ProgramFinancialRollup.View`, and `PortfolioFinancialRollup.View`; no combined `ProjectFinancialRollup.View` exists. These source counts do not describe database rows, role grants, direct overrides, or effective permissions. The protected tenant Admin baseline assertion also needs reconciliation: the previous test expected 48 while the current source calculation returned 47. The source delta from the 133-key audit baseline and the brief's unsupported 136-key figure remain historical audit items, not revalidated here.

## Findings closed in source

### Issue reads require `Issue.View`

`IssueController` now applies class-level `[Authorize(Policy = "Issue.View")]`. `IssueService` fails closed when management scope is unavailable, the tenant is empty, or the user has no assigned project scope; its list and detail queries remain tenant-, project-, and deletion-scoped.

**Evidence:** `apps/api/src/NEXUS.Modules.IssueManagement/Api/IssueController.cs`; `apps/api/src/NEXUS.Modules.IssueManagement/Application/IssueService.cs`; catalog entry and API policy in `PermissionCatalog.cs` and `Program.cs`.

### Plugin registry reads require `Plugin.View`

`PluginController` now applies class-level `[Authorize(Policy = "Plugin.View")]`. The registry query requires the current tenant and excludes deleted rows. This source classification is tenant-facing; it does not grant platform-wide plugin administration.

**Evidence:** `apps/api/src/NEXUS.Modules.PluginManagement/Api/PluginController.cs`; `apps/api/src/NEXUS.Modules.PluginManagement/Application/PluginRegistryService.cs`; catalog entry and policy in `PermissionCatalog.cs` and `Program.cs`.

### Route guards use canonical View permissions

Project pages use `Project.View`; AI workspace uses `Planning.View`; audit pages use `Audit.View`; tickets and test management use `WorkItem.View`; generic integrations use `Settings.View`; and plugin pages use `Plugin.View`. Routes and navigation no longer depend on the retired lower-case aliases.

**Evidence:** `apps/web-react/src/routes/AppRoutes.tsx`; `apps/web-react/src/lib/navigation.ts`; `apps/web-react/src/lib/permissions.ts`.

### Project setup uses the annual-budget creation capability

Optional draft budget creation is independently checked with `ProjectAnnualBudget.Create` in the API and React flow. It remains a Draft and retains project, fiscal-year, and financial-ceiling validation.

**Evidence:** project service and project creation UI source; `ProjectAnnualBudget.Create` policy in `Program.cs`.

### Project Financial Overview has a dedicated read-only capability

`GET /api/projects/{projectId}/financial-foundation` requires `ProjectFinancialFoundation.View` and checks Tenant/Project scope. Its catalog entry is a Project-scoped View with no write, approval, or export authority. The matrix now includes the capability and mapped API/UI surface.

**Evidence:** `ProjectFinancialFoundationController.Get`; `PermissionCatalog.cs`; `Program.cs`; React project financial API and route gates.

### Cost Utilization separates report reads from export

`GET /api/resource-analytics/cost-utilization` uses `Resource.View`; actual cost-rate fields are redacted unless `Resource.ViewCostRate` is effective. Export now uses `GET /api/resource-analytics/cost-utilization/export-data`, which requires both `Resource.View` and `CostUtilization.Export`, then returns the same scoped DTO with cost-rate redaction preserved. The client formats the authorized response into PDF/XLSX. The previous client-generated flow had no server-side export-specific authorization; the dedicated route closes that source-level gap. No role or protected Admin grant was added.

**Evidence:** `CostUtilizationAnalyticsController`; `FinancialPermissionPolicyRegistration`; `CostUtilizationAnalyticsService`; React export-data query and export sheet; focused authorization/scope test source added but not executed.

### Program and Portfolio financial rollups use distinct scopes

Program rollups require `Program.View` and `ProgramFinancialRollup.View` (Program scope); Portfolio rollups require `Portfolio.View` and `PortfolioFinancialRollup.View` (Portfolio scope). Controller policies and the service check both permissions, tenant and native parent scope, and filter descendant Projects. The matrix records both routes independently; `ProjectFinance.View` remains shell-only.

**Evidence:** Program and Portfolio management controllers; `ProgramPortfolioFinancialRollupService`; catalog and centralized policy registration; dedicated policy/scope test source added but not executed.

### Financial approval defaults use a narrow first-grant path

`ProjectAnnualBudget.View` and `ProjectAnnualBudget.ManageApprovalConfiguration` are cataloged as Project-scoped. A shared bootstrap delegation policy makes only the approved View/Manage and existing low-risk financial read/export keys initially assignable when the actor has effective `Roles.ConfigurePermissions` and `TenantAdministration.ManageScope`. It honors Active Deny, allows a custom role or a different same-tenant user, rejects self-assignment and new grants to system roles, and preserves existing keys. It does not add Annual Budget approval (`ProjectAnnualBudget.Approve`) or one-time override (`ProjectApproval.Override`) to the bootstrap allowlist or Admin baseline.

**Live Development state:** the protected `tenant.admin` role holds both prerequisite permissions, and one active member has them without direct Deny. The database has three `ProjectAnnualBudget.View` role grants, no `ProjectAnnualBudget.ManageApprovalConfiguration` role grants, and no direct Allow/Deny users for either key. `ProjectAnnualBudget.Approve` and `ProjectApproval.Override` each have zero role grants and zero direct Allow/Deny users. No grants were changed. Therefore an eligible approver cannot currently be selected in this Development data, and live Annual Budget approval defaults cannot be saved until the authorized tenant-scoped grant path is exercised for an explicitly approved recipient.

**Verification:** the targeted `ProjectSettingsAuthorizationTests`, `FinancialPermissionDelegationTests`, and `FinancialApprovalDelegationBoundaryTests` run passed **30/30**, covering Allow, Active Deny, tenant/project scope, settings save/reload/inheritance with ordered mandatory approvers and immutable snapshot, and authorized one-time override behavior. This uses isolated test fixtures, including EF InMemory; it does not establish live SQL Server persistence. The generic User Permission Administration focused suite also passed 30/30 but is separate evidence.

### Cost Utilization management scope fails closed when empty

The service returns an empty scoped report without querying source lookups when no Project is in management scope. A caller-specified Project outside that scope is resolved through `CanAccessProjectAsync` and rejected before source reads when unauthorized. Regression test source covers empty scope, rejected ProjectId, and resolver-approved ProjectId; these tests were not executed in this update.

**Evidence:** `CostUtilizationAnalyticsService`; `CostUtilizationManagementScopeTests.cs`.

### Status report export is checked by the API

The report export GET endpoint requires `StatusReport.Export`, verifies project/report scope through the existing service path, and permits export only for Published reports. The React PDF, Excel, and print flow obtains this authorized report response before generating output.

**Evidence:** `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs`; `apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx`.

### Stakeholder management checks are enforced at controller and service boundaries

Stakeholder list reads require `Stakeholders.Read`; mutations and invitation administration require `Stakeholders.Manage`. Services validate the fresh per-request permission claims, actor identity, tenant, project management scope, and read-only state. Token-based invitation validation/acceptance/decline/consent withdrawal are documented public self-service exceptions and must validate the one-time invitation token and relationship state.

**Evidence:** `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectStakeholderController.cs`; `StakeholderInvitationController.cs`; `ProjectStakeholderService.cs`; `StakeholderInvitationService.cs`; `UserPermissionClaimsMiddleware.cs`.

### User and role administration service entrypoints are tenant-bound

User listing and role listing require fresh `Users.View` and `Roles.View` permissions inside their services, in addition to controller policies. User and role mutation services bind the supplied actor and tenant identifiers to the authenticated request principal and reload effective permissions from tenant-scoped persisted assignments. Direct service entrypoints without a matching authenticated actor and tenant fail closed. Role updates accept only active catalog permissions the actor can delegate; protected Admin and last-effective-Admin invariants are checked inside the tenant-scoped mutation transaction.

**Evidence:** `UserAdministrationService.cs`; `RoleAdministrationService.cs`; `UsersController.cs`; `RolesController.cs`; `TenantAdminMutationGuard.cs`; service-entry tests in `UserPermissionAdministrationTests.cs`. Tests have not yet been executed.

## Remaining release blockers

### Persisted permission and Development verification

The repository EF gate's read-only `inspect -NoBuild` verified the actual Development target as SQL Server `Shady`, database `NexusPmo` (database ID 5). The model is synchronized, all 10 source migrations are applied, none are pending, `20261101114229_AddProjectAnnualBudgetApprovalConfiguration` is applied, and the physical schema matches the model. No migration, seed, reset, or permission-data write was performed. The gate marked backup as not needed for this read-only inspection; no independent backup was created. Do not reset or recreate this database as part of this closure.

### Initial grant/delegation path for new financial keys

The original blanket first-grant gap is closed in source for `ProjectAnnualBudget.View` and `ProjectAnnualBudget.ManageApprovalConfiguration` through the narrow policy described above. This policy deliberately does not bootstrap `ProjectAnnualBudget.Approve` or `ProjectApproval.Override`. The Development database has no approver or override grants; their initial assignment must follow existing authorized permission administration, with an explicitly selected same-tenant target and no automatic Admin/Project Manager grant. The live UAT still needs an approved approver recipient and an authenticated session.

### Full end-to-end action coverage

The raw handler inventory remains a candidate list, not a semantic business-action denominator. Coverage is not computed. The matrix retains its existing row IDs and source evidence and adds the canonical catalog entries introduced by the rebuild; remaining UI/API/service/scope scenarios require consolidated UAT.

## Positive controls observed in source

- Active Action dependencies are enforced when effective permissions are resolved. Missing dependencies do not add grants.
- Role delegation is bounded by the actor's effective permissions and tenant/resource scope.
- Ordinary startup seeding does not rewrite existing role grants.
- Financial approval, settlement, reversal, closing, carry-forward, and ceiling-change permissions remain separate.
- Protected Admin baseline derivation excludes Organization, Platform, CrossTenant, and SystemOwner scope.

## Verification status

This focused P0 closure rechecked the API build, React settings suite, health endpoint, and live Development database gate. The broader audit's historical checks were not rerun.

| Check | Status |
|---|---|
| Catalog entries and API policy key comparison | 160 source catalog entries (43 View, 117 Action); automated catalog/policy comparison not rerun. The Admin baseline assertion discrepancy (48 expected / 47 source result) remains open. |
| Focused React settings tests and localization | **20/20 passed** with 15-second timeout; EN/AR locale JSON parsed. `npm run typecheck` ran over five minutes without output and was stopped. |
| Focused generic permission administration tests | **30/30 passed**; does not include the financial delegation boundary or project authorization test classes. |
| Financial/API authorization tests | **30/30 passed** for the targeted P0 test classes. The broader architecture filter ran 52 tests: 49 passed and 3 failed on catalog count (158 expected/160 actual), a `Resource.ViewCostRate` screen-view dependency assertion, and Admin baseline count (48 expected/47 actual). |
| API source build | **Passed**, zero warnings/errors after the current code changes. |
| Development API health | **Passed**, `http://127.0.0.1:5000/health` returned `200 Healthy` with `--skip-development-database-initialization`; this did not exercise authenticated project APIs or SQL persistence. |
| EF model synchronization and Development identity | **Passed.** Model synchronized; target verified as `Shady` / `NexusPmo`, database ID 5. |
| Migration state | **10 applied, 0 pending.** `20261101114229_AddProjectAnnualBudgetApprovalConfiguration` is applied. No migrations applied in this closure. |
| Backup | **Not created; not needed for this read-only gate.** No database write was attempted. |
| Physical schema | **Passed.** No missing or extra model objects reported by the gate. |
| Persisted permission rows and direct overrides | **Read-only inspected.** View: 3 role grants; Manage: 0; Approve: 0; Override: 0; direct Allow/Deny rows for View/Manage/Approve/Override: 0. Existing grants were not changed. |
| Authenticated settings save/reload and browser UAT | **Not run.** No authenticated session or eligible `ProjectAnnualBudget.Approve` user is available in the verified Development data. |

Source-level regression tests for financial delegation boundaries, Active Deny, cross-tenant scope, settings inheritance, and override authorization ran successfully in the targeted filter. The three general architecture assertions above remain unresolved and were not altered as part of this P0 scope.

No database migration, reset, or permission-data write was performed. Current status is **SOURCE AND TARGETED TESTS VERIFIED; LIVE FINANCIAL CONFIGURATION CLOSURE BLOCKED** until an explicitly authorized approver grant and authenticated Development save/reload/submission UAT are available. The required database recreation/reset is neither necessary nor authorized.
