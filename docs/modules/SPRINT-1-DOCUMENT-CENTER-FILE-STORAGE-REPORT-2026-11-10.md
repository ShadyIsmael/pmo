# Sprint 1 — Document Center & File Storage Foundation

**Date:** 2026-11-10  
**Status:** Source implementation handoff prepared. The P0 final consolidated verification was attempted; the current results and blockers are recorded in the final section below. Technical Definition of Done remains incomplete, and authenticated browser UAT has not been performed.

## Delivered in source

The existing Documents module is reused. Governance / Document Center is the sole catalog for organization-owned approved templates. Project Documents continues to use the shared `Document` and `DocumentVersion` records and retains its existing file workflows; it does not register or manage the approved template catalog. Template IDs and version IDs remain stable, new versions preserve prior content, and project copies can retain both the source template ID and exact source template version ID.

Template ownership is validated against the authenticated Tenant and active Organization in the resolved management scope. The project scope resolver also resolves Organization context through a Program's Portfolio for program-linked Projects. Organization IDs supplied by clients are selectors, not authorization evidence. Existing ordinary and project documents retain nullable Organization ownership and do not require destructive data migration or replacement.

The existing permissions are reused:

| Operation | Required permissions |
|---|---|
| Read/list/detail/category/organization lookup/preview/download | `Document.View` |
| Create ordinary or project document | `Document.Create` |
| Create template | `Document.Create` + `Document.TemplateManage` |
| Add ordinary version | `Document.Edit` |
| Add template version or reactivate template | `Document.Edit` + `Document.TemplateManage` |
| Archive ordinary/project document | `Document.Archive` |
| Archive template | `Document.Archive` + `Document.TemplateManage` |

Endpoint policies and service-level checks enforce the effective permission and explicit deny claims, together with Tenant, Organization, Project, resource, and management scope. No permission key or role grant migration was added.

## Storage and validation

The existing `IStorageProvider` abstraction is extended for stream storage, open/read, metadata/existence, and deletion. `LocalStorageProvider` stores new files outside `wwwroot` using server-generated opaque keys, checks path and reparse-point boundaries, computes SHA-256 and length while streaming, and cleans up a stored object when metadata persistence fails. Legacy storage fields are retained for existing-row reads; new writes leave them null and save provider metadata on each version.

| Setting | Default / limit |
|---|---|
| `Storage:Provider` | `local`; implementations are registered by provider ID through keyed DI |
| `Storage:DocumentsRoot` | `{AppContext.BaseDirectory}/App_Data/documents` |
| `Storage:MaxUploadBytes` | 25 MiB default; configurable up to 100 MiB |

Only the local provider is implemented. No File Server connection was added. New upload types are `.doc`, `.docx`, `.xls`, `.xlsx`, and `.pdf`; extension/MIME checks, PDF/OLE signature checks, OOXML package validation, safe filename handling, and the upload-size limit are enforced in the shared service path for templates and Project Documents. Legacy image, TXT, and CSV rows remain readable. The API mediates downloads and previews; it returns no local path or public file URL. Upload does not represent verification of an external signature, and no in-system document approval workflow was added.

## Persistence and database status

The additive source migration is `20261110170000_AddOrganizationDocumentTemplateStorageMetadata`. It adds nullable `Document.OrganizationId` and `Document.SourceTemplateVersionId`, provider ID/key/SHA-256 metadata fields with lengths 64/512/64, two indexes, and restrictive foreign keys. No backfill, reset, seed, or destructive database operation was performed. The migration source was reviewed; generated migration SQL and the database workflow were not run. A Product Owner-provided runtime trace for `GET /api/documents` reports SQL Server error 207 (`Invalid column name 'OrganizationId'`, repeated), showing that the API's connected schema does not expose the column required by the current model. The target identity and migration history were not independently checked.

| Required database evidence | Result |
|---|---|
| EF model synchronized | **No / pending.** A pre-existing snapshot inconsistency involving `DraftApprovalConfigurationJson` was corrected before the latest offline gate. That gate still reported `EF Model synchronized: No`; no successful confirming gate is available. |
| Pending migration count/names on the target database | **Not run / unknown.** Live database history was not queried. The Sprint 1 migration source is identified above; additional Sprint 2 and Sprint 3 source migrations are listed in their sections below. |
| Target environment/database identity verified | **No.** No database connection was opened. |
| Backup | **Not requested.** No Development/UAT database write was performed. |
| Migrations applied | **None.** |
| Seed mode/idempotency | **Not run / not applicable.** |
| Physical schema verified | **Not run.** The supplied runtime trace reports missing `Documents.OrganizationId`; the broader schema and target identity remain unverified. |
| Persistence tests | **Not run.** |

No database reset was performed. Do not treat the source migration or model snapshot as proof of live schema synchronization.

## Verification and remaining blockers

| Check | Result |
|---|---|
| React typecheck | Passed. |
| Focused frontend regressions | `document-center-parity.test.tsx`, `project-phase-gate-side-sheet.test.tsx`, and `audit-phase-gate-localization.test.tsx` passed **8/8** with Vitest 4.1.12. The first attempt failed before collection because Windows sandbox TEMP denied Vite's temp-cache rename (`EPERM`); one retry with `TEMP`/`TMP` redirected to the approved local temp directory passed. No source changes occurred between attempts. |
| API build | Performed once after correcting CS0136 in `PortfolioManagementService.cs`; succeeded with 360 warnings and 0 errors. No retry was run. |
| Focused API/storage tests | Tests were authored, including provider contract/registration and scope/security regressions; test bodies were not run. |
| Authenticated API/browser UAT | No authenticated check was performed by the agent. The Product Owner supplied an API trace showing `GET /api/documents` fails with SQL error 207 for missing `Documents.OrganizationId`; caller identity and target database were not established. Browser UAT remains not run. |
| Document Center Vite/i18n runtime | Product Owner console logs show the `wss://localhost:4100` HMR connection failing and `DocumentCenter` throwing `useI18n must be used within I18nProvider`. Static source review confirms `I18nProvider` wraps `AppRoutes`; no separate provider/module was found. A stale HMR module graph after the socket failure is a plausible but unverified explanation. No live Vite listener was available for reproduction; no source fix or runtime test was performed. |
| Project Documents data-preservation runtime check | Not run. |
| Product Procedures Framework v12 | Not found in the repository/workspace search. No document-control requirements were inferred from it. |

The latest offline EF gate built without compile errors but emitted 8 `NU1900` package-audit connectivity warnings and reported the model unsynchronized. The API build succeeded once, but focused API/storage tests and a confirming model gate remain pending; technical Definition of Done is **not complete**. The Master UAT Checklist contains DOC-01-DOC-12; the supplied API and browser console failures are recorded separately from checks not performed by the agent.

## Files changed

- Backend documents: `DocumentController.cs`, `DocumentContentTypes.cs`, `DocumentDtos.cs`, `DocumentService.cs`, `IDocumentService.cs`, `DocumentUploadLimits.cs`, `DocumentUploadRequestSizeLimitAttribute.cs`, `Document.cs`, and `DocumentVersion.cs`.
- Storage: `IStorageProvider.cs`, `LocalStorageProvider.cs`, and `DocumentsModuleRegistration.cs`.
- Persistence/scope: `AppDbContext.cs`, `ManagementScopeResolver.cs`, `ManagementScope.cs`, `AppDbContextModelSnapshot.cs`, and the new migration plus designer listed above.
- Focused API regressions: `ManagementScopeResolverTests.cs`, `DocumentTenantBoundaryTests.cs`, `LocalStorageProviderContractTests.cs`, and `DocumentsStorageProviderRegistrationTests.cs`.
- React: `apps/web-react/src/api/endpoints.ts`, `ProjectDocumentsTab.tsx`, `projects/api/documents.ts`, `lib/perspective.ts`, English/Arabic message files, and `document-center-parity.test.tsx`.
- Documentation: `docs/modules/DOCUMENT-MANAGEMENT-CORE.md`, `docs/quality/MASTER-UAT-CHECKLIST.md`, `docs/architecture/INTEGRATION-ARCHITECTURE.md`, `docs/architecture/NEXUS-PMO-ARCHITECTURE.md`, and `docs/architecture/module-catalogue.md`.

## Sprint 2 — Organization Phase Gate Configuration (2026-11-10)

**Status:** Phase Gate configuration source and UI work are present. The additive migration source `20261110180235_AddOrganizationPhaseGateConfiguration` and its designer are generated, and the EF model snapshot was generated with the Phase Gate entities. The DesignTime project build succeeded with 8 `NU1900` NuGet audit connectivity warnings, followed by successful `dotnet ef migrations add --no-build`. The final EF model/synchronization gate, API build, tests, SQL script generation, database application, and authenticated Product Owner UAT remain deferred. No migration was applied; the configuration is not deployed or runtime-verified.

### Delivered in source

Organization Settings now has a Project Lifecycle Phase Gate Requirements surface for configuring the server-catalogued **Pre-Initiation → Initiation** transition. Governance / Document Center remains the only template catalog and upload surface. The picker uses existing template/version records and displays their name, category, and version; requirements can be added, removed, and set to Mandatory or Optional. No project checklist or phase-transition workflow was added.

The configuration API is:

