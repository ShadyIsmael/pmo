# Dynamic Project Phases and Statuses — Implementation Report

**Catalog foundation:** 2026-11-11  
**P1 integration and verification:** 2026-11-12  
**Scope:** Reference Data → Project Lifecycle; Project Create/Edit/Search; Status Reports; Program/Portfolio reporting; Phase Gate compatibility; safe legacy cleanup.  
**Status:** The original catalog foundation and its Development migration were previously verified. This P1 pass removes transition-workflow setup from the lifecycle catalog contract and integrates reporting semantics. The current verification table distinguishes checks already run from checks still outstanding; Product Owner browser UAT remains separate.

## Product and API behavior

Project Phase and Project Status are separate database-backed catalogs with stable IDs/codes, bilingual names and descriptions, ordering, active/default flags, and Tenant/optional Organization scope. Project Health remains distinct. Monitoring & Controlling may be marked as a continuous activity alongside delivery phases. Create and Edit catalog records do not ask for transition steps or allowed-next entries, and the catalog/options contract does not require transition setup.

Status catalog entries carry configurable reporting semantics. `ReportingCategory` is one of `InProgress`, `Completed`, or `Excluded`; it drives active/completed rollups and whether a project belongs in Phase Gate compliance scope. `AttentionLevel` is one of `None`, `NeedsAttention`, or `Critical`, so attention summaries and critical counts remain distinct without status-name checks. The initial backfill preserves the legacy meanings of common statuses: active/on-hold work is `InProgress`, completed/closed/done work is `Completed`, and cancelled/archived work is `Excluded`; legacy attention states retain their attention level. Users can set these semantics for newly created or edited statuses without changing status names or codes.

Create/Edit Project, project search and filters, Status Report editing, dashboards, and Program/Portfolio rollups consume catalog-backed options or the catalog-linked reporting semantics. A custom phase or status can therefore be selected and reported without adding a new code branch. Historical status text remains projected where existing integrations require it.

Project Status changes are direct actions protected by `ProjectStatus.Change`, tenant/project/organization scope, Active Deny, and Admin safeguards. A change does not consult a transition graph and is recorded in the existing audit trail with prior and new catalog identities; a supplied reason remains optional audit detail. Ordinary Project updates cannot change Phase or Status.

Project Phase changes require `Project.View` and `ProjectPhase.Change` in the scoped project context. Phase Gate requirements/evidence and immutable snapshots remain governance information, but missing, mismatched, or unavailable documents do not block an otherwise authorized transition; readiness is recorded for reference in the transition audit. The transition does not require `Document.View`. Phase Gate requirements remain separate from lifecycle transition configuration. Historical code/name snapshots and project audit events remain readable.

## Legacy reference trace and cleanup

The initial cleanup removed three unused frontend components: `ProjectFilters.tsx`, `ProjectCards.tsx`, and `ProjectTable.tsx`; the dead `ProjectFilters` file contained an obsolete fixed status filter. The P1 integration removes remaining active hardcoded Project Status choices/classifications only after each caller is moved to catalog options or reporting semantics, including the Home summary active-project count. Compatibility fields and routes are retained where they still serve existing contracts.

`Project.Status` / `Project.Phase` text, stable catalog IDs, phase-gate snapshots, and audit history remain intact for existing projects and integrations. The separate `ProjectPhase` entity remains a project-plan breakdown concept, not the lifecycle phase catalog. Phase Gate document requirements may be configured for any two active, in-scope phases; these phase pairs do not define allowed project movement. Actual phase changes remain direct; document readiness is reported and audited but does not block an authorized transition. Existing transition-rule tables from the catalog migration are preserved as historical storage and are not consulted for catalog CRUD, options, Phase Gate pair listing, status mutation, or phase readiness. Do not drop those tables or rewrite the applied migration as part of this cleanup. No project rows, audit entries, historical migrations, or catalog IDs are deleted or rewritten.

## Permission contract

