# Project Current-Phase Document Readiness

**Date:** 2026-11-18  
**Scope:** Document Center, Project Documents, Reference Data, and Project Lifecycle

## Outcome

Implemented category-backed document readiness for the Project's current phase and exact Source Template/version matching. The existing Change Phase UI displays the current-phase result before transition confirmation. Existing phase authorization, project scope checks, Active Deny, concurrency checks, and non-blocking readiness policy remain in place.

## Existing functionality reused

- Document Center's shared template catalog, stable template IDs, immutable document versions, and active/category metadata.
- Project Documents upload and document detail/list contracts, including persisted `SourceTemplateId`, `SourceTemplateVersionId`, and source version number.
- Reference Data's `ProjectPhaseCatalogEntry.DocumentCategoryId` assignment and shared/Organization category scope validation.
- The existing Project Phase Gate readiness API, Change Phase settings, side sheet, audit path, and project authorization checks.
- Existing requirement evidence behavior for phases without a Reference Data category assignment.

No new Phase Gate Requirements administration screen, permission, entity, relationship, or migration was added by this change.

## Missing functionality implemented

- A category-mapped current phase now resolves requirements from active templates in the Project's Organization assigned to that phase's configured category.
- Category templates use their configured `Document.IsMandatory` value to distinguish mandatory from optional requirements.
- Project Documents are matched by exact `SourceTemplateId` and current `SourceTemplateVersionId`; a populated source version number must agree. Filenames and display names do not participate.
- Readiness requires an active, unarchived Project Document in the same Project and an available current file version with matching storage metadata. Wrong-version, inactive, archived, unavailable, and out-of-Organization associations cannot satisfy a requirement.
- Mandatory satisfied/missing counts and percentage, optional counts, and per-template statuses are returned by the backend and shown in the existing Change Phase UI. Optional missing documents do not count as mandatory missing.
- Readiness is recalculated on each API evaluation and transition submission. Relevant document, template/category, and lifecycle changes invalidate the frontend readiness query.
- The Project Document details display the exact Source Template Version ID; source identity remains present in list/detail responses after save and reopen.
- English and Arabic labels cover the additional version identity and readiness summary.

## Requirement resolution and transition behavior

For a Project whose current phase has a configured category, the backend resolves:

`Project current phase → configured DocumentCategory → active category templates in the Project Organization`

The category must be active and not deleted and must be either Tenant-shared or owned by the Project's Organization. Templates must be active, unarchived, undeleted, and Organization-owned. Each distinct template is emitted once. Its current `DocumentVersion.Id` is the applicable version for matching. A Project Document satisfies it only when the source template ID and source version ID match exactly and its current file version is available and consistent with the storage provider metadata.

The selected target phase does not change this checklist. A phase without a category assignment retains the existing pinned Phase Gate snapshot/evidence path. The current phase catalog stores one optional category ID per phase; no many-to-many mapping was added.

Readiness remains advisory and does not automatically block a phase transition. The backend evaluates and audits the current-phase result while preserving the existing `Project.View` and `ProjectPhase.Change` authorization, management scope, Active Deny, and project row-version checks. Phase transition does not gain document-content access.

## Files changed for this task

- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs`
- `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectPhaseGateLifecycleTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj` (adds an isolated readiness-focused compile/test selection)
- `apps/api/tests/NEXUS.Api.Tests/Security/ProjectPhaseGateAuthorizationBoundaryTests.cs`
- `apps/web-react/src/features/administration/api/projectLifecycle.ts`
- `apps/web-react/src/features/organization/api/phase-gate-requirements.ts`
- `apps/web-react/src/features/projects/ChangePhaseSettings.tsx`
- `apps/web-react/src/features/projects/ProjectDocumentsTab.tsx`
- `apps/web-react/src/features/projects/api/documents.ts`
- `apps/web-react/src/features/projects/api/project-phase-gates.ts`
- `apps/web-react/src/features/projects/components/ProjectPhaseGateSideSheet.tsx`
- `apps/web-react/src/i18n/messages/ar.json`
- `apps/web-react/src/i18n/messages/en.json`
- `docs/modules/DOCUMENT-MANAGEMENT-CORE.md`
- `docs/modules/PROJECT-PHASE-DOCUMENT-INTEGRATION.md`

The shared worktree contains other recovered, uncommitted changes. This list covers files changed for this readiness task; unrelated working-tree content was preserved.

## Verification

- Focused React tests (`change-phase-settings`, `project-phase-gate-side-sheet`, `document-center-parity`): **30 passed**.
- React typecheck: **passed**.
- React production build (`npm run build`): **passed**. Existing dependency annotation and large-chunk warnings were emitted.
- Focused API tests: the readiness-focused test assembly compiled successfully. VSTest aborted before reporting any cases because the testhost did not connect to the runner within 180 seconds. A broader test-project compile also exposed unrelated pre-existing Finance and ResourceManagement test-source errors; the focused target excludes those files. Backend test execution remains unverified.
- API build: **passed**, 0 errors, using the isolated `apps/api/bin/codex-p1` output to avoid DLLs held by the running shared API process. Warnings included an existing nullable dereference in `ProjectLifecycleCatalogService`, an unavailable NuGet vulnerability feed, and IKVM compatibility warnings.
- `git diff --check` for task files: **passed**; Git reported line-ending normalization notices only.
- Database: this change added no persistence model or migration and performed no database operation, reset, or reseed. The recovered migration files already present in the shared worktree were not applied or changed as part of this task.
- Authenticated browser UAT: **not run**.

## Remaining verification

Resolve the VSTest testhost connection issue and execute the focused API tests. Authenticated browser UAT remains a release verification item.