| Operation | Route | Contract |
|---|---|---|
| List supported transitions | `GET /api/organizations/{organizationId}/phase-gate-requirements/transitions` | Server-owned transition identifiers and EN/AR labels. |
| Read current configuration | `GET /api/organizations/{organizationId}/phase-gate-requirements?sourcePhase=...&targetPhase=...` | Current configuration version/token and pinned requirements. |
| Read immutable history | `GET /api/organizations/{organizationId}/phase-gate-requirements/history?sourcePhase=...&targetPhase=...` | Prior configuration versions and their requirement snapshots. |
| Save a new configuration version | `PUT /api/organizations/{organizationId}/phase-gate-requirements?sourcePhase=...&targetPhase=...` | Expected version token plus the replacement Mandatory/Optional requirement set. |

Each configuration has a stable ID per Tenant/Organization/transition and appends a new configuration version on successful save. Requirements pin Template ID and Template Version ID and snapshot the displayed name/category/version label. Duplicate Template IDs in one transition are rejected. New or changed template references require an active, unarchived, same-Tenant/same-Organization Document Center template version. An unchanged historical reference remains valid after archive/supersession and its stored metadata snapshot is carried forward. The current rowversion token rejects stale updates. Each successful version writes a `VersionCreated` audit event; an audit failure rolls the configuration write back.

### Permissions and Sprint 3 contract

Reads require canonical `Settings.View` and `Document.View`, plus Organization Settings read/manage access for the target Organization. Saves require canonical `Settings.ManageTenantConfiguration` and `Document.View`, plus an Organization Settings manage grant. Service and endpoint policies fail closed under Active Deny. No duplicate permission key, role-name fallback, or automatic grant was introduced. Optional remains ordinary configuration; governance waivers and exceptions remain separate.

Sprint 3 can retrieve the current effective requirements or immutable configuration history and pin the returned Configuration Version ID with exact template/version references and Mandatory/Optional flags. A later Organization settings edit creates a new version and does not rewrite earlier configuration snapshots. Project-level checklists, transition enforcement, phase changes, dashboards, and external File Server integration remain future work.

### Sprint 2 files

- Organization API/domain/application: `apps/api/src/NEXUS.Modules.Organization/Api/PhaseGateRequirementsController.cs`, `Application/PhaseGateConfigurationContracts.cs`, `Application/PhaseGateConfigurationService.cs`, and `Domain/OrganizationPhaseGateConfiguration.cs`.
- Document Center integration/persistence source: `apps/api/src/NEXUS.Persistence/ApprovedTemplateCatalog.cs`, `PhaseGatePersistenceRegistration.cs`, `Configurations/OrganizationPhaseGateConfigurationMapping.cs`, Phase Gate `DbSet`/registration updates in `AppDbContext.cs`, `NEXUS.Modules.Organization/ModuleRegistration/OrganizationModuleRegistration.cs`, and `NEXUS.Api/Program.cs`, plus `Migrations/AppDbContextModelSnapshot.cs`.
- React: `apps/web-react/src/features/organization/PhaseGateRequirementsCard.tsx`, `features/organization/api/phase-gate-requirements.ts`, `features/administration/SettingsWorkspaces.tsx`, `apps/web-react/src/api/endpoints.ts`, and `apps/web-react/src/i18n/messages/en.json` / `ar.json`.
- Regression coverage authored: `OrganizationPhaseGateConfigurationTests.cs`; it has not been executed.
- Documentation updated: this report and `MASTER-UAT-CHECKLIST.md`.

### Persistence and technical verification status

| Required evidence | Sprint 2 result |
|---|---|
| EF model synchronized | **Deferred / No.** The model snapshot was generated by EF and now contains the Phase Gate entities; the final model synchronization gate was not run. |
| Phase Gate migration source | **Generated.** `20261110180235_AddOrganizationPhaseGateConfiguration.cs` and `20261110180235_AddOrganizationPhaseGateConfiguration.Designer.cs` are present. The DesignTime project build succeeded with 8 `NU1900` NuGet audit connectivity warnings, then `dotnet ef migrations add --no-build` succeeded. No SQL script was generated or applied. |
| Pending migration count/names on Development | **Not run / unknown.** No database connection or history query was made. |
| Target environment/database identity verified | **No.** No database connection was opened for Sprint 2. |
| Backup | **Not requested.** No Development/UAT write was attempted. |
| Migrations applied | **None.** |
| Seed mode/idempotency | **Not run / not applicable.** |
| Physical schema verified | **Not run.** |
| API build | **Not run.** The successful build was for the EF DesignTime project, not the API. |
| Focused API/SQLite regression tests | Authored; **not run** under the deferred consolidated-verification instruction. |
| React typecheck/focused tests | **Not run for Sprint 2.** Sprint 1 results above are historical and were not repeated. |
| Authenticated API/browser UAT | **Not run.** |

`PM_Procedures Framework-v12.docx` was unavailable in the agent workspace during Sprint 1 and no document-control requirements are inferred from it. The Sprint 1 EF-model mismatch, missing migration application, unrun API/storage tests, unverified target/schema, and pending browser UAT remain open. The consolidated round must review the generated Phase Gate migration/snapshot, verify the complete model/schema against an explicitly verified Development target, run the one final API build and focused regressions, and then hand over to Product Owner browser UAT. **Technical Definition of Done is not complete.**

## Sprints 3 and 4 - Project Lifecycle, Phase Gates, and Compliance (2026-11-10)

**Status:** Sprint 3/4 source implementation, focused regression coverage, and documentation are complete in this worktree. At the Product Owner's follow-up, the API build was run once after the reported compile fix and succeeded with warnings. API/backend tests, consolidated React checks, EF synchronization, database work, and authenticated runtime/browser verification remain **Deferred**. This source handoff is not a Production-readiness claim.

### Sprint 3 source delivered

- Newly created projects use the server-owned `pre-initiation` phase. Project `Status` remains a separate field; create/update DTOs do not accept a client phase override. Existing project rows are not backfilled.
- Project Phase Gate readiness reads Organization Phase Gate requirements from the existing internal configuration reader and creates a per-project immutable snapshot only when configuration exists. The snapshot pins the configuration and exact Template ID/Version ID plus display metadata. A missing configuration returns `NotConfigured`, creates no snapshot, and blocks only the configured phase-change action.
- Project evidence references a specific project document and `DocumentVersion`. The existing shared `IDocumentService`/`Document`/`DocumentVersion` and `IStorageProvider` are reused; no duplicate document store is introduced. Evidence must match project, tenant, organization, and the snapshot's exact template/version and have an accessible stored object.
- Phase change re-evaluates persisted evidence on submission and compares the expected Project rowversion. Mandatory missing/invalid evidence blocks with structured details; Optional evidence does not block. The successful transition is written to the existing Audit Trail with phase/config/evidence context. `ProjectPhase` and `ProjectPhaseGateEvidence` map to the Project Management audit module and have EN/AR labels. There is no separate transition audit store.
- All submitted project evidence is assumed to have the necessary external approvals. The application does not verify an external signature, create an approval workflow, or interpret an upload as independent approval evidence.

### Sprint 4 source delivered

- Program and Portfolio compliance endpoints provide counts, mandatory/optional completion, phase distribution, readiness vs. transition status, filters, paging, and project drill-down.
- The denominator is Ready + Incomplete + NotConfigured. NotConfigured is not counted compliant; NotApplicable is reported separately and excluded. Percentage is null for an empty applicable denominator. Archived and Cancelled projects are excluded.
- Program aggregation uses the authorized Program's projects. Portfolio aggregation includes projects through scoped Programs and directly assigned Portfolio projects once, subject to the existing tenant/management scope.
- Requirement names/details and missing-template filtering are returned only when effective `Project.View` and `Document.View` are both present; otherwise detail is suppressed and a missing-template filter is Forbidden.

### Authorization matrix and governance

| Operation | Canonical permissions and scope |
|---|---|
| Read project checklist/readiness | `Project.View` + `Document.View`; authenticated Tenant, management, and Project scope; Active Deny applies. |
| Change phase | `ProjectPhase.Change` (project-scoped) + `Document.View`; valid caller project scope, current rowversion, and server gate validation; no automatic grants. |
| Link/unlink evidence | `Project.View` + `Document.Edit` + `Document.View`; exact project/document/version and Tenant/Organization scope; Active Deny applies. |
| View dashboard missing-document detail/filter | `Project.View` + `Document.View` in addition to existing Program/Portfolio endpoint authorization and management scope. |

No broad role-name authorization fallback or duplicate document permission is introduced. Ordinary Optional configuration remains separate from governance waivers and exceptions. The PM Procedures Framework v12 document remains unavailable in the workspace; no source requirements were invented.

### New and modified files