| Operation | Permission | Scope and behavior |
|---|---|---|
| View phase catalog | `ProjectLifecyclePhase.View` | Tenant or authorized Organization catalog |
| Create/edit phase, ordering/defaults | `ProjectLifecyclePhase.Create` / `ProjectLifecyclePhase.Edit` | Requires matching View and catalog scope; no transition setup |
| Activate/deactivate phase | `ProjectLifecyclePhase.ActivateDeactivate` | Preserves project references; no hard-delete action |
| View status catalog | `ProjectLifecycleStatus.View` | Tenant or authorized Organization catalog |
| Create/edit status, reporting category/attention level, ordering/defaults | `ProjectLifecycleStatus.Create` / `ProjectLifecycleStatus.Edit` | Requires matching View and catalog scope; no transition setup |
| Activate/deactivate status | `ProjectLifecycleStatus.ActivateDeactivate` | Preserves project references; no hard-delete action |
| Change phase | `ProjectPhase.Change` | Project scope plus existing Phase Gate/document checks |
| Change status | `ProjectStatus.Change` | Project scope; direct active in-scope catalog selection and audit; no transition graph |
| Load project search choices | `Project.View` | Tenant/authorized Organization lookup scope |
| Load Create/Edit choices | `Project.Create` or `Project.Edit` | Tenant/authorized Organization lookup scope; does not grant catalog administration |
| Load Status Report status choices | `StatusReport.View`, `StatusReport.Create`, `StatusReport.Edit`, or `StatusReport.Publish` | Requires a scoped project ID; returns statuses only |
| Load phase change choices | `ProjectPhase.Change` | Requires a scoped project ID; returns phases only |
| Load status change choices | `ProjectStatus.Change` | Requires a scoped project ID; returns statuses only |

The permission catalog, policies, frontend guards, and permission contract tests use these keys. Existing Active Deny and Tenant/Organization/resource checks remain. No role-name authorization or grant widening was introduced. Status changes remain a project action; catalog reporting settings remain protected by the catalog Edit permission.

## Migration and compatibility

Migration `20261111173858_AddProjectLifecycleCatalogs` creates the phase/status catalogs and transition-rule tables, adds project and Phase Gate ID links, seeds initial values and maps legacy values. Unknown legacy values receive bilingual `LEGACY-*` records. The existing text columns remain, project associations and audit rows are retained, and duplicate mapped Phase Gate configuration/snapshot pairs fail explicitly rather than being merged. The migration replaces uniqueness/check constraints as needed, but its `Up` path does not drop project data, audit rows, text columns, or old catalog values. Its `Down` path drops the new catalog structures and must not be used casually.

## Earlier catalog-foundation verification — 2026-11-12

This record is the read-only baseline from before the P1 reporting-semantics schema change. It does not establish the current pending migration count or post-change model/schema state.

The restricted execution attempt failed before server identity/history reads with `TCP Provider: Access is denied`. A read-only inspection from the approved Windows host context succeeded using the Development API connection and Windows Integrated Authentication. This identifies the recurring failure as an execution-context boundary; no connection string or credentials were logged. The documented EF gate fallback is now recorded in `AGENTS.md` and `docs/development/ef-core-database-workflow.md`.

| Check | Result |
|---|---|
| EF model/snapshot synchronized | Yes — offline model gate passed; 23 repository migrations |
| Pending database migrations and names | 0 — none |
| Environment and exact target identity | Verified — Development; `SERVERPROPERTY('ServerName')=Shady`, `DB_NAME()=NexusPmo`, database ID 5 |
| Migration state | `20261111173858_AddProjectLifecycleCatalogs` is already applied in history. No migration was applied during this resume. |
| Backup | Not requested |
| Required seed mode and idempotency | Not run — no reset/seed was requested |
| Physical schema | Overall gate: No. It reported 15 pre-existing differences on `IntegrationConnections`, `IntegrationWebhooks`, and `Programs` (fingerprint `7A6D2E074284D56438749F563AF4AEF3BD2DEE4E3687DC54F9ADF467C095E9FB`); no lifecycle migration is pending. |
| Persistence tests against Development SQL Server | Not run |
| Focused React checks | Typecheck passed; focused Status Report test passed 12/12 |
| Focused API tests | Failed before test discovery because MSBuild could not copy `NEXUS.Modules.ProjectPlanning.MppAdapter.dll` into the API test output; another process held the destination file open. |
| Final API build | One attempt in an isolated output directory failed with 23 `MSB3030` reference-copy errors because disabling project-reference builds redirected reference paths to absent `bin/lifecycle-verify` assemblies. No C# compiler diagnostic was reported. |
| Authenticated browser/Product Owner UAT | Not run |

At that earlier point, no database writes, target-specific SQL generation, backup, or seed operation occurred. The earlier target-specific SQL artifact referenced in prior notes is not present as a current artifact and must not be used as a pending script. The API test run did not reach test discovery; the single API build attempt recorded there failed at reference copying, not C# compilation.

## Current P1 verification

