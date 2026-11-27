# Document Center Merge Recovery

**Priority:** P0  
**Repository:** `C:\Projects\pmo`  
**Recovery source:** `d9cae4ab6b39d9a357370de48e00fbb35596320e` (`document center - final`)  
**Integrated source snapshot:** `ab9231889b5dc1b54101c777685185d6d10d70ed` (PR #47)

## Execution status

| Work item | Status |
|---|---|
| Preserve and inspect the current worktree | Done — pre-existing staged, unstaged, and untracked changes recorded below |
| Trace branch, merge, and remote-ref history | Done |
| Inventory Document Center changes absent from current `main` | Done — recorded before source recovery |
| Selectively recover and integrate verified code | Done — restored into the current main worktree; uncommitted |
| Build, focused tests, and final review | Done with blockers recorded below |

## Git forensics

At inspection, `HEAD` and `origin/main` were `57f6368ea88d6bdb29658e51964214f98ccd0211`, a merge of `feature/payment-lifecycle-isolation`. Its first parent was `e8a180c205b1b07d5ac2d5ed6549942fb598fd75`, which merges `integration/finance-payment-lifecycle` into `335b19dff3f39e074251587a8f420d3c32ead11d` (`finance module`). Neither current merge changes Document Center paths relative to its first parent.

The last integrated main commit containing the completed Document Center is `ab9231889b5dc1b54101c777685185d6d10d70ed`, merge PR #47. Its parents are `be72a17471754f3dfd9652f3e622f75d86795580` (PR #44) and `d9cae4ab6b39d9a357370de48e00fbb35596320e` (`document center - final`). The final feature branch contains the preceding `Document Center v1` commit `cdd1bddf46b851eef38fc915137d4eefe32c1bc4` and validation commit `14fcbd903d5ee26db2e6814c6e27aed5f3d8690f`.

Current `HEAD` is not descended from `ab923188`; their merge base is `335b19d`. The remote-tracking reflog records `origin/main` at `ab923188` after a fetch at 21:01:01 (+02:00), then updated to `57f6368` by push at 21:01:06 (+02:00). This is evidence of a non-fast-forward branch replacement at the push; the reflog does not preserve the exact command, so a force-push cannot be stated as proven. The payment merges are on the replacement line and did not themselves remove Document Center paths.

The branch name was `Document-Center`: reflog entries show checkout between `main` and `Document-Center`, plus `pull --tags origin Document-Center`. No local or remote-tracking branch ref remains. The reflog still contains the feature commits, and the commits remain readable in the repository object database. No stash contains Document Center work; the listed stashes concern the React migration and FCM work. The feature history includes `cdd1bddf` (Document Center v1), `be72a174` (PR #44 merge), `14fcbd90` (document center validations), `d37ba17e` (Document-Center merge), `d9cae4ab` (document center final), and `ab923188` (PR #47 merge into main).

## Recovery inventory — recorded before source edits

Comparison of current `HEAD` with integrated snapshot `ab923188` shows that the base document upload/list foundation remains, but significant later Document Center and Project Phase Gate work is absent. Existing `DocumentService`, `DocumentController`, `Document`/`DocumentCategory` entities, and Project Documents frontend/API files diverge from the completed snapshot. They are not safe to replace wholesale because the current branch and worktree also contain later finance changes.

| Area | Missing or divergent functionality identified in `ab923188` |
|---|---|
| Category administration and lookup | Category CRUD/status controller and permission contracts; organization-scoped category management; template-specific and project-upload category options; category editor, system-category handling, and Project Phase assignment. |
| Central Document Center and templates | Central library/listing UI, search/filter/detail flows, template upload and ownership/category persistence, active/archive/version/download behavior, bounded file validation, and template/version comparison support. |
| Project Documents | Later API/UI contracts for project uploads, immutable source-template/version identity, version history, storage safety, and template selection are missing or differ from the completed implementation. |
| Project lifecycle and Phase Gate | Organization phase-gate configuration APIs/services; Project Phase catalog and transition rules; readiness, evidence, history, compliance snapshots, template pinning, and archived-template-reference validation; associated settings and side-sheet UI. |
| Authorization and integration | Category permission policies, registration and lookup rules, route/navigation/permission mapping, audit integration, and EN/AR labels are absent or diverge. |
| Persistence | Category/phase-gate mappings, configuration readers/registration, and the Document Center/Phase Gate migrations listed below are absent from current `HEAD`. The migration snapshot differs substantially. |
| Tests and delivery documentation | Category scope/authorization, storage/type validation, template history/comparison, library UI, and Phase Gate tests, plus the phase integration and sprint closure documents, are absent or older. |

The candidate migration set relevant to the recovered feature is:

- `20261110170000_AddOrganizationDocumentTemplateStorageMetadata`
- `20261110180235_AddOrganizationPhaseGateConfiguration`
- `20261110190107_AddProjectPhaseGateLifecycle`
- `20261110210000_AddOrganizationManagedDocumentCategories`
- `20261111134000_AddDocumentIsMandatory`
- `20261111173858_AddProjectLifecycleCatalogs`
- `20261112094313_AddDocumentCategoryToProjectPhase`
- `20261112171603_AddProjectPhaseGateEvidenceCompliance`

This is an inventory of source migrations, not a claim that they are pending or applied on the current Development database. The source snapshot also contains unrelated later migrations and features; those are outside this recovery unless required by a verified Document Center dependency.

## Preservation constraints captured before recovery

The worktree was already dirty. These staged changes were present: `apps/web-react/src/features/finance/CostsForecastsPage.tsx`, `CostsOverviewPage.tsx`, `CostsPage.tsx`, `forecast-calculations.ts`, and `BudgetRevisionSection.tsx`. These unstaged files were present: `apps/api/src/NEXUS.Api/Program.cs`, `appsettings.Development.json`, Cost Management DTO/domain files, Notifications `NotificationTypes.cs`, Project Management `ProjectReportingService.cs` and `ProjectService.cs`, `NEXUS.Persistence/AppDbContext.cs`, `PaymentSettlementConfiguration.cs`, frontend Finance `api/finance.ts` and `ProjectFinanceTab.tsx`, plus `endpoints.ts`, `navigation.ts`, and `permissions.ts`. An untracked `b5bf2e54cb654f7b/` directory was also present. These changes are user work and must remain intact.

The integrated candidate is not a safe whole-tree replacement: its migration tree lacks the two payment lifecycle migration `.cs` files present on current `HEAD`, while the current branch has later payment changes. Recovery must preserve the payment migrations, reconcile only the Document Center model and migrations, and avoid restoring unrelated RBAC, financial, or generated artifacts. No database reset, seed, migration apply, or Development data write is authorized or planned.

## Recovery and verification results

## Recovery and verification results

### Recovered files and functionality

Verified source from the Document-Center branch was selectively restored into the current main worktree. This restores the Document Center category catalog and Organization-to-Category upload contract; central template listing, search, filters, details, versions, activation, archiving, and download; Project Documents source-template/version references; Organization Phase Gate configuration; Project Lifecycle catalogs, readiness, evidence, history, compliance snapshots, and the EN/AR UI/API contracts.

Recovered file groups include:

Representative recovered paths:

- `apps/api/src/NEXUS.Modules.Documents/Api/DocumentCategoryController.cs`, `Application/DocumentService.cs`, and `Application/DocumentFileTypeValidator.cs`.
- `apps/api/src/NEXUS.Modules.Organization/Api/PhaseGateRequirementsController.cs` and `Application/PhaseGateConfigurationService.cs`.
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs`, `ProjectPhaseGateReadinessService.cs`, and `Api/ProjectPhaseGateController.cs`.
- `apps/api/src/NEXUS.Persistence/Configurations/DocumentCategoryConfiguration.cs`, `ProjectLifecycleCatalogConfigurations.cs`, and the nine feature migration pairs listed below.
- `apps/web-react/src/features/administration/DocumentCategoriesPage.tsx`, `features/projects/DocumentLibraryOverview.tsx`, `features/projects/LibraryDocuments.tsx`, and `features/projects/components/ProjectPhaseGateSideSheet.tsx`.

- Documents backend: category API and authorization, service/DTO/entity/version changes, upload validation/limits/template comparison, storage provider handling, and module registration.
- Organization and Project Management backend: Phase Gate configuration controllers/services/entities; lifecycle catalog, transitions, readiness, evidence, archived-template checks, and DTOs.
- Persistence: AppDbContext integration, category/Phase Gate mappings, configuration readers/registration, and migration source/designers.
- React: Document Categories and lifecycle administration pages; Document Center library screens; Project Documents and Phase Gate settings/side-sheet screens; API clients; routes, navigation, permission keys, and English/Arabic messages.
- Tests and module documentation for categories, storage/type validation, template comparison, library flows, phase gate configuration/readiness, authorization boundaries, and audit localization.

Recovered feature migration source IDs (their applied/pending state is not known):

1. 20261110170000_AddOrganizationDocumentTemplateStorageMetadata
2. 20261110180235_AddOrganizationPhaseGateConfiguration
3. 20261110190107_AddProjectPhaseGateLifecycle
4. 20261110210000_AddOrganizationManagedDocumentCategories
5. 20261111134000_AddDocumentIsMandatory
6. 20261111173858_AddProjectLifecycleCatalogs
7. 20261111224116_AddProjectStatusReportingSemantics
8. 20261112094313_AddDocumentCategoryToProjectPhase
9. 20261112171603_AddProjectPhaseGateEvidenceCompliance

Within the selected source snapshot, no additional Document Center or Phase Gate source file remains identified as missing. The deleted Document-Center branch ref has not been recreated; its commits remain readable in Git history. The separate untracked draft migration 20261117200000_AddProjectLifecycleCatalogs.cs was preserved and remains outside EF migration discovery.

One current-main RBAC contract gap remains: the current Role/effective-permission contracts do not expose the organization-owned Role scope expected by one branch version of ManagementScopeResolver. The resolver was adapted to use currently supported project/portfolio/program assignments and project-organization lookup; no hard-coded role bypass was introduced. Users relying only on an organization-owned Role still need the separate Role contract integration before that scope can be verified.

### Main integration and preserved work

The active checkout is main at 57f6368ea88d6bdb29658e51964214f98ccd0211. The recovered files are present in this main working tree. No merge commit, staging operation, or remote push was made: Git metadata writes are unavailable in this workspace, and the original index already contains staged Finance work. The restoration is therefore integrated locally but remains uncommitted.

Pre-existing staged Finance changes and the original unstaged Program, settings, payment, notification, project, and configuration changes were preserved. The untracked b5bf2e54cb654f7b directory and the draft lifecycle migration were also left in place. No reset, clean, blanket revert, or cherry-pick was used.

### Build and test evidence

- Frontend focused tests: 7 files passed; 35 tests passed, covering category API/save, library overview and parity, phase settings/side sheet, and Phase Gate audit localization.
- Frontend production build: npm run build passed; TypeScript completed and Vite transformed 2,998 modules, with production build completed in 1m 58s.
- Backend builds: NEXUS.Modules.Documents, NEXUS.Modules.ProjectManagement, and NEXUS.Persistence each built successfully with 0 warnings and 0 errors.
- Backend focused test attempt: tests did not execute because NEXUS.Api.Tests fails compilation with 21 errors in other current-tree tests/contracts, including Finance PaymentRequestStatuses/fiscal-year symbols, financial policy extensions, and ManagementScopeResolverTests.ProjectMember. Recovered Phase Recovery test constructor mismatches and the existing DocumentTenantBoundaryTests constructor contract were corrected; the final compile output contains no remaining errors in those Document Center/Phase Gate call sites.
- Full API build: not verified. A build attempt hit files locked by the already-running NEXUS.Api process (PID 15760); an earlier full dependency-graph attempt also reported missing mpxj 16.10.0.0 references in ProjectPlanning.MppAdapter. Runtime API compatibility and RBAC behavior were not smoke-tested.

### Database and migration safety

No database connection, migration, seed, reset, or data write was performed.

- EF model synchronized: Not verified. The documented EF gate restore failed before the offline model check with NuGet NETSDK1060 / Value cannot be null (Parameter path1) while reading project.assets.json.
- Pending migrations: Not verified; applied history was not inspected. The nine source migration IDs are listed above and must not be interpreted as pending.
- Target environment/database identity verified: No; no database was opened.
- Backup: Not requested.
- Migrations applied: None.
- Minimal seed/idempotency: Not run.
- Physical schema verified: No.
- Persistence tests: Not run.

### Remaining blockers

1. Restore/build the EF gate tooling so model synchronization and the actual Development migration history/schema can be checked. No Development data was changed.
2. Resolve the current API test-project compilation failures, then run the focused backend tests; those tests have not yet run.
3. Obtain an API build that can complete without the running API's output locks and without the MppAdapter dependency errors.
4. Integrate the missing organization-owned Role scope contracts before claiming full Organization-only RBAC coverage.
5. Create a merge commit only when Git metadata writes are available; the restored changes currently sit uncommitted on main.