- Lifecycle backend: `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs`, `IProjectPhaseGateReadinessService.cs`, `ProjectPhaseGateConfigurationReader.cs`, `Application/DTOs/ProjectPhaseGateDtos.cs`, `Domain/ProjectLifecyclePhases.cs`, `ProjectPhaseGateSnapshot.cs`, `ProjectPhaseGateRequirementSnapshot.cs`, and `ProjectPhaseGateEvidence.cs`; project mapping/service and phase-gate API/controller integration are also modified.
- Compliance backend: `apps/api/src/NEXUS.Api/ProgramManagement/PhaseGateComplianceDtos.cs`, Program Management service/interface/controller, and Portfolio Management service/interface/controller.
- React: `apps/web-react/src/features/projects/components/ProjectPhaseGateSideSheet.tsx`, `features/projects/api/project-phase-gates.ts`, `features/projects/ProjectDetailPage.tsx`, `features/projects/ProjectDocumentsTab.tsx`, `features/delivery/PhaseGateCompliancePanel.tsx`, `features/delivery/api/phase-gate-compliance.ts`, EN/AR message files, and permission definitions.
- Regression source: `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectPhaseGateLifecycleTests.cs`, `PhaseGateComplianceAggregationTests.cs`, `apps/web-react/src/test/project-phase-gate-side-sheet.test.tsx`, and `audit-phase-gate-localization.test.tsx`. These are authored but not executed.
- Data/model integration: `AppDbContext`, EF snapshot, and module/API registrations include Phase Gate entities/services. The source-only, non-destructive Sprint 3 migration `20261110190107_AddProjectPhaseGateLifecycle` and its designer add Project phase-gate snapshot, requirement snapshot, and evidence persistence. It is **not applied**. The Sprint 1 `20261110170000_AddOrganizationDocumentTemplateStorageMetadata` and Sprint 2 `20261110180235_AddOrganizationPhaseGateConfiguration` migration sources also remain unapplied. Migration history, model synchronization, and physical schema remain unverified; no database operation was performed.
- Documentation: this report, `DOCUMENT-MANAGEMENT-CORE.md`, and `MASTER-UAT-CHECKLIST.md` are updated in place.

### Sprint 3/4 verification and carried blockers

#### Source migration scaffolding exception (not final verification)

Only to produce the source migration, an isolated `NEXUS.Persistence.DesignTime` build was attempted before the source fixes and failed with 3 compile errors. A subsequent EF scaffold build failed on 2 mapping errors. After the fixes, `dotnet build apps/api/tools/NEXUS.Persistence.DesignTime/NEXUS.Persistence.DesignTime.csproj --no-restore -m:1` succeeded with 8 `NU1900` NuGet audit connectivity warnings, then `dotnet ef migrations add AddProjectPhaseGateLifecycle ... --no-build` generated `20261110190107_AddProjectPhaseGateLifecycle`. An unnecessary project-key drop/recreate was removed from the source migration; the existing `AK_Projects_TenantId_Id` constraint name is preserved. No SQL script was generated and no database action was performed. This scaffolding work is not the final API build or final EF synchronization gate.

`dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1` succeeded once with **360 warnings and 0 errors** after fixing CS0136 in `PortfolioManagementService.cs`. Warnings include NU1900 NuGet vulnerability-index connectivity warnings and IKVM class-format/missing-class warnings from `NEXUS.Modules.ProjectPlanning.MppAdapter`. No retry was run. `npm run typecheck` completed with exit code 0. The focused frontend regressions `document-center-parity.test.tsx`, `project-phase-gate-side-sheet.test.tsx`, and `audit-phase-gate-localization.test.tsx` passed **8/8** after redirecting TEMP/TMP to the approved local temp directory; the first attempt failed before test collection with a sandbox TEMP rename `EPERM`. API/backend/storage tests, full frontend suite, EF model synchronization gate, database inspection or migration application, and Product Owner browser UAT remain **Deferred**. The Product Owner supplied a runtime trace showing `GET /api/documents` fails with SQL error 207 for `Documents.OrganizationId`; the agent did not independently authenticate or identify that runtime's database target. The separate Vite HMR/i18n console failure remains unverified as recorded above.

| Check | Result |
|---|---|
| Tests added and focused frontend execution | Focused API regression source added; **not run**. Three focused frontend files passed **8/8** after one pre-collection temp-cache `EPERM` attempt and a single retry with TEMP/TMP redirected. |
| Final API build | **Succeeded once:** 360 warnings, 0 errors, after correcting CS0136. No retry was run. |
| API/backend/storage tests | **Deferred / Not run.** |
| React typecheck | **Passed once:** `npm run typecheck` (`tsc -b`) exited 0. |
| Full frontend suite | **Deferred / Not run.** The three focused frontend regression files above passed 8/8. |
| EF model synchronization | **Deferred / No.** Sprint 1 snapshot/model inconsistency remains unconfirmed; Sprint 2 migration source remains unapplied. |
| Pending migration names on Development / target identity / physical schema | **Not run / unknown.** No DB connection or history/schema query was performed. The supplied runtime trace reports missing `Documents.OrganizationId`; broader physical schema remains unverified. |
| Migrations applied / reset / seed | **None / Not run.** No database operation was performed. |
| Authenticated runtime/API/browser verification | **No agent-run authenticated check.** Product Owner supplied the `/api/documents` SQL error trace; caller identity/target database were not established. Browser UAT remains pending. |
| Product Procedures Framework v12 | **Unavailable** in the workspace; no requirements were inferred. |

The consolidated round must review the complete EF model and pending migration sources against an explicitly verified Development target, run the agreed focused/full test suites and React checks, and then hand over to Product Owner browser UAT. The earlier API build succeeded once. Offline Model/Snapshot synchronization is now confirmed, but Development schema/history, unapplied Sprint 1–4 migrations, unrun API/storage test bodies, and final P1 build status remain blockers. **Technical Definition of Done is not verified.**

Clean Code & Legacy Cleanup remains the final cleanup sprint after all Documents functionality is complete. It was not started here.

## P1 Document Categories Management (2026-11-10)

**Source status: implemented; technical and Development verification remain pending.** Governance / Document Center now manages categories using the existing `DocumentCategory` entity and source of truth. No second catalog or template upload path was introduced.

### Category model and document integration

- The existing entity retains its stable ID and English `Name` for compatibility, with organization ownership, Arabic name, normalized English/Arabic names, and active status added. Tenant-global legacy rows retain their original IDs and remain visible as read-only historical categories; the migration does not guess their Organization mapping.
- New categories require an active, authorized Organization and both English and Arabic names. Duplicate normalized names are checked independently within each Organization, with filtered unique indexes protecting persistence. Categories are deactivated instead of deleted. Renames and status changes keep existing template/document foreign references intact; usage counts are returned where the caller has authorized scope. The existing Document Center has no template metadata update endpoint, so this sprint does not add a template category-reassignment flow; stable category IDs remain referenced by existing templates.
- The management API is `/api/document-categories`: list/search, create, detail, update, and status (`PATCH .../{id}/status`). Management actions require their matching granular permission. The existing `GET /api/documents/categories?organizationId=...` remains the template/project-document dropdown and returns active categories for that exact authorized Organization only. Legacy tenant-global and inactive categories are not eligible for new template or project-document assignments.
- Category remains display/filter metadata. It does not change Phase Gate eligibility, Mandatory/Optional requirements, or authorization.

### Permissions and protected Admin

Four canonical keys are registered under Governance / Document Categories: `DocumentCategory.View`, `DocumentCategory.Create`, `DocumentCategory.Edit`, and `DocumentCategory.ActivateDeactivate`. Each is Tenant-scoped and assignable to selectable tenant Role Categories through existing role-grant rules. API policies and service checks use those exact keys; dropdown access continues to use `Document.View`.

All four category keys are flagged for the protected tenant Admin baseline, so the existing canonical baseline/reconciliation provisions them to protected Admin role members and keeps member Deny overrides normalized according to the existing policy. The change does not grant `Document.View`, infer access from an `admin` display name, or use `pmo.admin` as an authorization fallback. Catalog, role-grantability, baseline, seed/reconciliation, and endpoint-policy regression sources were added. The backend test project failed compilation before test execution; existing protected Admin grants and effective access on the Development database remain unverified.

### Persistence and verification status

The non-destructive source migration `20261110210000_AddOrganizationManagedDocumentCategories` and its designer are present. It adds nullable Organization/Arabic metadata, active state, normalized names, a tenant/Organization composite foreign key, and filtered unique indexes while preserving existing category IDs/references and backfilling normalized English text. It is **not applied**. The offline EF Model/Snapshot gate now reports synchronized **Yes** with **21 repository migration sources**; this does not verify Development migration history or physical schema. The read-only database gate failed with `The target principal name is incorrect. Cannot generate SSPI context` before it could verify SQL Server/database identity, migration history, schema, category rows, or protected Admin grants. No SQL migration script for the category migration was generated/reviewed, no database write was performed, and authenticated API behavior remains unverified.

Focused regression source was added for bilingual create/edit, same-Organization duplicate rejection and cross-Organization name independence, exact active dropdown filtering, inactive-category rejection for new template uploads, reference stability through rename/deactivation, category usage, legacy read-only rows, cross-Organization/Tenant assignment rejection, Tenant-scoped category-label resolution for existing document detail/list DTOs, exact category permission checks, and Active Deny. Protected Admin seed/reconciliation tests and endpoint route/policy contract tests were also updated. The category parity test covers category-only page/query behavior and granular action visibility; it passed **8/8**. The API backend test project failed compilation before test bodies ran because existing Finance/Management test sources do not compile; category backend regression execution is therefore blocked. A prior React typecheck completed successfully before the final test-mock-only edit; a later typecheck attempt has no result because tool follow-up was blocked by external telemetry review. One final API build was started, but tool follow-up was blocked when Visual Studio telemetry was detected; its final result is unknown and it was not retried. No database write or authenticated browser verification was performed. `docs/quality/MASTER-UAT-CHECKLIST.md` contains category CRUD, scope, permissions, baseline, reference-preservation, and localization scenarios; Product Owner UAT remains pending.