| Check | Current P1 result |
|---|---|
| EF model/snapshot synchronized | Yes — host preflight and offline model gate report synchronized; repository contains 24 migrations |
| Pending Development migration count and names | 1 — `20261111224116_AddProjectStatusReportingSemantics` |
| Development environment and exact server/database identity | Verified read-only from the approved Windows host: Development; `SERVERPROPERTY('ServerName')=Shady`, `DB_NAME()=NexusPmo`, database ID 5; Windows Integrated Authentication verified |
| Backup | Not requested |
| Migrations applied | No — no apply authorization was given. Reviewed target-specific SQL is `artifacts/ef-review/dev-pending-20261111T230351Z-b6901660.sql`, SHA-256 `3DC834F93B45C5B9886417BC02CA835342EAAF68083E4C4A888630E4A392ADC1` |
| Required seed mode and idempotency | Not run — no reset or seed was requested |
| Physical schema | No — 17 reported differences: 15 existing baseline differences on `IntegrationConnections`, `IntegrationWebhooks`, and `Programs` (fingerprint `7A6D2E074284D56438749F563AF4AEF3BD2DEE4E3687DC54F9ADF467C095E9FB`) plus the two columns added by the pending migration |
| Persistence tests against Development SQL Server | Not run |
| Focused API tests | Not run — `dotnet test` stopped before test discovery when MSBuild could not copy referenced assemblies into `apps/api/src/NEXUS.Api/bin/Debug/net10.0` because another process held them; diagnostic log: `artifacts/ef-review/project-lifecycle-api-tests-final.log` |
| React typecheck and focused lifecycle tests | Passed — `npm run typecheck`; six focused suites, 40/40 tests |
| Single final API build attempt | Passed — `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1`; 0 errors, 352 IKVM compatibility warnings; log: `artifacts/ef-review/project-lifecycle-final-api-build.log` |
| Authenticated browser/Product Owner UAT | Not run; handed to Product Owner |
| Blockers | Pending Development migration (not applied), 15 unrelated baseline schema differences, unavailable Development persistence-test evidence, API tests not executed, and Product Owner UAT |

## UAT handover

The [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md) records positive, negative, and cross-scope scenarios for catalog management without transition setup, catalog-driven project/report choices, configurable reporting semantics, direct audited status changes, separate Phase Gate document readiness, historical data preservation, permissions, and localization. Browser UAT is a Product Owner handover and must remain marked not run until exercised by an authenticated user.

## P0 resume fix - default selection - 2026-11-12

The shared ProjectLifecycleCatalogService.SetDefaultAsync logic for both Project Phase and Project Status was corrected. The previous single SaveChanges batch could violate the filtered unique default index (UX_ProjectPhaseCatalogEntries_Scope_Default / UX_ProjectStatusCatalogEntries_Scope_Default) while clearing and setting defaults in the same statement batch.

### Change summary

- Validate permission, trusted management context, scope, active state, and RowVersion before changing data.
- Selecting an already-default record is now idempotent and returns success without changing data.
- Within one transaction per operation:
  1. Clear the existing default(s) in the same Tenant/Organization scope and persist that change.
  2. Set the selected record as default and persist.
  3. Write the SetDefault audit record capturing prior and new default identities.
  4. Commit only after all operations and audit succeed; rollback on any failure.
- DbUpdateConcurrencyException and DbUpdateException (including unique-constraint conflicts) are converted to conflict responses instead of HTTP 500.
- Tenant/Organization isolation, Active Deny, permission checks, and unique-default integrity constraints are preserved. No unique index or database value was manually patched, and no migration is expected.

### Verification

- Focused API build: succeeded.
- React typecheck: passed.
- Focused runtime regression tests for default switching, idempotency, scopes, concurrency, rollback, audit, authorization, and RowVersion conflicts are handed over for Product Owner/CI execution; the local test project currently contains unrelated in-progress compile errors outside this scope.

## P1 resume fix - Project Lifecycle Options 404 - 2026-11-12

The `GET /api/project-lifecycle/options?projectId=...` endpoint returned HTTP 404 with "The requested project was not found" for valid authorized projects.

### Root cause

The `ProjectPublicIdRoutingMiddleware` rewrites the external `projectId` query parameter from the Public ID to the Internal ID before the request reaches the controller. However, `ProjectLifecycleCatalogService.GetOptionsAsync` was still looking up the project by `PublicId` instead of the already-resolved Internal ID (`Id`).

### Change summary

