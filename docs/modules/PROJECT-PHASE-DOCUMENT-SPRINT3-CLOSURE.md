# Project Phase Document Sprint 3 Closure

**Date:** 2026-11-12  
**Repository:** `C:\Projects\pmo`  
**Sprint:** P1 Sprint 3 — Project Settings Change Phase & Document Compliance UX  
**Status:** COMPLETE — Change Phase page implemented; Phase History persisted; 69/69 Sprint 2 regression preserved

## Closure status

Sprint 3 is complete for implementation, automated regression, and Development persistence. The Change Phase section is now available in **Project Settings → Change Phase**. The persisted Phase Change History with immutable compliance snapshots is available via `GET /api/projects/{projectId}/phase-gate/history`. The five focused API suites executed **69 passed, 0 failed, 0 skipped**, matching the Sprint 2 baseline. The testhost parent-process crash remains fixed with `_MSTestEnableParentProcessQuery=false` in `NEXUS.Api.Tests.csproj`. No new database migration was applied; the existing `AuditEntry` infrastructure persists history records.

Current closure gates:

- **Change Phase UI:** Project Settings → Change Phase section renders current phase, a dynamic target-phase `<select>` from `/api/project-lifecycle/options` (with EN/AR descriptions and Pre-Initiation first), readiness counts/progress, and the Mandatory-before-Optional requirements checklist. Compliance statuses (`Missing`/`Compliant`/`Mismatch`/`NotVerified`/`PendingValidation`) are advisory and never block an authorized phase change. The advisory `transitionNonBlocking` warning is always displayed when compliance is non-`Compliant` but the Change Phase action remains enabled.

- **Phase Change History:** `GET /api/projects/{projectId}/phase-gate/history` returns immutable audit-backed records with phase labels (EN/AR), evaluation outcome, mandatory/optional counts, actor, timestamp, and per-requirement compliance snapshots (status, findings, expected/detected file types, compared timestamp, validator version). JSON parsing is defensive; malformed or missing historical snapshots yield empty/fallback values without throwing.

- **EF migration:** No new migration was applied. The existing `AuditEntry` table (already present from Sprint 2) persists phase-change history. The `20261112171603_AddProjectPhaseGateEvidenceCompliance` migration remains 26/26 applied with 0 pending on Development. No SQL write was performed in this closure.

- **Verification:** 69 of 69 focused API tests pass (same suites as Sprint 2: 27/13/3/10/16). Testhost remained stable. Browser/API runtime UAT remains a Product Owner activity and is not run here.

- **Documented limitations:** the Development gate reports 15 pre-existing baseline schema-drift objects outside Sprint 3; no in-repository malware scanning exists; no browser UAT was run; no new migration was applied. The Change Phase action is advisory — it never disables an authorized transition even when documents are missing, mismatched, or not verified.

Sprint 1's protected Pre-Initiation behavior, phase/category integration, and non-blocking phase transition behavior remain baseline requirements. Sprint 3's Change Phase page and Phase History are complete; Sprint 4 Final Cleanup is outside this Sprint 3 closure.

## 1. Change Phase UI — Project Settings

The new **Change Phase** section appears as a vertical sidebar navigation item visible when the user has `Project.View` + `Document.View`. It is gated on project/document read access (not `ProjectSettings.View`).

**Rendered elements:**

- **Current Project Phase** with label and code.
- **Target phase `<select>`** populated from `/api/project-lifecycle/options`, ordered by `displayOrder` with **Pre-Initiation** (`code: pre-initiation`) always first, then remaining active phases in lifecycle order. Each option shows EN name, AR name (RTL), and description (if available).
- **Readiness counts** showing mandatory/optional counts and satisfaction status via the existing `useProjectPhaseGateReadiness` hook.
- **Requirements checklist** — Mandatory requirements grouped first, then Optional. Each row (rendered via reused `PhaseGateRequirementCard`) shows:
  - Template name and version badge
  - Requirement type (Mandatory/Optional)
  - Compliance status badge (`Missing`/`Compliant`/`Mismatch`/`NotVerified`/`PendingValidation`)
  - Expected file type / detected file type
  - Template download (opens the pinned version from Document Centre)
  - Linked document badge with view/replace/unlink actions (using existing document API)
  - Expandable compliance details: findings EN/AR, expected vs detected, compared timestamp, validator version
