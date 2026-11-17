# P0 Create Project Lifecycle Options 409 Recovery

**Date:** 2026-11-17  
**Status:** PARTIAL — implementation and focused automated checks pass. Live Development database evidence and Browser UAT remain open; a supplementary API role-permission regression suite has failures whose attribution is unresolved in the broad dirty checkout.

## Scope and finding

The Create Project form requested lifecycle options with `purpose=create` before a Portfolio was selected. The hook already sent `purpose`, Portfolio, and Department, but the controller did not bind that context. The service therefore checked the protected Pre-Initiation phase against a null Organization and returned a 409 for a valid Organization-scoped catalog. This is a source-traced cause; the reported authenticated runtime request and persisted Development catalog state were not observed in this recovery.

The historical lifecycle options GET can provision Pre-Initiation through a separate path. This recovery did not replay the live GET or relax that protected-phase behavior. The create-options request now returns an empty options set before Portfolio selection and performs Organization resolution only after a scoped Portfolio is supplied.

## Changes authored

- `ProjectLifecycleCatalogController` binds `purpose`, `portfolioId`, and `programId` for the options route.
- `ProjectLifecycleCatalogService` requires effective `Project.Create` for create-purpose options, rejects caller-supplied Organization/Project IDs, resolves an active same-Tenant Portfolio and its active Organization, enforces Portfolio/Program scope, and requires any Program to be active and owned by that Portfolio. It returns empty options before Portfolio selection. After selection, phases and statuses are filtered to the resolved Organization; the protected active default Pre-Initiation check stays Organization-scoped.
- `ProjectService` requires an explicit Portfolio on create. A Program cannot implicitly supply one. Program/Portfolio mismatch and inactive hierarchy cases are rejected.
- `CreateProjectSheet` waits for Portfolio selection before loading lifecycle options, sends selected Portfolio and Program scope, removes arbitrary default-phase fallback, requires Portfolio before submit, clears stale selection state on hierarchy changes, and displays localized selection/unavailable states in English and Arabic.
- API regression coverage includes controller query binding for no Portfolio and valid Portfolio, organization-only option filtering, unassigned/archived Portfolio, inactive/mismatched Program, and explicit-Portfolio Project create rules. React regression cases cover the no-Portfolio state, required Portfolio, protected-phase behavior, selected scope, and stale phase handling.
- `MASTER-UAT-CHECKLIST.md` now contains CPL-409-01 through CPL-409-06 for Portfolio selection, scoped options, tenant/Organization isolation, Program validation, non-mutating missing-phase behavior, and Role/Side Menu regression.

## Review

The independent workflow reviewer found no blocking authorization or scoping defect in the changed Create Project path. The reviewer requested explicit tests for an active same-Tenant Portfolio outside `Scope.PortfolioIds` and an inactive Program. Both cases were added and passed in the focused API run below.

## Verification record

| Check | Result |
|---|---|
| React TypeScript typecheck | PASS — `npm run typecheck` |
| Focused React Vitest | PASS — `project-create-sheet-initial-budget.test.tsx`, 21/21 |
| Role / Side Menu React regressions | PASS — `organization-configuration-access.test.tsx`, `role-permission-eligibility.test.ts`, and `project-lifecycle-catalog-org-admin.test.tsx`, 16/16; includes Sidebar access behavior |
| React production build | PASS — `npm run build` (workspace-local temporary directory used for esbuild; existing chunk-size and import warnings remain) |
| API lifecycle options and Project ownership tests | PASS — 30/30 across `ProjectLifecycleCatalogOptionsTests` and `ProjectOwnershipIntegrityTests`; API and test assemblies compiled in this run |
| Supplementary API management-scope / tenant-admin permission regressions | FAIL — 1/7 passed. Five `ManagementScopeResolverTests` fail while their shared fixture saves with SQLite foreign-key errors at `ManagementScopeResolverTests.cs:108`; one `TenantProvisioningAdminRolePermissionTests` fails because its expected 63 core permission keys differ from the current catalog containing additional keys. Attribution is unresolved in the broad dirty checkout; this suite remains open separately from the focused lifecycle/ownership filter. |
| `git diff --check` | No whitespace findings in the recovery files. It reported existing trailing whitespace in unrelated dirty files `User.cs`, `SeedDataProvider.cs`, and `UserPermissionAdministrationTests.cs`; those files were not edited for this recovery. |
| Authenticated runtime request / Browser UAT | NOT RUN — requires the user's consolidated UAT session |

## Development database and persistence evidence

No database connection, authenticated options GET, database write, migration, seed, or backup was performed. The approved Windows Development host was not available to this Agent executor, so current identity, catalog rows, duplicate protected phases, and any tenant-level Pre-Initiation row have not been verified. Historical Development reports are not current evidence.

| Required evidence | Status |
|---|---|
| EF model synchronized | Not run in this recovery |
| Pending migration count and names | Not verified |
| Current Development environment and `Shady` / `NexusPmo` / database ID 5 identity | Not verified in this run |
| Backup | Not requested; no database write was performed |
| Migrations applied | None |
| Seed mode / idempotency | Not applicable; no seed ran |
| Physical schema | Not verified in this run |
| Persisted lifecycle rows / duplicate and tenant-scope comparison | Not verified |
| Persistence tests | Not run |
| Blockers | Host-side read-only Development preflight/catalog evidence and Browser UAT remain open; supplementary API role-permission failures are recorded above |

## Required follow-up

From PowerShell 7 on the authorized Windows Development host, run the documented read-only preflight from [Development database connectivity and runner](../development/development-database-connectivity-and-runner.md):

```powershell
Set-Location 'C:\Projects\pmo'
.\scripts\dev\db-preflight.ps1
```

Review the sanitized report for `SERVERPROPERTY('ServerName')`, `DB_NAME()`, database ID, EF migration history, and schema status. Compare Organization-owned lifecycle phase/status IDs, active/default flags, duplicate protected-phase candidates, and any tenant-scoped Pre-Initiation rows. Do not replay the options GET until its side effects are explicitly accounted for. The focused automated lifecycle, ownership, React, and build checks are complete; keep CPL-409-01 through CPL-409-06 open until live runtime and Browser UAT evidence is recorded. Investigate the supplementary API permission-suite SQLite fixture and catalog expectation failures separately, without assuming attribution.
