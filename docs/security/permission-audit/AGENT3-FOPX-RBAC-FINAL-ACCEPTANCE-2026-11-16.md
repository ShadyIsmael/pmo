# AGENT 3 — FOPX PM RBAC Consolidation and Database Cleanup

**Status: PARTIAL — Database Migration Pending; final acceptance is not met.**

## Delivered in source

- Organization membership is derived from active, same-tenant Organization-owned Role assignments. `/api/auth/me` uses the same source and omits inactive or deleted Organizations. The legacy `UserOrganization` entity/table mapping was removed from the current model; no new permission keys or role grants were introduced.
- Collection GETs marked for Project, Portfolio, and Program apply Organization-specific effective View permissions and row scope. Active Organization and tenant checks are required; Active Deny is honored; a request without an authorized Organization scope returns no Organization-scoped collection rows. Unmarked endpoints and mutations are not changed by this collection middleware.
- Role permission catalog delegation flags now account for protected Admin baseline and Organization-role grant eligibility. Direct `Allow` overrides are rejected for every `ProtectedTenantAdminPolicy.MandatoryPermissionKeys` entry, including unflagged Administration-module core permissions. Direct Deny remains handled separately.
- Added RBAC-ORG-01 through RBAC-ORG-06 to the Master UAT Checklist. These cases cover Role-derived membership, inactive Organizations, Project/Portfolio/Program collection scope, explicit Organization IDs, tenant-level View behavior, and the legacy table data gate.
- Removed the malformed, undiscoverable migration and replaced it with discoverable migration `20261116200535_RemoveUserOrganizations`, including its designer and snapshot update. Its destructive `Down` path recreates the table structure but cannot restore removed records or creation metadata.

No Development data was modified. No seed or reset was run.

## Verification evidence

| Check | Result |
|---|---|
| API test-project build | Passed after the final source changes: 0 errors, 32 warnings. Output was isolated under `%LOCALAPPDATA%\Temp\pmo-rbac-validation-20261116`. |
| Focused RBAC/tenant-isolation VSTest filter | 96 passed, 15 failed, 0 skipped, 111 total. TRX: `%LOCALAPPDATA%\Temp\pmo-rbac-validation-20261116\rbac-focused-final-2.trx`. The added direct-override regression now passes. |
| Frontend production build | Not verified: `tsc -b` completed and Vite transformed 3,005 modules, then both attempts failed while Windows denied cleanup of temporary `esbuild-*` directories. No frontend source was changed for this task. |
| Browser UAT / authenticated API UAT | Not run. |
| SQL Server persistence tests | Not run. |
| Independent security review | Reviewer confirmed Organization collection filtering, Active Organization checks, and scope boundaries. The reviewer then identified unflagged Admin Core direct-Allow coverage; the source guard and regression assertion were expanded to the entire mandatory Admin Core. Final review of that last expansion is pending. |

### Remaining focused test failures

Five `ManagementScopeResolverTests` fail before their assertions because the shared SQLite fixture fails with a foreign-key violation while saving at `ManagementScopeResolverTests.cs:108`:

- `PerspectiveSnapshot_UnionsDirectManagerContextsWithInheritedPortfolioScope`
- `Direct_department_project_assignment_contributes_its_organization_to_scope`
- `SponsorAssignment_GrantsOnlyItsTenantProjectScopeWithoutAResourceMembership`
- `Organization_role_project_view_exposes_only_projects_owned_by_its_organization`
- `Project_context_inherits_organization_from_program_portfolio_when_project_portfolio_is_null`

Ten `UserPermissionAdministrationTests` remain failing and need disposition before the suite can serve as acceptance evidence: `Role_management_is_tenant_scoped_and_keeps_administration_grants_actor_scoped`, `Tenant_administrator_scope_does_not_allow_role_creation_details_deletion_or_cross_tenant_access`, `User_status_change_cannot_remove_the_last_effective_tenant_admin`, `Role_administrator_can_edit_custom_role_with_ordinary_permissions_outside_actor_access`, `Role_detail_excludes_permissions_from_other_tenants_for_protected_admin`, `Ordinary_scoped_permissions_are_delegable_without_manage_tenant_scope_and_foreign_ids_are_rejected`, `Protected_tenant_admin_baseline_permissions_outside_administration_cannot_be_delegated_to_custom_roles`, `Role_permission_catalog_keeps_ordinary_scope_delegable_and_administration_actor_scoped`, `Budget_revision_actions_can_be_granted_saved_reloaded_and_revoked_across_role_categories`, and `Role_api_rejects_new_inactive_grants_and_removes_existing_legacy_grants_on_reconciliation`.

Several are assertion/status mismatches (including expected 403 vs 400 and expected 409 vs 403); the five SQLite fixture failures are setup failures. Do not treat the suite as passed or infer these failures are only test defects without resolving them.

## Development database and migration gate

The documented read-only Development gate verified the active API endpoint and live database identity as **Development / SQL Server `Shady` / database `NexusPmo` / database ID `5`**, using Windows Authentication. The EF model/snapshot check passed. The repository has 31 migrations; 30 are applied and one is pending: `20261116200535_RemoveUserOrganizations`.

The currently applied physical schema has no missing objects against its applied migration history. `UserOrganizations` is an extra legacy table expected to be removed by the pending migration. A read-only live inventory found two active rows; both match current Role-derived memberships, with no unmatched rows or derived-membership gaps. No AuditEntries references, inbound foreign keys, triggers, jobs, or module dependencies were found. The table contains creation metadata (`CreatedAt`/`CreatedBy`) that the drop would lose, and the migration's `Down` cannot restore those values. Under the task's no-data-loss gate, the migration remains unapplied.

| Required database completion field | Evidence |
|---|---|
| Model synchronized | Yes — offline EF model/snapshot check. |
| Pending migrations | 1: `20261116200535_RemoveUserOrganizations`. |
| Environment and identity | Verified: Development, `Shady` / `NexusPmo`, DB ID 5; API endpoint aligned. |
| Backup | Not requested; optional for Development under repository policy. |
| Migration generated and reviewed | Yes. SQL artifact: `artifacts/ef-review/dev-pending-20261116T200558Z-1daaef65.sql`; SHA-256 `8A5F368B822E705F23561DD5A1319870DC8392952EB3DAFBF1CB986EFCF24D5F`. |
| Migrations applied | No — blocked by the two records' non-recoverable creation metadata. |
| Seed mode / idempotency | Not run. |
| Physical schema | Current applied schema inspected; pending removal not applied or post-verified. |
| Persistence tests | Not run against SQL Server. |
| Blocker | A reviewed, non-lossy disposition for the two rows' creation metadata is required before dropping the table. Preserve the table and data until then. |

Read-only gate reports: `artifacts/ef-review/dev-db-preflight-inspect-20261116T200737Z-196c5f4e.json` and `artifacts/ef-review/dev-db-migrations-status-20261116T200857Z-33d52f92.json`.

## Acceptance decision

Do not mark this work complete. The source changes and test additions are reviewable, but final acceptance is blocked by the 15 focused test failures, unrun Browser/API UAT and SQL Server persistence tests, the frontend temporary-directory cleanup failure, and the non-lossy database migration gate. Keep `20261116200535_RemoveUserOrganizations` pending and preserve the Development table and its records.