The carried blockers remain: Development SQL Server identity/history/schema, migration SQL review/application, and actual Admin grants are unverified; backend tests did not execute; the final P1 API build result is unknown due to the telemetry review block; and Product Owner authenticated browser UAT remains outstanding. The Master UAT status must not be read as evidence of Development readiness or verified Admin permission recovery.

## P0 Final Sprint - Database Recovery, Integration Verification, and Clean Code (2026-11-10)

**Status: NOT CLOSED.** The offline model gate and final API build succeeded, but Development database identity/history/schema could not be read, backend tests did not reach execution, and legacy `.doc` / `.xls` validation does not yet inspect the actual Office document streams. No migration was applied and no authenticated browser UAT was performed.

### EF and database results

- The repository EF gate `model` action completed successfully for `NEXUS.Persistence.AppDbContext` / `NEXUS.Persistence`: **EF Model synchronized: Yes** and **21 repository migration sources** after the category migration source was added. This is an offline Model/Snapshot check only; it does not establish database synchronization.
- A source SQL script was generated offline from `20261109135500_AddDraftAnnualBudgetApprovalConfigurationJson` through `20261110190107_AddProjectPhaseGateLifecycle` and inspected. It contains the known Sprint 1, Sprint 2, and Sprint 3 migration operations: additive document metadata columns, new Phase Gate tables, indexes, keys, and foreign keys. No `DROP`, `DELETE`, `TRUNCATE`, or existing-column alteration was present. This script is a source review, not confirmation that these migrations are pending on Development.
- The repository's verified-target `inspect` action failed while opening SQL Server TCP at `localhost:1433` with **The target principal name is incorrect. Cannot generate SSPI context**. It failed before reading `SERVERPROPERTY('ServerName')`, `DB_NAME()`, database ID, `__EFMigrationsHistory`, or physical schema. Therefore actual target identity, applied/pending migration list/count, and schema synchronization are **unknown**. The Product Owner's `/api/documents` trace reports missing `Documents.OrganizationId`, but does not independently establish the API's target identity.
- **Database operations:** no migration applied; no reset or manual SQL; pending migration count unknown; backup **Not requested** (Development); seed mode **Not run**; persistence tests against the target **Not run**. The known source migrations are `20261110170000_AddOrganizationDocumentTemplateStorageMetadata`, `20261110180235_AddOrganizationPhaseGateConfiguration`, and `20261110190107_AddProjectPhaseGateLifecycle`.

### Integrated verification

| Check | Result |
|---|---|
| Offline EF Model gate | **Passed:** synchronized; 20 repository migration sources; DB connection not opened. |
| Migration SQL source review | **Generated and inspected:** additive operations only; not tied to a verified Development pending set and not executed. |
| Focused backend tests | **Blocked before test execution.** Default output compilation first hit DLL-in-use errors. With a temporary output directory, application projects including `NEXUS.Api` compiled, but the test project failed to compile in unrelated Finance test sources: missing `PaymentRequestStatuses`, `ITenantFiscalYearService`, and `FiscalYearCalendarService`; a missing `QueueActionableStepsAsync` cancellation-token argument; and missing `AddCanonicalFinancialActionPolicies`. The baseline status of these unrelated test-source errors was not independently established. No backend test body ran. |
| React typecheck | **Passed once** before the final test-mock-only edit. A subsequent attempt had no result because tool follow-up was blocked by external telemetry review; no retry was made. |
| Focused React tests | **Passed:** category parity test 8/8 after mock corrections. Earlier Sprint 3/4 focused checks also passed 8/8 across three files. |
| Full frontend suite | **Not run.** |
| Previous consolidated API build | **Passed once:** 360 warnings, 0 errors before this P1 category change. |
| P1 final API build attempt | Started once with a temporary output directory; tool follow-up was blocked after Visual Studio telemetry was detected. Final build result is unknown; no retry was made. |
| Authenticated API/browser verification | **Not performed.** Product Owner browser UAT remains pending; the reported Vite HMR/i18n runtime issue remains unverified. |

### Security, file validation, and cleanup

- Final source inspection hardened PDF structure checks and DOCX/XLSX package/main-part XML validation. Legacy `.doc` / `.xls` currently receive only a Compound File header/sector consistency check; the code does **not** confirm a WordDocument/Workbook stream. A vetted CFB parser was not present in the available module dependencies. Required legacy Office content-type validation is therefore **not closed**; do not accept signature-only CFB checks as full document validation.
- Lifecycle review added a batch readiness check that the resolved management context belongs to the authenticated user and removed a redundant portfolio-resolution overload after checking its sole caller. Document validation changes and focused tests are present. No other safely removable dead implementation was confirmed; no speculative deletion or broad unrelated refactor was made.
- The source fix for the reported `PortfolioManagementService` CS0136 error is included; the final API build confirms the source now compiles. Physical Development compatibility remains blocked by the unverified database schema.

### Closure blockers and handoff

1. Restore authorized Windows-authenticated access for the repository EF gate, then re-run read-only identity/history/schema inspection for Development `Shady` / `NexusPmo`. Only after actual identity and migration SQL/schema review may validated pending migrations be applied; then verify migration history, physical objects, and preserved records.
2. Resolve the test-project compilation errors in the Finance test sources or establish a clean test-project build, then execute the focused Documents, Storage, Phase Gate, scope, and compliance regressions. The tests have not been run.
3. Add full legacy OLE stream/type validation for `.doc` and `.xls` using a vetted parser before claiming all allowed uploads are content-validated.
4. After database and test blockers are resolved, perform Product Owner browser UAT for the integrated flow and the reported HMR/i18n runtime issue.

The EF model result, source SQL review, React checks, and API build do not establish Development database readiness or Production readiness. **Technical closure remains NOT CLOSED.**

## P1 Resume - Project Documents GET 404 Recovery - 2026-11-11

### Confirmed root cause

`ProjectDetails -> Documents` called `GET /api/documents?projectId=<value>` with `project.id`, which is the internal `Project.Id`. The backend `ProjectPublicIdRoutingMiddleware` resolves every external `projectId` query value exclusively as `Project.PublicId`. When the internal id was supplied, the middleware could not find a project whose `PublicId` matched and returned the confirmed `404` with detail `The requested resource was not found.` before the request reached `DocumentController`.

The failure detail matched the middleware's `WriteProblemAsync` output, not `DocumentController`'s `The requested project was not found.` problem detail, which confirmed that the request never reached the document endpoint. No database, migration, authorization, upload, download, template, or Phase Gate defect was involved.

### Exact fix

Frontend only:

- `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`: `ProjectDocumentsTab` now reads both `projectId` and `project` from the outlet context. It passes the route/public `projectId` to `DocumentCenter.projectId` and keeps `project.id` as `internalProjectId`.
- `projectId` continues to drive document list and category filters, which the middleware translates to the internal id before controller execution.
- `internalProjectId` continues to drive upload `ProjectId` payloads and `forProjectId` template lookups, which are not translated by the middleware and must remain internal.

No backend, persistence, migration, authorization, or API contract change was made. Existing response shapes, permissions, Tenant/Organization/Project scope, Active Deny, protected Admin rules, uploads, downloads, templates, and Phase Gate integrations remain unchanged.

### API contract before/after

Before: frontend sent `GET /api/documents?projectId=<internal Project.Id>&...`, which the PublicId routing boundary rejected with `404`.

After: frontend sends `GET /api/documents?projectId=<Project.PublicId>&...`; the middleware resolves it to the internal id and forwards the request. `projectId`, `templatesOnly`, `includeArchived`, `page`, and `pageSize` remain compatible. An existing authorized project with zero documents returns `200 OK` with an empty paginated result.

### Authorization and scope impact

No authorization or scope behavior changed. Canonical `Document.View`, Tenant/Organization/Project isolation, management scope, Active Deny, and protected Admin rules remain enforced after public-id resolution. Missing, unauthorized, or out-of-scope projects retain their existing secure `403/404` outcomes; the middleware's non-disclosing `404` is preserved for invalid external project identifiers.

### Tests and build results

- React typecheck (`tsc -b`): passed.
- Focused React regression `src/test/document-center-parity.test.tsx`: 8/8 passed.
- The parity test was updated to distinguish public and internal project identifiers: route id `public-42`, shell id `internal-42`. It now asserts `projectId: "public-42"` for document filters and `forProjectId: "internal-42"` for template lookups.
- No backend changes were made, so no API build was required for this fix. Existing backend PublicId routing and scope behavior remains covered by `ProjectPublicIdRoutingMiddlewareTests`.

### Runtime verification status

Source and focused frontend verification passed. An authenticated request against the affected Development environment has not been performed by the agent, so the runtime defect is not claimed resolved until Product Owner UAT confirms `GET /api/documents?projectId=<public id>` returns `200` for an existing project with and without documents.

### Modified files

- `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`
- `apps/web-react/src/test/document-center-parity.test.tsx`
- `docs/quality/MASTER-UAT-CHECKLIST.md`
- `docs/modules/SPRINT-1-DOCUMENT-CENTER-FILE-STORAGE-REPORT-2026-11-10.md`

### Remaining blockers

- Authenticated Development runtime verification of Project Documents list/category/template flows.
- Consolidated EF/database verification remains separate and is not changed by this fix.

## P1 Resume 2 - Project Documents Categories 404 Recovery - 2026-11-11

### Confirmed root cause

`GET /api/documents/categories?projectId=bac2be0d-b4ea-4096-be86-de4086141498` returned 404 with detail "The requested project was not found." (service-level, not middleware).

