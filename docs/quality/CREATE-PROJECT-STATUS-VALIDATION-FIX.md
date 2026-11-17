# Create Project Status Validation Fix

## Defect

Create Project showed `Select a project status.` while the Project Status field was disabled,
so the form could never be submitted successfully.

## Root cause

Project Status is Tenant-scoped reference data shared by every Organization. Project Phase
remains Organization-scoped (protected Pre-Initiation). The create flow violated that split in
four places:

1. `ProjectLifecycleCatalogService.GetOptionsAsync` filtered Status options by the resolved
   Organization (`OrganizationId == organizationId`), which excludes Tenant rows
   (`OrganizationId = null`). The status catalog therefore arrived empty.
2. Create options could only be requested after a Portfolio was selected, even though the
   Status catalog does not depend on the Portfolio.
3. The Portfolio and Program change handlers cleared `statusId`.
4. No status default was applied when the catalog finally loaded, and the select stayed
   disabled whenever the option list was empty.

## Contract now enforced

- Project Status options are always the Tenant catalog (`OrganizationId == null`), for Create
  Project with or without a Portfolio and for existing Project lookups. Other Organizations'
  status rows and other Tenants' rows are excluded.
- Create Project without a Portfolio returns Status options only. It returns no Phases and does
  not require or provision the Organization-scoped protected Pre-Initiation phase.
- With a Portfolio selected, Phases come from that Portfolio's Organization (protected
  Pre-Initiation first) and Status still comes from the Tenant catalog.
- No Status or Phase IDs are hardcoded, and backend validation remains authoritative.

## Changes

- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs`
  - Create purpose without a Portfolio sets `createTenantStatusOnly`; phase option loading and
    the Pre-Initiation precondition are skipped, so the response contains only Tenant Statuses.
  - Create purpose with a Portfolio resolves the owning Organization for Phases while Status is
    filtered to `OrganizationId == null`.
  - Project and Organization lookups also filter Status to the Tenant catalog.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs`
  - `ResolveLifecycleSelectionAsync` resolves the initial/default Status from Tenant-scoped
    statuses only.
- `apps/web-react/src/features/projects/components/CreateProjectSheet.tsx`
  - Lifecycle options load as soon as the sheet opens, without waiting for a Portfolio.
  - The Status select is enabled from the Tenant catalog, auto-applies the configured Tenant
    default, and replaces stale IDs that are not in the loaded catalog.
  - Portfolio/Program changes no longer clear the selected Status.
  - Submission uses the resolved Tenant Status ID, not a stale form value.
  - Submission is also blocked while the status lookup is unresolved (loading, lookup error,
    or an empty Tenant catalog) so `Select a project status.` can never appear against a
    disabled Status field; the truthful loading/error/empty state explains why.
  - A single lifecycle lookup error with a working Retry action is shown for both create and
    edit modes; the empty-status hint no longer requires a Portfolio.
  - The initial Phase is only resolved after a Portfolio is selected.
- `apps/web-react/src/i18n/messages/en.json`, `.../ar.json`
  - Phase hint now refers to project phases; create description states that Status comes from the
    Tenant catalog and Phase from the selected Portfolio's Organization.
- Tests:
  - `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectLifecycleCatalogOptionsTests.cs`
    - Create options without a Portfolio return only Tenant Statuses, no Phases, and no
      Pre-Initiation conflict or provisioning.
    - Create options with a Portfolio include Tenant Statuses and exclude the owning
      Organization's and another Tenant's statuses.
  - `apps/web-react/src/test/project-create-sheet-initial-budget.test.tsx`
    - Tenant Status loads, is enabled, and auto-selects the default before a Portfolio exists.
    - Selection survives Portfolio/Program changes and is submitted as `statusId`.
    - A stale status is replaced by the Tenant default; an empty catalog shows the truthful
      empty state and keeps the Create button disabled so no error is raised against the
      disabled Status field; create-mode lookup errors expose Retry and also block submission.

## Validation

Commands run from `C:\Projects\pmo` on 2026-11-17:

| Check | Command | Result |
|---|---|---|
| API build + lifecycle options tests | `dotnet test apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj -p:OutputPath=<isolated-temp> --filter FullyQualifiedName~ProjectLifecycleCatalogOptionsTests` | **PASS — 18/18** (isolated output used because a concurrent `NEXUS.Api` dev server PID 17668 locks the shared `bin` output) |
| Focused React suite | `npm --prefix apps/web-react run test -- project-create-sheet-initial-budget` | **PASS — 25/25** |
| CreateProjectSheet consumer sweep | `npm --prefix apps/web-react run test -- project-create-sheet project-settings project-lifecycle project-creation` | **PASS — 6 files, 44/44** |
| Projects list / overview parity (lifecycle caller regression) | `npm --prefix apps/web-react run test -- projects-list project-overview-source-parity` | **PASS — 2 files, 34/34** |
| TypeScript typecheck | `npm --prefix apps/web-react run typecheck` (`tsc -b`) | **PASS** |
| React production build | `npm --prefix apps/web-react run build` with `TEMP`/`TMP` pointed at a workspace-local folder | **PASS — built in 2m 19s** (existing chunk-size warnings remain). The esbuild `remove ...\Temp\esbuild-...: Access is denied` failure only occurs with the default system temp folder in this executor; a workspace-local temp directory succeeds. |

Cross-Tenant isolation evidence (backend): `ProjectLifecycleCatalogOptionsTests` asserts that
create options exclude another Tenant's status row (`NotContain(foreignTenantStatus.Id)`, lines
212 and 226) and that a cross-Tenant project options request returns 404 (lines 240-242).

## Unrelated failing suites observed

A broad filter run
(`dotnet test ... --filter "FullyQualifiedName~ProjectLifecycleCatalogOptionsTests|FullyQualifiedName~PreInitiationPhaseProtectionTests|FullyQualifiedName~ProjectLifecycleDocumentCategoryScopeTests|FullyQualifiedName~ProjectLifecyclePermissionContractTests|FullyQualifiedName~OrganizationPhaseGateConfigurationTests"`)
resulted in **37 passed / 19 failed**. None of the failures are on the changed Status paths;
they fall in phase-protection, phase-gate configuration, and phase document-category code that
concurrent sessions are editing in this same dirty checkout:

- `PreInitiationPhaseProtectionTests` (6 failures): `SeedProvisioning_*` expect Tenant-level
  (`OrganizationId = null`) Pre-Initiation rows, but the concurrent seed change
  (`SeedDataProvider.cs`, 2026-11-16) now provisions Pre-Initiation only at Organization
  scope; `LifecycleOptions_ExposeOnePreInitiationOptionAndKeepItDefault` and
  `PreInitiationPhase_RecoveryRestoresCanonicalCode` expect the old self-healing
  `EnsurePreInitiationPhaseAsync` read path, which concurrent work replaced with the
  fail-fast `HasActiveDefaultPreInitiationPhaseAsync` / 409 contract
  (see `docs/quality/PROJECT-LIFECYCLE-OPTIONS-409-RECOVERY.md`).
- `OrganizationPhaseGateConfigurationTests` (12 failures): test-host `EnsureCreated` fails
  with `SQLite Error 1: 'near "max": syntax error'` — a SQL Server-to-SQLite model
  translation boundary in that suite's own schema, before any assertion runs.
- `ProjectLifecycleDocumentCategoryScopeTests` (1 failure):
  `Tenant_scoped_phase_rejects_category_from_another_tenant_even_when_both_scopes_are_tenant_wide`
  expects `400` but receives `403`, because concurrent permission-scope refactoring of
  `CreateAsync` now returns `Forbidden` for a Tenant-scoped phase create first.

These suites are recorded as open, concurrent-work regressions; they were not caused by, and
were not fixed in, this defect's scope. Re-run them once the concurrent phase/seed/permission
work settles.

## Database evidence

No database operation was performed: no connection, migration, seed, reset, or write.

| Required evidence | Status |
|---|---|
| EF model synchronized | Not run — no schema change in this defect |
| Pending migration count and names | Not verified — no migration created or applied |
| Target environment / database identity (`Shady` / `NexusPmo`) | Not verified — no database connection was opened |
| Backup | Not requested (no write performed) |
| Migrations applied | None |
| Seed mode / idempotency | Not applicable — no seed ran; Tenant-level Status seeding already exists in `SeedDataProvider`, no reseed performed |
| Physical schema | Not verified — no schema change |
| Persistence tests | Not run against a database; all cited tests are in-memory/SQLite test-host tests |
| Blockers | Development host-side read-only preflight and authenticated Browser UAT remain open (Product Owner session required); concurrent phase/seed/permission suites failing as listed above |