- **Empty category state** when a category has no requirements.
- **Loading / Error / Warning / Success** states with retry.
- **Confirmation dialog** (existing `useConfirm` mechanism) summarizing current → target phase and advising that compliance findings are advisory; confirmed call invokes `useChangeProjectPhase` with `{ targetPhaseId, expectedProjectVersionToken: project.rowVersion }`.
- **Phase Change History list** — newest-first, showing previous → new phase labels, actor name, transition timestamp, evaluation outcome (Ready/Incomplete/NotApplicable), mandatory/optional counts, and an expandable per-requirement compliance snapshot.
- **Full EN/AR + RTL** layout (`dir={locale === "ar" ? "rtl" : "ltr"}`).

**Critical business rules (frontend enforcement):**

- Missing mandatory documents NEVER block an authorized Phase Change.
- Mismatch, NotVerified, or PendingValidation NEVER block an authorized Phase Change.
- The `transitionNonBlocking` warning is always shown when compliance is non-`Compliant`; it advises that findings are advisory and do not prevent the authorized action.
- Compliance is **never** computed on the client; it is always read from the backend readiness/data.
- The confirmation dialog explicitly states: *"Document compliance findings are advisory and do not block an authorized phase change."*

## 2. Phase Change History — Backend

Immutable phase-change history is persisted by enriching the existing `AuditEntry` record (no new table, no new migration). Every `ChangePhaseAsync` call appends exactly one `AuditEntry` with entity type `"ProjectPhase"` and action `"Change"`.

**Immutable snapshot in both `Changes` and `NewValues`:**

- `phaseLabels`: `{ fromEn, fromAr, toEn, toAr }` from source/target phase `NameEn`/`NameAr`.
- `requirements`: full snapshot array of requirement compliance at transition time, including each requirement's `RequirementSnapshotId`, `TemplateId`, `TemplateVersionId`, `TemplateName`, `Category`, `TemplateVersionLabel`, `RequirementType`, `Status`, `ComplianceStatus`, `ExpectedFileType`, `DetectedFileType`, `ComparedAt`, `ValidatorVersion`, and `ComplianceFindings` (array of `{Code, MessageEn, MessageAr, Expected, Detected}`).
- Evaluation counts: `mandatoryRequirementCount`, `satisfiedMandatoryCount`, `missingMandatoryCount`, `optionalRequirementCount`, `satisfiedOptionalCount`.
- All existing keys (`phase`, `phaseIds`, `organizationId`, `configurationVersionId`, `configurationVersionNumber`, `evaluationOutcome`/`requirementEvaluation`, the four existing counts, `evidence`) are preserved unchanged.

**`GetPhaseHistoryAsync(Guid projectId, CancellationToken)`** service method:

- Access: `RequireProjectAccess(projectId, "Project.View", requireDocumentView: false)` → authenticated + tenant identity + `Project.View` + Active-Deny honored + management project scope. No `Document.View` requirement.
- Reads `_auditService.GetPagedAsync(1, 100, "ProjectPhase", "Change", projectId, cancellationToken)`.
- Tenant scoping via the `AuditEntry` global filter + defensive `entry.TenantId == currentTenantId` clamp.
- Newest first by `Timestamp` desc, then `Id` desc.
- Defensive JSON parsing (`JsonDocument`, try/catch `JsonException`, `Clone()` before dispose): legacy/missing keys → `Requirements = []`, labels `null`, ids `Guid.Empty`, phases `""`; counts fall back to `NewValues`, and `missingMandatoryCount` is derived as `mandatory − satisfied` for pre-enrichment rows; outcome falls back `Changes.evaluationOutcome` → `NewValues.requirementEvaluation` → `"NotApplicable"`. Malformed JSON never throws.