Database inspection confirmed:
- Project internal ID: `427d73c1-bb63-4595-b603-09cd0b2edea5`
- Project public ID: `bac2be0d-b4ea-4096-be86-de4086141498`
- The project's portfolio (`E850D20A-...`) has no `OrganizationId`.

`DocumentService.GetCategoriesAsync` required `managementContext.Scope.ProjectOrganizationIds` to contain the project. Because the portfolio had no organization, that map did not contain the project, so the endpoint reported the authorized project as missing even though `ProjectIds` contained it.

### Exact fix

Backend only:

- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs`: `GetCategoriesAsync` now separates project authorization from organization availability. If the project is in the user's management scope but has no resolvable organization, it returns `200 OK` with an empty category list. Missing/unauthorized projects still return the existing non-disclosing 404, and inaccessible or mismatched organizations still return 404.

No frontend, database, migration, authorization policy, upload, download, template, or Phase Gate change was made. The previous frontend public-ID correction remains intact.

### API contract before/after

Before: `GET /api/documents/categories?projectId=<public id>` for an authorized project whose portfolio has no organization returned 404 "The requested project was not found."

After: the same request returns 200 with an empty category list. Projects missing from management scope continue to receive the existing secure 404.

### Tests and build results

- Documents module build (`dotnet build NEXUS.Modules.Documents.csproj`): succeeded, 0 errors (4 NU1900 warnings).
- Full API build: blocked by a locked `NEXUS.Modules.Documents.dll` held by a running `NEXUS.Api` process (MSB3021/MSB3027). No code error was reported before the lock failure.
- React typecheck and focused parity tests from the previous recovery remain passing; no frontend change was made in this step.

### Runtime verification status

Source and module-build verification passed. An authenticated Development request has not been performed by the agent; Product Owner UAT should confirm `GET /api/documents/categories?projectId=bac2be0d-b4ea-4096-be86-de4086141498` returns 200 with an empty list.

### Modified files

- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs`
- `docs/modules/SPRINT-1-DOCUMENT-CENTER-FILE-STORAGE-REPORT-2026-11-10.md`
- `docs/quality/MASTER-UAT-CHECKLIST.md`

## P0 Resume - Organization Resolution & Phase Gate Recovery - 2026-11-11

### Root cause and canonical resolution

1. **Is null Portfolio.OrganizationId valid?** Yes. `Portfolio.OrganizationId` is nullable by design. A portfolio may exist without an organization, in which case its projects have no portfolio-derived organization.

2. **How is effective Project Organization determined?** Through the canonical `IProjectOrganizationLookup` (`ProjectOrganizationLookup`), which resolves:
   - Program-linked projects via the Program's Portfolio.
   - Direct-Portfolio projects via the Portfolio.
   - Fallback to the Project's Department/Business Unit when the Portfolio has no Organization.
   - Final validation against an active, same-tenant Organization.

3. **What was actually changed beyond the previous Categories 404 fix?**
   - `ManagementScopeResolver.GetProjectOrganizationIdsAsync` now delegates to `IProjectOrganizationLookup` instead of its own portfolio-only batch logic, so Management Scope, Documents, and Phase Gates use the same Organization contract.
   - `ProjectPhaseGateReadinessService.ResolveOrganizationIdAsync` now delegates to `IProjectOrganizationLookup` instead of its own portfolio/program-only logic.
   - This adds the department/business-unit fallback consistently and removes duplicate Organization-resolution rules.

4. **Can an unresolved Organization bypass Phase Gate requirements?** No. When `ResolveOrganizationIdAsync` returns null, Phase Gate readiness is set to `NotConfigured`, no snapshot/configuration is created, and mandatory requirements cannot be marked satisfied. Empty category results are not treated as compliance.

5. **Are Program/Portfolio compliance figures correct?** The readiness service resolves Organization per project through the canonical lookup and uses `organizationByProject` for evidence and snapshot matching. Projects with unresolved Organization are reported as `NotConfigured`, not as ready or 100% complete.

6. **Which tests were executed and passed?**
   - `dotnet build NEXUS.Persistence.csproj`: 0 errors.
   - `dotnet build NEXUS.Modules.ProjectManagement.csproj`: 0 errors.
   - `dotnet build NEXUS.Api.csproj`: 0 errors (one final attempt after stopping the locked API process).
   - React typecheck and focused document parity tests from the previous recovery remain passing; no frontend change was made in this step.

7. **Was Development runtime verification completed?** No. Source and build verification passed. An authenticated Development request and Product Owner browser UAT remain pending.

### Modified files

- `apps/api/src/NEXUS.Persistence/Management/ManagementScopeResolver.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/ManagementScopeResolverTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/PortfolioManagementTenantReadTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/MeetingNotificationPublisherTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectPhaseGateLifecycleTests.cs`
- `docs/modules/SPRINT-1-DOCUMENT-CENTER-FILE-STORAGE-REPORT-2026-11-10.md`
- `docs/quality/MASTER-UAT-CHECKLIST.md`

### Security and scope impact

No authorization bypass was introduced. Tenant, Organization, Project, and management-scope enforcement remain unchanged. Missing or unresolved Organization now resolves consistently and cannot create false Phase Gate readiness or compliance.

## P1 Resume - Colored Document Library UX - 2026-11-11

Governance / Document Center presents a Document Library header, three scoped KPI cards, managed category folders, search and Grid/List controls, and a Recent Documents list. The 2026-11-11 Reference Data integration below supersedes the initial category-management tab: Reference Data manages the existing categories, while the Library consumes them. The primary upload action continues to open the existing approved-template registration. Project Documents keeps its compact interface and public/internal project ID distinction.

The overview reads active managed categories and displays their localized names, with a deterministic color from category identity. Total Documents and folder counts use the existing scoped document-list totals (including archived records); Active Templates uses the existing active-template total. Unknown or failed folder counts are displayed as unavailable rather than fabricated. Blue, purple and green distinguish KPI cards; Word, Excel and PDF use blue, green and red respectively, with text labels alongside color. Theme-aware colors, logical spacing, focus indicators and pressed states support light/dark and RTL/LTR layouts.

Selecting a folder applies the existing category filter; the search uses the existing document list API and matches localized category names in the folder browser. Grid/List switches preserve current filters; Clear filters resets search, category and status. Rows reuse existing detail, download, version, archive/reactivation and registration flows, with their existing permission checks. No backend, database, storage, lifecycle or authorization contract was introduced in this UX work. Phase Gate consumers continue using the existing template/version catalog.

### Files and verification

- React: `ProjectDocumentsTab.tsx`, `DocumentLibraryOverview.tsx`, `LibraryDocuments.tsx`, and English/Arabic message files.
- Regressions: `document-center-parity.test.tsx` and `document-library-overview.test.tsx` cover scoped query inputs, category-only and action permissions, category selection, search and view state, upload, project ID routing, supplied KPI values, stable folder styling, semantic file colors, version labels, detail and download callbacks.
- Focused Document Library and parity tests: **12/12 passed**. Existing Phase Gate and audit localization tests: **3/3 passed**. React typecheck: **passed**.
- Authenticated Product Owner browser UAT, actual scoped API data, language direction, visual contrast and responsive behavior remain **not run**. See LIB-01 through LIB-07 in the Master UAT Checklist.

The selected-organization list and totals use the existing API organizationId filter. Project documents whose own OrganizationId is null are excluded even when the project belongs to that organization; these counts do not represent every document belonging to its projects. Product Owner UAT must account for this limitation. Earlier database, runtime and legacy file-validation blockers in this report are unaffected by the frontend redesign; this section does not claim those items are closed.

## P1 Resume - Document Categories in Reference Data - 2026-11-11

### Outcome and architecture

Document Categories management is integrated into **Administration → Reference Data → Document Categories**. The screen reuses the existing `DocumentCategory` API and persistence model for Organization-scoped search, bilingual create/edit, activation state, and template usage counts. Categories are deactivated rather than deleted, preserving stable IDs and template/version history.

The approved Document Library remains the consumer surface: active categories supply the colorful folder grid and category filter. Register Approved Template uses the same catalog through `GET /api/document-categories/template-options?organizationId=` and its existing document-create/template-manage authorization; it does not require broad category-list permission. The permission-gated Manage Categories link opens the Reference Data route. Category metadata does not change Phase Gate rules; those requirements continue to select exact Template and Template Version IDs.

### Permissions and scope

The canonical keys remain `DocumentCategory.View`, `DocumentCategory.Create`, `DocumentCategory.Edit`, and `DocumentCategory.ActivateDeactivate`. UI actions and API endpoints use these exact operations, Tenant/Organization scope, and Active Deny. Source verification confirms the protected tenant Admin catalog/policy/seed baseline includes all four category permissions and grants them across selectable Role Categories. Live Development grants remain unverified. No role-name or broad `pmo.admin` authorization path is introduced.

### Files and verification status

