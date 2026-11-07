# P0 Permission Delegation Bootstrap and Project Workflows Closure

**Date:** 2026-11-03
**Status:** Source, Development catalog bootstrap, and focused regression verification complete. Authenticated browser UAT remains to be run.

## Scope and permission classification

| Permission | Category / scope | Effect and boundary | Delegation policy |
|---|---|---|---|
| ProjectSettings.View | Project Settings / Project | View project settings in the authenticated tenant and authorized project scope; does not authorize changes or sensitive section data. | Canonical, assignable, outside the protected Tenant Admin effective baseline. Admin received additive role grants to unblock first delegation. Explicit Deny still wins. |
| ProjectSettings.Edit | Project Settings / Project | Edit project settings; depends on ProjectSettings.View. Protected financial configuration retains its own permissions. | Canonical, assignable, outside the protected baseline; additive Admin role grant. Deny still wins. |
| ProjectFinancialFoundation.View | Financial Management / Project | Read the Project financial foundation only; no write, approval, or export rights. | One of four explicit bootstrap delegation keys. Requires effective Roles.ConfigurePermissions and TenantAdministration.ManageScope. Never auto-granted by the Admin baseline. |
| CostUtilization.Export | Capacity / Tenant | Export cost and utilization in tenant/management scope; also requires Resource.View; cost rates remain redacted without Resource.ViewCostRate. | Explicit delegation key only; Active Deny excludes it from the actor's grantable set. Not included in protected baseline. |
| ProgramFinancialRollup.View | Financial Management / Program | Read a Program rollup and in-scope descendant projects only. | Explicit delegation key only; not included in protected baseline. |
| PortfolioFinancialRollup.View | Financial Management / Portfolio | Read a Portfolio rollup and in-scope descendant programs/projects only. | Explicit delegation key only; not included in protected baseline. |

All six catalog entries have EN/AR labels and descriptions. The protected effective Tenant Admin baseline now automatically includes active Tenant-scoped Administration-module permissions and the approved explicit operational keys. The four finance keys above remain excluded. Project Settings permissions are supplemental role grants, not protected baseline permissions, so a direct Deny remains effective.

The one-time Development bootstrap is scoped to SQL Server, Development, and an explicitly supplied expected server/database. It validates the current canonical catalog classification, verifies the actual database identity, uses a serializable transaction and tenant Admin application lock, inserts only missing catalog rows, and adds only the two Project Settings role links to the stable protected Tenant Admin role. It does not grant finance permissions, clear user overrides, change unrelated roles/accounts, or change schema. It records audit entries and then verifies effective permissions and assignability through the actual role administration service.

## Development database evidence

| Required gate item | Result |
|---|---|
| EF model synchronized | **Yes.** No entity, relationship, or schema mapping changed in this closure. |
| Pending migrations | **0.** None pending; source migration set was unchanged. |
| Development identity verified | **Yes.** SQL Server Shady, database NexusPmo, database ID 5; tenant 11111111-1111-1111-1111-111111111111. |
| Backup independently verified | **No.** The user explicitly waived the backup requirement for this bounded Development catalog/role-grant operation; no backup was created or claimed. |
| Migrations applied | **0.** No migration was applied. |
| Physical schema verified | **Yes.** Existing Development schema matched the model/migration state; this operation wrote catalog/role/audit rows only. |
| Catalog and effective delegation | **Verified.** First run added 6 missing catalog rows and 2 Project Settings Admin role links; one active Admin had effective settings and role-configuration capability. The actual role service exposed all 6 delegable entries. |
| Idempotency | **Verified.** Second run added 0 rows and 0 grants; the same active Admin remained effective. |
| Financial grants / account changes | **None.** No financial role grant, user override, or account change was made. |
| Persistence test suite against Development | **Not run as a suite.** Persisted catalog, role, audit, effective permission, and assignability verification was performed by the scoped Development command. No test-fixture writes were made to the live database. |
| Blockers | None for source/bootstrap completion. Authenticated live browser/API grant/revoke UAT is still pending. |

## Project Workflows implementation and verification

The existing Project Workflows route and Project Header Actions entry remain guarded by canonical ProjectSettings.View. Editing requires ProjectSettings.Edit; selecting Custom additionally requires Workflow.View. The project settings side sheet and its Financial Approval configuration remain the existing source of settings. No Project Settings tab was added to Project Details navigation.

The screen loads persisted per-project settings and published definitions from the workflow API. It saves Standard/Custom mode, selected published definition/version, and the existing Change Request approval options. Loading, empty, retry/error, no-permission, validation, save-pending, and success states are represented. EN/AR labels, RTL, responsive layout, and theme tokens are retained.

The backend persists against tenant/project scoped configuration rows; custom definitions, versions, and initial states are explicitly constrained to the authenticated tenant. Workflow execution tenant must match the authenticated tenant. New records resolve the selected active Published version and initial state. Existing workflow instances are not rewritten. The project-configurable domains are Task, Risk, Finance, and Change Request. Reporting/Status Report remains its separately managed specialized workflow.

Approval methods, approvers, sequence, transitions, guards, actions, and domain-specific behavior continue to come from existing published workflow definitions and existing Financial Approval configuration; the project screen does not create a second engine. Capabilities unsupported by the existing engine/configuration remain unsupported by the project screen. Existing workflow instances are not retroactively converted.

Focused verification:

- API ProjectSettingsAuthorizationTests: **15/15 passed**, covering canonical settings permissions, protected finance configuration policies, explicit Deny, management/project scope, custom selection requiring Workflow.View, Reporting exclusion, tenant isolation, and custom workflow save/reload/apply to a new record.
- API UserPermissionAdministrationTests: **30/30 passed** in the focused permission administration suite, including authorized Project Settings delegation/revocation, recipient Active Deny, finance bootstrap gating, and foreign permission IDs.
- React Project Workflows parity suite: **17/17 passed**, including save/reload, loading/error/empty/retry, no Workflow.View behavior, and reuse of the existing settings side sheet.
- API build passed; the MPXJ blocker was resolved by using the repository's installed NuGet cache and no dependency changes.
- Authenticated browser UAT against the running Development app was not performed; the current process was left running and may still use its pre-change binaries.

## Unsupported in the Project Workflows screen

The screen adopts an existing active Published workflow; it does not create or edit workflow definitions. Editing definition states, transitions, transition-level approver lists/methods/sequences, guards, and actions remains in the existing Workflow Designer. Existing Project Financial Approval defaults remain in the Project Settings side sheet, and Change Request keeps its existing project policy options. Reporting/Status Report is intentionally handled by its specialized workflow configuration. Existing workflow instances are not migrated when project configuration changes.

## Remaining UAT

Run the authenticated positive/negative/cross-tenant UI/API scenarios in the Master UAT Checklist. In particular, confirm a same-tenant authorized role administrator can grant and revoke Project Settings View/Edit, the recipient sees only the granted operations, explicit Deny remains effective, foreign project/tenant identifiers are rejected, and finance bootstrap keys remain delegable only through the authorized path.

## Implementation guidance applied

Reviewed repository-specific permission-first, frontend data integrity, database delivery, security, and test guidance before making changes. No database reset, migration, unrelated permission, duplicate workflow engine, or Project Settings navigation tab was introduced.