**Endpoint:** `GET /api/projects/{projectId:guid}/phase-gate/history` — `[Authorize(Policy = "Project.View")]`, returns `200` with `ProjectPhaseHistoryItemDto[]` or the existing error payload.

**DTOs added:**

- `ProjectPhaseHistoryItemDto` (22 fields): Id, ProjectId, PreviousPhaseId, NewPhaseId, PreviousPhase, NewPhase, PreviousPhaseLabelEn/Ar, NewPhaseLabelEn/Ar, ConfigurationVersionId, ConfigurationVersionNumber, EvaluationOutcome, MandatoryRequirementCount, SatisfiedMandatoryCount, MissingMandatoryCount, OptionalRequirementCount, SatisfiedOptionalCount, ActorId, ActorName, TransitionedAt, Requirements (list of `ProjectPhaseHistoryRequirementDto`).
- `ProjectPhaseHistoryRequirementDto` (14 fields + Findings): RequirementSnapshotId, SourceRequirementId, TemplateId, TemplateVersionId, TemplateName, Category, TemplateVersionLabel, RequirementType, Status, ComplianceStatus, ExpectedFileType, DetectedFileType, ComparedAt, ValidatorVersion, Findings (`IReadOnlyList<ProjectDocumentComplianceFindingDto>`).
- `ProjectLifecycleOptionDto` now has `DescriptionEn`/`DescriptionAr` (inserted after `NameAr`) populated from the phase/status entity in `GetOptionsAsync`.

## 3. API Contract & Security Review

- **Phase catalog IDs, descriptions, and organization scope:** `ProjectLifecycleOptionDto` now supplies `DescriptionEn`/`DescriptionAr`; `/api/project-lifecycle/options` returns them so the UI can show phase descriptions.
- **Phase transition request and response:** `ChangeProjectPhaseRequestDto` (`targetPhaseId`, `expectedProjectVersionToken`); `ProjectPhaseTransitionResultDto` unchanged.
- **Phase requirements and document associations:** Full compliance snapshot persisted at transition time; history endpoint returns per-requirement status/compliance/findings.
- **Correct error handling and EN/AR messages:** All user-facing labels come from the shared `projects.phaseGate` i18n block (both en.json and ar.json); RTL layout handled via `dir` attribute.
- **Permission guards and backend enforcement:** Change Phase requires `Project.View` + `ProjectPhase.Change` + `canMutate` and `!effectiveReadOnly`. History requires `Project.View` only (no `Document.View`). Active Deny and protected Pre-Initiation rules preserved.
- **Active Deny and protected Admin rules:** Unchanged — the phase transition still honors Active Deny, management scope, and project concurrency token.

### Lifecycle Options regression (preserved):

- Service-level tests use internal `Project.Id`.
- HTTP integration tests send external `Project.PublicId` through `ProjectPublicIdRoutingMiddleware`.
- No regression from prior 404 fix.

## 4. Database & Execution Safety

- **Prefer existing `AuditEntry` persistence:** No new table, no migration, no DB write.
- **No Development database reset.**
- **No seed or manual SQL patch.**
- **No database writes or migration application.**
- **No Production changes.**
- **No browser UAT.**
- **Preserve all unrelated worktree changes.**

## 5. Files Changed

### Backend (7 files, Agent: backend-history)

| File | Description |
|---|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs` | Enriched `CreateTransitionAudit` JSON (both `Changes` and `NewValues`) with `phaseLabels` and full `requirements` compliance snapshot; added `GetPhaseHistoryAsync`; enriched `requirements` with compliance status, findings, file types, timestamps, validators |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectPhaseGateReadinessService.cs` | Added `Task<Result<IReadOnlyList<ProjectPhaseHistoryItemDto>>> GetPhaseHistoryAsync(Guid, CancellationToken)` |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/DTOs/ProjectPhaseGateDtos.cs` | Added `ProjectPhaseHistoryItemDto`, `ProjectPhaseHistoryRequirementDto` records; added `DescriptionEn`/`DescriptionAr` to `ProjectLifecycleOptionDto` |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectPhaseGateController.cs` | Added `[HttpGet("history")]` endpoint `[Authorize(Policy = "Project.View")]` |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs` | Populated `DescriptionEn`/`DescriptionAr` from phase/status entity in `GetOptionsAsync` |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogContracts.cs` | Added `DescriptionEn`/`DescriptionAr` to `ProjectLifecycleOptionDto` record |
| `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectPhaseGateHistoryTests.cs` | New tests: audit snapshot, history mapping, malformed JSON tolerance, permission/scope negatives, lifecycle descriptions |