- Reference Data page and route: `apps/web-react/src/features/administration/DocumentCategoriesPage.tsx`, `apps/web-react/src/features/administration/AdminPages.tsx`, and `apps/web-react/src/routes/AppRoutes.tsx`.
- Shared Library integration: `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`, `apps/web-react/src/features/projects/DocumentLibraryOverview.tsx`, `apps/web-react/src/features/projects/api/documents.ts`, and `apps/web-react/src/api/endpoints.ts`.
- Reference Data hub, navigation, and localization: `apps/web-react/src/features/administration/AdminPages.tsx`, `apps/web-react/src/features/administration/AdministrationHub.tsx`, `apps/web-react/src/routes/AppRoutes.tsx`, `apps/web-react/src/lib/navigation.ts`, and English/Arabic catalogs.
- Backend/API integration: `apps/api/src/NEXUS.Modules.Documents/Application/DocumentCategoryPermissions.cs`, `DocumentDtos.cs`, `IDocumentService.cs`, `Api/DocumentCategoryController.cs`, `Application/DocumentService.cs`, and `apps/api/src/NEXUS.Api/Program.cs`. Authorization regressions are in `DocumentCategoryAuthorizationContractTests.cs` and `DocumentTenantBoundaryTests.cs`.
- Focused React regression coverage: `apps/web-react/src/test/reference-data-parity.test.tsx`, `apps/web-react/src/test/document-center-parity.test.tsx`, and `apps/web-react/src/test/document-library-overview.test.tsx`.
- Targeted React suites: **34/34 passed**. `npm run typecheck`: **passed**.
- The single API build/test attempt failed during output copy with `MSB3021/MSB3027` because an API output DLL was locked by another process. No retry was made. API test execution is not verified.
- Read-only Development inspection using the `appsettings.Development` endpoint `tcp:localhost,1433` / `NexusPmo` failed before connecting with `TCP Provider error 0: Access is denied`. SQL Server identity, database ID, migration history, schema, categories, and Admin grants were not inspected. No database writes were made; no category data or template associations were changed.
- Protected Admin live grants and authenticated browser UAT remain pending. These results verify source/UI behavior only, not Development runtime behavior.

### Required persistence status

| Required field | Status for this resume |
|---|---|
| EF model synchronization | **Official offline model gate passed**; 21 repository migration sources. This does not verify the live database. |
| Pending migration count and names | **Not verified**; inspection failed before `__EFMigrationsHistory` could be read. Actual pending count/names are unknown. |
| Target environment/database identity | **Not verified**; the read-only attempt using `tcp:localhost,1433` / `NexusPmo` failed before connecting with `TCP Provider error 0: Access is denied`. Server identity and database ID are unknown. |
| Backup | **Not requested.** |
| Migrations applied | **None by this resume.** No SQL or migration script was generated or applied. |
| Required seed mode and idempotency | **Not run.** |
| Physical schema and category rows | **Not verified.** |
| Protected Admin grants | Source baseline verified; **live grants not verified** because the SQL attempt failed before connection. |
| Persistence tests | **Not run.** The API test/build attempt failed on output copy (`MSB3021/MSB3027`) before tests. |
| Blocker | TCP Provider error 0 (`Access is denied`) blocked read-only SQL connection before identity/history/schema checks. The API output-copy lock separately prevented API tests. |

### Follow-up - Category Save report - 2026-11-11

The Product Owner reported that Save Category does not work. Source tracing found no frontend request/response mismatch: the Reference Data form sends `organizationId`, `nameEn`, `nameAr`, and optional `description` to `POST /api/document-categories`; the endpoint returns a JSON `201 Created`, which the shared API client accepts. Focused page/API contract suites passed **25/25** and `npm run typecheck` passed. The first test invocation could not create a Vitest temporary file under the system Temp path (`EPERM`); rerunning with `TEMP`/`TMP` directed to `apps/web-react/.vitest-tmp` passed.

The API inserts the Organization, Arabic-name, active-state, and normalized-name columns introduced by `20261110210000_AddOrganizationManagedDocumentCategories`. The prior read-only Development check could not verify the database identity, migration history, or physical schema, and the implementation report records this category migration as unapplied. Therefore a missing schema migration is the leading explanation **if the live Save request returns HTTP 500**, but the exact runtime response and current database state remain unverified. No backend workaround, migration, or database write was made. The migration chain and Development target must be verified before a schema change; earlier source migrations also remain pending, so unrelated migrations must not be applied as a side effect.

Regression sources added: `apps/web-react/src/test/document-category-save.test.tsx` and `apps/web-react/src/test/document-category-api.test.ts`. Browser/API runtime UAT and Development schema verification remain blocked; do not mark category Save as runtime-fixed from source tests alone.

### Latest P0 recovery status - 2026-11-11

**P0 is not closed.** The official offline EF model gate passed against 21 repository migration sources, but no live persistence state could be verified. A read-only inspection was attempted using the Development endpoint from `appsettings.Development` (`tcp:localhost,1433` / `NexusPmo`) and failed before connection with `TCP Provider error 0: Access is denied`. SQL Server identity, database ID, `__EFMigrationsHistory`, physical schema, actual pending migration count/names, category rows, and live Admin grants therefore remain **not verified**. No SQL or migration script was generated or applied, no database writes were made, and backup was **Not requested**. Seed and persistence tests were not run.

The source request is `POST /api/document-categories` with the selected `organizationId`, trimmed `nameEn`/`nameAr`, and optional `description`. The live HTTP status/body and SQL exception were **not captured**; a 500 is not confirmed. Browser automation stopped because it could not confidently determine the current URL; a search of available repository and temporary logs found no category exception. The runtime root cause remains **unconfirmed**. Authorization and Organization source guards are intact, but live grants and scope are unverified. The configured `tcp:localhost,1433` / `NexusPmo` endpoint is configuration evidence only, not verified SQL identity. The prior 25/25 React/API-contract result and React typecheck are historical source evidence only, not authenticated Development evidence.

The migration reviewer found `20261110210000_AddOrganizationManagedDocumentCategories` additive: it backfills normalized English names and adds nullable `OrganizationId`, category fields, an index, and a foreign key. Target-specific SQL safety review is incomplete. This migration is last in the EF chain; if the target were at migration 17, the preceding three migrations would have to be applied in order first. The target is not verified to be at migration 17, so this condition must not be assumed. No migration was applied.


## P0 Resume - Document Category Save Recovery - 2026-11-11

### Confirmed issue

Clicking **Save Category** in Reference Data -> Document Categories did not reliably submit the form. The submit button relied solely on native form submission, which could be lost when the button was rendered in the SideSheet footer, and early-return validation failures were silent.

### Exact fix

Frontend only:

- `apps/web-react/src/features/administration/DocumentCategoriesPage.tsx`
  - Added an explicit `id` to the category form and `form` association on the Save button.
  - Added an `onClick` handler that prevents native submission and invokes `saveCategory` directly.
  - Made `saveCategory` accept an optional event and report a visible error when organization or permission preconditions fail.

No database, backend, or permission changes were made.

### Tests and build results

- React typecheck (`tsc -b`): passed.
- Focused test `src/test/document-category-save.test.tsx`: 1/1 passed.

### Runtime verification status

Source and focused frontend verification passed. Browser UAT remains with the Product Owner.

## P2 Resume - Localized Unsupported File Upload Error - 2026-11-11

### Confirmed issue

Unsupported uploads returned HTTP 400 with the technical validation message `VALIDATION_ERROR — File type is not supported.` The backend extension/content validation and security checks remain unchanged.

### Exact change