- In `ProjectLifecycleCatalogService.GetOptionsAsync`, changed the project lookup from `item.PublicId == projectId.Value` to `item.Id == projectId.Value` since the middleware has already resolved the Public ID to the Internal ID.
- Updated the inline comment to clarify that API callers receive the internal Project ID after PublicId routing middleware rewrites the route/query parameter.
- Updated integration tests in `ProjectLifecycleCatalogOptionsTests` to pass the Internal ID (`project.Id`) instead of the Public ID (`project.PublicId`) when calling the service directly (bypassing middleware).
- Fixed FluentAssertions `BeEquivalentTo` calls in tests to use explicit array syntax for type inference.

### Verification

- React typecheck: passed.
- ProjectManagement module build: succeeded.
- API build: succeeded (single attempt; reference-copy locking issue noted in earlier verification persists as environment constraint).
- Integration tests for lifecycle options could not run due to pre-existing test project compilation errors unrelated to this change; middleware unit tests have pre-existing tenant-scoped persistence failures in the test harness.
- No database migration required.
- Authorization, tenant/organization isolation, Active Deny, and scope checks preserved.

## Earlier P0 schema recovery report - Project Status reporting semantics - 2026-11-12

This records an earlier session's claim that the reporting-semantics migration was applied. The Main Agent's later read-only host preflight for the P1 integration found the migration pending again; use the Current P1 verification below as the latest database state. No migration was applied during that later check.

The Development database was missing ProjectStatusCatalogEntries.AttentionLevel and ReportingCategory, causing GET /api/project-lifecycle/statuses?includeInactive=true to fail with SQL error 207.

### Diagnosis and fix

- EF model and configuration already declared both columns with check constraints and safe defaults (None, InProgress).
- Migration 20261111224116_AddProjectStatusReportingSemantics existed but had not been applied to Development.
- Read-only host-side preflight (scripts/dev/db-preflight.ps1) verified Development target Shady / NexusPmo and generated pending SQL.
- Reviewed SQL was applied through scripts/dev/db-apply-migrations.ps1 with authorization reference P0-ProjectStatusCatalogSchemaRecovery-2026-11-12.
- Applied migration: 20261111224116_AddProjectStatusReportingSemantics.
- Result: 1 migration applied; post-apply EF model synchronized: Yes; post-apply schema verified excluding acknowledged baseline drift: Yes.
- Existing baseline drift unrelated to this task remains 15 objects (IntegrationConnections, IntegrationWebhooks, Programs) and is acknowledged, not repaired here.
- Existing status records received safe defaults through the migration backfill; no reset, manual SQL patch, or history manipulation was used.
- No hardcoded status names are used by application reporting logic; migration backfill is one-time data preservation only.

### Verification

- Development database target verified: Shady / NexusPmo.
- Windows Authentication: Yes.
- Migration history updated with 20261111224116_AddProjectStatusReportingSemantics.
- Physical schema now includes AttentionLevel and ReportingCategory.
- Final API build: succeeded.
- React typecheck: passed.

## Sprint 1 phase/document integration addendum - 2026-11-12

This addendum records the later Sprint 1 closeout and supersedes earlier transition-readiness wording and any implication that the latest live Development migration status was verified. Earlier verification entries above remain historical records of those earlier runs.

- Pre-Initiation provisioning/recovery, Project creation defaulting, exact Tenant+Organization category mapping validation, and non-blocking document readiness on authorized phase transitions are implemented in source. See [Project Phase and Document Category integration](PROJECT-PHASE-DOCUMENT-INTEGRATION.md) and [Sprint 1 closure](PROJECT-PHASE-DOCUMENT-SPRINT1-CLOSURE.md).
- Offline EF model/snapshot gate passed with 25 source migrations. The source migration `20261112094313_AddDocumentCategoryToProjectPhase` exists and was reviewed but not edited or applied.
- Latest Development migration status, exact target identity, and physical schema are **not verified**. The documented read-only status wrapper failed before identity/history checks with `TCP Provider: Access is denied`. The earlier table's pending-migration count is an earlier observation and must not be treated as current evidence.
- The final consolidated filtered API test attempt failed at test-project compilation before test discovery. Earlier compilation attempts exposed an Organization type ambiguity and a missing test import; both task-specific issues were fixed. The final output still included compile errors in other test sources, including Finance status references, `ManagementScopeResolverTests`, and `PhaseGateComplianceAggregationTests`. The targeted Sprint 1 suites did not execute.
- No migration apply, database write, seed, reset, persistence test, or browser UAT was performed. Do not start Sprint 2 until API test compilation and the documented Development status inspection are resolved.