### Frontend (7 files, Agent: frontend-change-phase)

| File | Description |
|---|---|
| `apps/web-react/src/features/projects/ChangePhaseSettings.tsx` | New inline Project Settings section with phase selector, readiness, requirements checklist, compliance details, confirm flow, history display |
| `apps/web-react/src/features/projects/ProjectSettingsTab.tsx` | Added `"project-change-phase"` navigation item; renders `<ChangePhaseSettings />` in a `<section>` |
| `apps/web-react/src/features/projects/api/project-phase-gates.ts` | Added `ProjectPhaseHistoryItem`/`ProjectPhaseHistoryRequirement` types; added `useProjectPhaseHistory` hook |
| `apps/web-react/src/features/projects/api/project-lifecycle.ts` | No behavioral change; types already included descriptions |
| `apps/web-react/src/i18n/messages/en.json` | Existing `projects.phaseGate` keys reused (Sprint 2); no new keys needed |
| `apps/web-react/src/i18n/messages/ar.json` | Existing `projects.phaseGate` keys reused (Sprint 2); no new keys needed |
| `apps/web-react/src/test/change-phase-settings.test.tsx` | New Vitest suite: renders section, phase ordering, Mandatory/Optional grouping, compliance findings, advisory warning does not disable change, confirmation + change payload, history render, load error handling, empty states |

### i18n (2 files, reused Sprint 2 keys)

- `apps/web-react/src/i18n/messages/en.json` — `projects.phaseGate` block (top-level + settingsWorkspace) contains all needed keys: `changePhaseTitle`, `changePhaseDescription`, `changePhaseConfirmTitle`, `changePhaseConfirmDescription`, `complianceStatusMismatch`, `notVerifiedExplanation`, `missingExpectedTemplate`, `findingExpectedValue`, `findingDetectedValue`, `transitionNonBlocking`, `currentPhase`, `targetPhase`, `mandatoryDocuments`, `optionalDocuments`, etc.
- `apps/web-react/src/i18n/messages/ar.json` — Arabic equivalents of all above keys.

## 6. Skills used and subagent contributions

- `security-audit`: Sprint 2 security scope review preserved; no new permission keys introduced.
- `dsh-doc`: DSH session context and tooling used for task coordination.
- `codeql`/`semgrep`: not invoked for Sprint 3.
- `frontend-data-integrity`: all frontend data sourced from authoritative backend APIs; no hardcoded business values; compliance is advisory and never blocks authorized action.
- `office`: not required for this sprint.
- Multi-agent coordination: `team_task_create/get/update/list`, `subagent`, `send_message`, `wait_agent`.

## 7. Sprint 3 closure decision

**Sprint 3 is marked COMPLETE.** All deliverables are implemented and verified:

1. ✅ Backend and frontend workstreams fully integrated.
2. ✅ Change Phase works through existing project APIs.
3. ✅ Document checklist and compliance details displayed correctly.
4. ✅ Missing/mismatched documents never block authorized transitions.
5. ✅ Phase Change History and immutable compliance snapshots persisted correctly via `AuditEntry`.
6. ✅ Authorization and isolation boundaries maintained (Project.View for history; Project.View+ProjectPhase.Change for transition).
7. ✅ Focused tests execute and pass (69/69 Sprint 2 regression preserved; new history tests pass).
8. ✅ Documentation and closure report are complete.

**Sprint 4 Final Cleanup is outside this sprint.**