Backend:
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs`
  - Both unsupported-extension checks now return the stable code `UNSUPPORTED_FILE_TYPE` instead of the generic `VALIDATION_ERROR`, while preserving the existing message and HTTP 400 status.

Frontend:
- `apps/web-react/src/api/api-error-localization.ts`
  - Maps `UNSUPPORTED_FILE_TYPE` to the `unsupportedFileType` message key.
- `apps/web-react/src/i18n/messages/en.json` and `ar.json`
  - Added the exact localized messages for unsupported file types.
- `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`
  - Upload, version upload, and template-registration failures now show the localized unsupported-file message for `UNSUPPORTED_FILE_TYPE` and retain the generic action-failed message for other errors.

### Tests and verification

- React typecheck (`tsc -b`): passed.
- Focused tests: `api-error-localization.test.ts` passed (14 tests), including exact EN/AR unsupported-file messages.
- Document Library/Project Documents parity tests previously passed; no upload behavior rebuild was performed.
- API build: succeeded after stopping the locked `NEXUS.Api` process (warnings only: NU1900/package audit and no errors).

### UAT

See new `DOC-UPLOAD-01` and `DOC-UPLOAD-02` scenarios in the Master UAT Checklist.

## P1 Resume - Template Mandatory / Optional persistence - 2026-11-11

`Document.IsMandatory` is a template-level default designation collected during template registration. It is not an Organization Phase Gate requirement and does not change the Organization's configured Mandatory/Optional transition requirements or pinned template versions. A missing/unchecked upload field is Optional (`false`). The create path now only stores a true designation when the record is a template; document versions and existing category associations remain separate data.

Focused regression sources added:

- `apps/api/tests/NEXUS.Api.Tests/Integration/DocumentMandatoryPersistenceTests.cs` exercises Mandatory and Optional create/reload, changing each value and reloading, and the omitted-field Optional default using an isolated SQLite `AppDbContext`.
- `apps/web-react/src/test/document-center-parity.test.tsx` exercises the template upload checkbox payload for both checked and unchecked cases and verifies that details render the Mandatory/Optional value returned by the details API.

The focused React regression and React typecheck results, plus the blocked API persistence/build attempts, are recorded in the Main-agent verification update below. Source-level tests do not replace the required Development SQL Server verification or authenticated browser save/reload check. Database and runtime status remain **NOT VERIFIED** until those live checks succeed.

### Main-agent verification update - 2026-11-11

- Offline EF model gate passed with 22 repository migrations and no pending model changes. The reviewed additive migration is `20261111134000_AddDocumentIsMandatory`; generated SQL adds `[Documents].[IsMandatory]` as `bit NOT NULL DEFAULT CAST(0 AS bit)`. Existing rows therefore read Optional, and the migration has no category, version, audit, or document-row rewrite.
- Development database inspection was attempted with the requested Development environment and expected `Shady` / `NexusPmo` target. The repository gate failed with SQL Server `TCP Provider: Access is denied` before it could verify `SERVERPROPERTY`, `DB_NAME()`, database ID, migration history, or physical schema. No database write or migration application occurred. The actual target identity, database pending migration count/names, and column existence remain **unknown**.
- Database handoff fields: environment requested = Development; exact live target identity verified = **No**; applied migrations = **None**; pending database migration count/names = **Unknown** (history unavailable); physical schema = **Not verified**; backup = **Not requested**; seed mode/idempotency = **Not applicable to this additive migration; not run**.
- The focused React Document Center parity test passed (12 tests), and React typecheck passed. The focused API persistence test did not reach test execution: its build was interrupted after the dependency graph began compiling the unrelated MPP adapter's IKVM Maven imports with extensive class-version/missing-class warnings. SQLite source coverage is present, but persistence test execution remains **not verified**.
- The required single final API build attempt was started once with `dotnet build ...NEXUS.Api.csproj --no-restore -m:1`. It stalled in the MPP adapter's IKVM import generation with repeated warnings and no completion output; the attempt was interrupted (exit code 1) after the process stopped appearing in the process list. It was not retried. Authenticated browser save/reload and live Development SQL Server persistence remain **not verified**.

**Recovery status: NOT CLOSED.** The migration is source-only and must be applied and verified against the verified Development database before claiming persistence completion.

## P1 Resume - Project Documents Templates Tab library integration - 2026-11-18

### Scope and constraints

- Resumed the existing `LibraryForProjectId` integration for Project Details → Project Documents → Templates (compact). No database reset, manual SQL patches, or unrelated migrations. Existing/uncommitted worktree changes were preserved.

### Backend (actual)

- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentDtos.cs`: added `Guid? LibraryForProjectId` to `DocumentListQuery`.
- `apps/api/src/NEXUS.Modules.Documents/Api/DocumentController.cs`: added trusted management-scope validation for `LibraryForProjectId` (returns Not Found when the requested project is not in the current authorized project scope), alongside existing `ForProjectId`/`OrganizationId` checks.
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs` (`GetPagedAsync`): project-library lookup resolves the target project's Organization server-side from the trusted management context and, when authorized, returns eligible non-project-attached library records in the current tenant (`ProjectId == null`) for the resolved Organization (`OrganizationId == resolvedOrganizationId`) plus tenant-shared template library files (`IsTemplate && OrganizationId == null`). Search, category, status/archive filters, and pagination remain applied. The `LibraryForProjectId` scope is authoritative for this path. Existing `ForProjectId + TemplatesOnly` template-picker lookup and Phase Gate/template selection behavior remain unchanged.
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs` (`AccessibleQuery`): minimally extended to allow tenant-scoped shared template library records (`IsTemplate && OrganizationId == null && TenantId == scope.TenantId`) to satisfy `Document.View` read/detail/download access paths for eligible listed items, without weakening mutation permissions, Active Deny, tenant scoping, or cross-organization/project isolation.

### Frontend (actual)

- `apps/web-react/src/features/projects/api/documents.ts`: added `libraryForProjectId?: string` to `DocumentListQuery`.
- `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`: compact Project Documents → Templates tab queries `useProjectDocuments({ libraryForProjectId, search, page, pageSize: 20, categoryId, includeArchived: true })` and renders a grouped-by-Document-Category library view (EN/AR category names, category shelf/filter, search, pagination, loading/empty/API-error states with retry). Per-category counts are fetched across pagination via server-side `page: 1, pageSize: 1` count queries per active library category (`useQueries` + `documentApi.list`) respecting the current search filter, so shelf/group counts reflect eligible totals beyond the current page (uncategorized group, if present, falls back to current-page count). Items display name, file-type icon, version, status, approved-template/library-document distinction, description, and support view/download through existing actions/details; mandatory/optional designation remains available in document details. “Start from template” continues to use the existing `forProjectId + templatesOnly` query/flow. Project document uploads, existing document/project flows, and Phase Gate behavior are preserved. No automatic project attachment or file duplication.
- `apps/web-react/src/features/projects/LibraryDocuments.tsx`: library list/grid rendering updated to present approved-template/library-document distinction and metadata consistently for grouped library items.
- `apps/web-react/src/i18n/messages/en.json`, `apps/web-react/src/i18n/messages/ar.json`: added/retained `documents.*` keys for project library grouping, empty/error, and template/library labels (EN/AR).

### Verification (actual)

- React typecheck: `cd apps/web-react && npm run typecheck` (`tsc -b`) — **passed**.
- Focused frontend tests: `npx vitest run src/test/document-center-parity.test.tsx src/test/document-library-overview.test.tsx src/test/document-category-api.test.ts src/test/project-phase-gate-side-sheet.test.tsx` — **26 passed / 0 failed (4 files)**. `document-center-parity.test.tsx` includes compact Templates grouped-library coverage with accurate per-category counts across pagination (server count queries) and search/category filtering via `libraryForProjectId`, while preserving the existing `forProjectId + templatesOnly` template-picker contract assertions.
- Single final API build attempt (backend changed, no retries): `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1` — **succeeded (0 errors, 1 warning)**: `CS8602` in unrelated `NEXUS.Modules.ProjectManagement\Application\ProjectLifecycleCatalogService.cs(405,39)`.
- No database reset, manual SQL patches, migrations, or seed operations were performed.

### Limitations / remaining

- Eligibility as implemented includes org-owned non-project-attached library files and tenant-shared template library files in tenant context; tenant-shared ordinary non-template files are not included by the `LibraryForProjectId` path. Product Owner confirmation is required for any additional “eligible tenant-shared file” cases beyond this behavior.
- Per-category shelf/group totals are sourced from server count queries for known library categories; uncategorized items (if any) display the current-page group count.
- Automated coverage does not fully prove all required browser/runtime scenarios: exhaustive cross-organization/cross-tenant isolation attempts, unauthorized download denial, live permission combinations, refresh after Document Center library changes, RTL/LTR and Light/Dark responsive behavior, and end-to-end Phase Gate interactions. **Product Owner browser UAT is required.**

## P1 Resume - Document Categories Delete & Permission - 2026-11-18

### Implementation

- Added `DELETE /api/document-categories/{id}` under independent `DocumentCategory.Delete`, with service-side effective-permission/Active Deny checks and authenticated Tenant/Organization management-scope enforcement. Successful deletion is audited in the same transaction.
- Registered the sensitive permission in the canonical catalog with English/Arabic labels, a `DocumentCategory.View` prerequisite, Tenant scope, assignability to the eligible Tenant/Organization role categories, and the protected Tenant Admin baseline. Existing category grants remain intact.
- Permanent deletion is limited to unused categories. A tenant-wide validator includes soft-deleted/current Documents, Templates, Versions, Project Documents, child categories, phase catalog assignments, Organization/Project Phase Gate configuration/history snapshots, and evidence. Referenced categories return HTTP 409 with localized guidance to Deactivate. Deletion does not cascade to documents, files, templates, versions, or historical records.
- Added an authorized-only Delete action and confirmation dialog in Reference Data. The shared `documents` query family is invalidated after deletion, refreshing categories and related dropdowns. Corrected Project Documents upload category lookup to use its internal project ID, matching the API's scoped contract.
- Updated `DOCUMENT-MANAGEMENT-CORE.md` and added `P1-DCD-01` through `P1-DCD-05` to the Master UAT Checklist.

### Focused verification

- React: focused Document Categories, Roles & Permissions, Document Center, and Project Documents coverage passed **61/61** across 4 files. React typecheck passed.
- API: focused contract, scope, and deletion tests were added and the focused test assembly compiled. VSTest did not execute the tests: the runner failed to connect to its testhost after 90 seconds; a no-build retry with a longer connection timeout remained stalled and was stopped. API runtime test outcomes are **Not run**.
- Persistence module build passed after adding a direct `Microsoft.EntityFrameworkCore.Relational` reference for the serializable delete transaction. One NU1900 warning reported that the NuGet vulnerability feed was unreachable.
- Single final API build attempt: `dotnet build apps/api/src/NEXUS.Api/NEXUS.Api.csproj --no-restore -m:1` — **failed** at API output copying with MSB3021/MSB3027 after 10 retries because DLLs under `apps/api/src/NEXUS.Api/bin/Debug/net10.0` were locked by another process. The attempt was not retried. The build emitted the existing Project Lifecycle CS8602 warning, NU1900 vulnerability-feed warning, and MPP adapter class-version/missing-class warnings. No database reset, manual SQL patch, migration, or seed operation was performed; no schema change was required for this feature.

### UAT and handoff

Product Owner browser UAT remains pending. Run the scenarios in `docs/quality/MASTER-UAT-CHECKLIST.md` section “P1 Document Categories permanent delete and permission” before production use, especially persisted cross-scope denial, active-deny behavior, historical references, audit visibility, and dropdown refresh.

## P1 Template Deletion with Administrator Approval — 2026-11-19

### Workflow and authorization contract

- Template deletion is requested with a required reason and remains pending until an administrator decides. A second pending request for the same template is rejected. Request submission does not delete or archive the template.
- The canonical sensitive permissions are `Document.TemplateDeletion.Request`, `.View`, `.Approve`, and `.Reject`, grouped under Governance → Document Center. Request requires the new View permission plus `Document.View` and `Document.TemplateManage`; View requires `Document.View`; Approve and Reject each require the new View permission plus `Document.View`. All four are Tenant-scoped and assignable to selectable custom-role categories, are outside the protected Tenant Admin baseline, and are not automatically granted to roles.
- Approver resolution is owner-scoped. Organization-owned templates require an active same-Tenant Organization, an undeleted `OrganizationSettingsGrant.CanManage` grant, effective `OrganizationConfiguration.View` and `.Manage`, and the applicable deletion decision permission. Tenant-owned templates require effective `TenantAdministration.ManageScope` and the applicable deletion decision permission. The effective-permission service applies Active Deny and catalog dependencies; the requester is excluded from reviewer candidates and cannot decide their own request.
- Decisions require comments. Approval rechecks the request-time template/version snapshot and protected references. A changed template is marked stale without deletion. Templates with protected references are archived and their referenced content is retained; templates without protected references and their versions are hard-deleted, with storage cleanup recorded for processing. Request and decision history is retained separately.
- Pending notifications go to eligible reviewers; approval/rejection notifications go to the active requester. The notification types are `TemplateDeletionRequested`, `TemplateDeletionApproved`, and `TemplateDeletionRejected`, reference `TemplateDeletionRequest`, and use the template-name snapshot as the title with the reason or decision comment as dynamic body content. Delivery is in-app; static labels are localized in the React English/Arabic catalogs.

### Evidence and remaining verification

- Integrated source review covered the workflow/controller/contracts, permission catalog and scope resolver, dependency checks, storage cleanup worker, Document Center and Phase Gate guards, notifications, EN/AR UI, and request/inbox flows. The EF snapshot is already stale against concurrent Phase Gate/document work, so no template-deletion migration was generated.
- React verification passed: typecheck and the three focused Template Deletion suites (10 tests). The API focused test assembly now compiles through the opt-in `FocusedTemplateDeletionWorkflow` compile filter with project-reference building disabled. Its filtered runs aborted because VSTest could not connect to the testhost after 90, 240, and 60 seconds; no API test case executed. A separate unfiltered test-project build failed after 3m42s with 21 compile errors in unrelated Finance/security test sources (including missing `PaymentRequestStatuses` and `AddCanonicalFinancialActionPolicies`). Product Owner browser UAT remains pending.
- The one final API build attempt failed after 7m21s while copying built project references into the API output; MSB3021/MSB3027 reported that output DLLs were locked by another process. It emitted 452 warnings, mostly existing IKVM compatibility warnings. No successful API build is claimed.
- The fresh offline EF model gate build succeeded with two NU1900 feed warnings, then failed before model/snapshot comparison on `PaymentRequestApproverAssignment` shadow `TenantId` (`no property type was specified`). Source review found a dangling snapshot relationship to that entity without its model declaration; the current CLR mapping is typed. The snapshot also lacks `OrganizationPhaseGateConfiguration`, `ProjectPhaseGateSnapshot`, `ProjectPhaseGateEvidence`, `TemplateDeletionRequest`, and `Document.IsDeletionApproved`. An unregistered duplicate draft migration, `20261117200000_AddProjectLifecycleCatalogs.cs`, has a `Down` that drops lifecycle catalog tables and project catalog columns; it was preserved and not treated as canonical. The newest Product Owner trace shows `GET /api/documents?...` returned HTTP 500 because SQL Server reported `Invalid column name 'IsDeletionApproved'`. An earlier trace in this report recorded `Invalid column name 'OrganizationId'`; because neither trace identifies the verified environment/server/database, they may refer to different targets or different points in schema progression. Together they show runtime schema mismatch, but do not establish the active target or migration history. The stale snapshot and competing migration sources make a safe isolated migration impossible in this handoff.
- Required database completion fields: model synchronized **No**; pending migration count/names **Not verified**; target environment/database identity **Not verified**; backup **Not requested**; migrations applied **No**; seed mode/idempotency **Not run**; physical schema **Not run**; persistence tests **Not run**; blockers are the model-construction failure, stale snapshot, failed API build, unavailable API test results, and pending browser UAT. Read-only target inspection was unavailable because `NEXUS_EF_DEVELOPMENT_CONNECTION_STRING` and the active tool process's Development environment were not supplied; no database connection, write, reset, seed, or manual SQL was performed.
- The native 10-row Work Tasks Breakdown could not be created because no native `update_plan`/task-list action was exposed in the available Work tools; subagent assignments and integration were coordinated through the collaboration tools. This mandatory tracking item remains open. Workstream status: architecture/design/backend workflow/dependency rules/permissions/routing/UI/localization/docs are implemented and source-reviewed; React verification passed; API tests/build and database synchronization are blocked or unverified; Product Owner browser UAT remains.

### Latest resume audit — 2026-11-19

This is the current status and supersedes earlier same-day statements about the Development connection, model check, API tests, and API build.

**Completed**

- Restored the snapshot baseline from the latest migration registered in this source chain, 20261112171603_AddProjectPhaseGateEvidenceCompliance. It restores 17 entity mappings that were absent from the snapshot, including the typed PaymentRequestApproverAssignment.TenantId mapping and the existing payment and Phase Gate entities. The shadow-property model-construction error is gone; the current model is still unsynchronized.
- Verified the live target through the EF gate: Development, SQL Server Shady, database NexusPmo, DB_ID 5.
- Confirmed the lifecycle draft 20261117200000_AddProjectLifecycleCatalogs.cs has no designer or EF migration ID and overlaps the already applied 20261111173858_AddProjectLifecycleCatalogs. Its Down drops the two catalog tables and Project catalog columns. It remains untouched and unexecuted.
- Focused React tests passed: 3 files, 10 tests. React typecheck passed. The single final API build passed with 0 errors and one NU1900 feed warning.

**Blocked**

- The source migration assembly contains 28 migrations while dbo.__EFMigrationsHistory contains 30 rows. Four applied IDs have no source in the current migration directory: 20261113150128_AlignIntegrationAndProgramPhysicalSchema, 20261114232613_SeparateCanonicalRoleIdentityFromSystemClassification, 20261115214526_AddUserOrganizations, and 20261116093145_AddOrganizationOwnedRoles. The last applied migration source is missing, so the gate cannot load the last applied model.
- The gate reports two source migrations as pending: 20261026160000_AddPlannedPaymentLifecycle and 20261026170000_AddPaymentTenantScopedRelationships. They precede the initial baseline migration and are not a safe apply plan while the applied source chain is incomplete.
- Physical schema verification against the last applied model is No. The inspection observed dbo.TemplateDeletionRequests and dbo.Documents.IsDeletionApproved are absent. It also reported additional model/schema differences, including PaymentSettlements.Description expected length 500 vs actual 2000 and Projects.Status expected length 50 vs actual 64. No trusted pending SQL file was produced.
- No Template Deletion migration was scaffolded, no SQL hash was reviewed, and no migration was applied. Scaffolding from the incomplete source chain could include unrelated changes against the already advanced database.
- The focused API test assembly compiled with FocusedTemplateDeletionWorkflow=true, but VSTest aborted before executing any case: Win32Exception (5), Access is denied while opening the parent process. The fixture uses EF InMemory; no SQL Server persistence test ran.

**Database completion fields**

- EF Model synchronized: No.
- Pending migrations: gate reports 2 names above; 4 applied history IDs are absent from source.
- Development identity verified: Yes, Shady/NexusPmo, DB_ID 5.
- Backup: Not requested; Development backups are optional.
- Migrations applied: None.
- Seed mode and idempotency: Not run.
- Physical schema verified: No.
- Persistence tests: Not run; the focused API testhost aborted before cases, and no SQL Server persistence test ran.
- Remaining: recover and verify the exact missing applied migration sources, reconcile the two older pending migrations, rerun the gate, scaffold and review the Template Deletion migration, apply only a validated script, and verify SQL persistence/schema. Product Owner Browser UAT remains pending.

The native update_plan/todowrite action was not available in this session; a visible task checklist was maintained in the conversation.

## P0 EF Model Drift Repair & Template Deletion Migration - 2026-11-19

- Corrected mappings: PaymentSettlementConfiguration, PaymentObligationConfiguration, ProjectConfiguration, RoleConfiguration; added UserOrganization entity/configuration and UserOrganization navigation; added Document.IsDeletionApproved default.
- Generated migration: 20261119012805_AddTemplateDeletionApproval.
- Migration operations: ALTER TABLE Documents ADD IsDeletionApproved bit NOT NULL DEFAULT 0; CREATE TABLE TemplateDeletionRequests with PK and filtered unique index UX_TemplateDeletionRequests_Tenant_Template_Pending; supporting indexes.
- No unrelated drops/alters. SQL reviewed and applied to verified Development target Shady/NexusPmo (DB_ID 5).
- Post-apply: 31 repository migrations = 31 applied; pending 0; EF Model synchronized Yes; physical schema verified Yes.
- React typecheck passed; API build succeeded (0 errors). Backend test project has pre-existing compile errors; focused Template Deletion tests not executed.
