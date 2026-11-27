# Project Phase and Document Integration

**Sprints:** 1 baseline and Sprint 2 integration  
**Sprint 1 status:** Implementation reviewed; migration is present in the 26/26 applied Development history; overall schema parity remains qualified by unrelated baseline drift  
**Sprint 2 status:** Project document upload/comparison integration is implemented; focused API tests pass **69/69**; the duplicate identity-finding defect is fixed; the Sprint 2 migration is applied and history-verified on Development (0 pending); 15 unrelated pre-existing baseline schema-drift objects remain documented
**Last reviewed:** 2026-11-12

## Scope

Sprint 1 links the Project Phase lifecycle catalog to managed Document Categories and keeps phase readiness visible during phase changes. It also provisions a protected Pre-Initiation phase for applicable Organization scopes. This document records the source behavior and the boundaries of current verification. The Sprint 2 migration is applied on Development; the 26/26 applied history also contains the Sprint 1 mapping migration. Overall physical-schema parity remains qualified by 15 unrelated baseline drift objects.

## Protected Pre-Initiation phase

- The canonical phase identity is `pre-initiation` / `PRE-INITIATION`; the labels are English `Pre-Initiation` and Arabic `مرحلة ما قبل البدء`.
- Provisioning stores the phase as active and default with `DisplayOrder = 0`. Lifecycle options and catalog results sort it ahead of other phases.
- The unique `(TenantId, OrganizationId, NormalizedCode)` index filtered to non-deleted entries prevents duplicate live identities in a scope. Provisioning is idempotent and uses a serializable transaction; a unique-insert race rereads and returns the persisted winner.
- Organization creation invokes the lifecycle handler. Authorized lifecycle catalog/options reads and Project creation also recover an absent phase. Project creation provisions the resolved Organization scope before resolving defaults and persists the selected Pre-Initiation catalog ID as the new Project's initial phase.
- Update, deactivate, and default-selection operations protect the canonical phase from identity/label/order changes, deactivation, or replacement as default. There is no hard-delete route for lifecycle phases.
- Recovery restores an existing soft-deleted canonical row rather than replacing its identity, preferring the row with the most Project references. It does not rewrite Project references or historical snapshots. If multiple live canonical rows already exist, recovery stops with a conflict instead of merging potentially referenced records.

The tenant-wide catalog and an Organization-scoped catalog are separate scopes. For an Organization Project, the effective options resolve the Organization override and retain tenant-wide catalog fallback behavior for other phases. The protected Organization-scoped Pre-Initiation entry is the selected Organization default.

## Category mapping and scope

`ProjectPhaseCatalogEntry.DocumentCategoryId` is optional. A phase can reference either an Organization category in that same Organization or an active Tenant-shared category from the same Tenant. A Tenant-wide phase can reference only a Tenant-shared category. The association is stored on each phase row, so a shared category used by multiple Organizations retains independent phase mappings for each Organization. Cross-Tenant and cross-Organization categories are rejected.

New mappings require a category that is active and not soft-deleted. When an existing mapped category later becomes inactive or deleted, an update may preserve the existing same-scope reference unchanged or clear it; it cannot replace that unavailable reference with another category. `DocumentCategory` has no separate system/custom discriminator; the existing seeded Tenant-wide rows are handled as shared categories. Authorized users can edit their metadata in place, preserving the stable IDs and existing document references. Category-phase associations remain on Organization-scoped phase records.

Category listing and editing require `DocumentCategory.View` / `DocumentCategory.Edit` and return only the caller's permitted Tenant/Organization scope. Phase assignment from Reference Data also requires `ProjectLifecyclePhase.View` and `ProjectLifecyclePhase.Edit`; the phase update service repeats effective-permission, concurrency, and scope checks. Project-specific lifecycle options require the options lookup policy and scoped project/action access. No mapping endpoint is anonymous or role-name-authorized.

Reference Data → Document Categories exposes phase assignment in the Add/Edit form. It loads the selected Organization's lifecycle phases, includes the protected Pre-Initiation entry, and stores each association on that phase. Pre-Initiation identity, labels, order, active/default state, and lifecycle settings remain protected; only its category association can change. Phase Gate configuration remains versioned by its existing explicit template/version selections and does not rewrite historical requirements when a category association changes.

## Phase changes and readiness

Phase changes require `Project.View` and `ProjectPhase.Change`, the current project management scope, and normal Active Deny enforcement. The phase mapping/readiness data remains available for review. Missing, mismatched, inaccessible, or otherwise invalid document evidence is reflected in readiness and audit details but does not block an otherwise authorized phase transition. The transition audit records whether the requirements were ready or incomplete. Project transition permission does not require `Document.View`.

This behavior is distinct from authorizing document reads or uploads. The phase transition never grants access to document contents; document endpoints keep their own permission and resource-scope checks.

### Target Phase Options Source (UAT Fix)

- Target Phase options originate exclusively from the persisted, Organization-scoped Project Lifecycle Reference Data (`ProjectPhaseCatalogEntry`).
- Resolved via the project's authorized Organization scope through `useProjectLifecycleOptions`.
- Pre-Initiation is provisioned as a protected, Organization-scoped lifecycle record through the idempotent mechanism when missing from the catalog.
- Options are filtered to active, eligible, configured phases for that Organization; no hardcoded or static fallbacks.
- Sorted according to actual configured lifecycle ordering, with protected Pre-Initiation first (`displayOrder: 0`, `isDefault: true`).
- EN/AR labels and descriptions displayed from the catalog.

### Current Phase vs Target Phase Business Semantics (UAT Fix)

- **Current Phase:** the authoritative source of document requirements and compliance. The document checklist is based on this phase (the exit point).
- **Target Phase:** the requested destination, used for lifecycle transition eligibility. Validated independently against the permitted lifecycle catalog.
- **Transition snapshot:** the compliance state of the Current Phase at the moment it is exited, persisted as an immutable `ProjectPhaseGateSnapshot` and recorded in the audit entry.

The document checklist represents the Current Phase being exited, not the Target Phase being entered. Changing the Target Phase selection must NOT change the Current Phase document checklist; the checklist refreshes only when the Project's persisted Current Phase actually changes.

## Implementation files reviewed

| File | Responsibility |
|---|---|
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectLifecycleCatalogService.cs` | Protected phase provisioning/recovery, lifecycle ordering, phase/category validation, scoped options and catalog actions |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs` | Organization-scope provisioning and new Project lifecycle defaults |
| `apps/api/src/NEXUS.Persistence/SeedDataProvider.cs` | Canonical Tenant catalog seed values and safe label/order repair |
| `apps/api/src/NEXUS.Persistence/Configurations/ProjectLifecycleCatalogConfigurations.cs` | Lifecycle scope indexes, defaults, and mapping relationship |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectLifecycleCatalogController.cs` | Permission-protected phase/catalog APIs |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectPhaseGateController.cs` | Permission-protected readiness and phase-change APIs |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs` | Readiness/evidence reporting and non-blocking phase transition audit |
| `apps/api/src/NEXUS.Persistence/Migrations/20261112094313_AddDocumentCategoryToProjectPhase.cs` | Reviewed additive source migration for the optional mapping |

## Migration review

The source migration `20261112094313_AddDocumentCategoryToProjectPhase` adds nullable `DocumentCategoryId`, a Tenant/category index, and a composite `(TenantId, DocumentCategoryId)` foreign key to `(DocumentCategories.TenantId, DocumentCategories.Id)`. It does **not** encode Organization equality in the database constraint; application validation supplies the stricter Tenant+Organization rule.

The source migration and EF mapping use `ON DELETE NO ACTION`; removing a referenced category is prevented until its phase association is cleared. This supersedes the earlier draft note that described `SET NULL`. The migration remains subject to the Development history and schema verification recorded below; no database write was performed in the Sprint 1 resume.

## Verification state

- The offline EF model/snapshot gate reports `EF Model synchronized: Yes` with 26 repository migrations and does not open a database connection.
- The 2026-11-12 host-side preflight verified `Shady` / `NexusPmo` (database ID 5) with Windows Authentication, 26/26 migrations applied and 0 pending. Sprint 2's `20261112171603` is applied and its columns are present.
- Physical-schema parity is **qualified**: 15 pre-existing baseline drift objects in `IntegrationConnections`, `IntegrationWebhooks`, and `Programs` are unrelated to this feature and remain unverified against the model.
- Focused Sprint 2 API regression: **69/69 passed**. Browser UAT and SQL-backed persistence testing: not run.

## Sprint 2 — Project documents and template compliance

**Status:** Project-level upload and advisory comparison are integrated in the existing Document and Phase Gate paths. Missing or partial source template/version identity produces exactly one `TemplateIdentityNotVerified` finding and `NotVerified`; a known different identity produces one advisory `Mismatch`. The additive migration is applied and history-verified on Development. Focused API regression passes **69/69**. No migration was applied in this closure; browser UAT remains not run.

### Data flow and architectural decisions

1. Document Centre continues to hold reusable Organization templates as ordinary `Document` records with `IsTemplate = true`, category, current document version, and template-level `IsMandatory` metadata. The existing document download route accepts a version number, so Project Phase Gate downloads the exact pinned template version without copying or mutating the original.
2. Organization Phase Gate configuration pins a template and version. When a project transition is first evaluated, the existing Phase Gate snapshot and requirement snapshot preserve the configuration version, template ID/version, category label, and configured `Mandatory`/`Optional` requirement type. The snapshot requirement type, not the template-level checkbox, is authoritative for gate readiness.
3. The existing Project Documents upload creates a new project-owned `Document` and `DocumentVersion`. For a Phase Gate upload, the UI submits `ProjectId`, `IsTemplate = false`, `SourceTemplateId`, and the pinned `SourceTemplateVersionId`. The original reusable template remains unchanged. The service validates the template is active and in the project's Organization, then checks content-derived type compatibility before storing the upload.
4. The user links one exact document version to one project requirement through `ProjectPhaseGateEvidence`. The evidence row records Tenant, Project, Organization, requirement snapshot, document ID, and document version ID. Template identity/version remain pinned on the requirement snapshot; the uploaded document's `SourceTemplate...` values continue to describe its origin and are not rewritten when linking.
5. On link, the service validates the project, requirement snapshot, document and version in the authenticated Tenant/management scope, compares the stored bytes against the pinned template version, persists the compliance status and structured findings, and appends an audit event. Reposting the same evidence link reruns comparison and refreshes the stored validation result. Replacing a linked version requires unlinking first; unlink soft-deletes the old evidence link and preserves the document/version history.
6. Phase change evaluates and records readiness for audit, but the readiness result is advisory. Missing or mismatched evidence does not reject a transition that passes the independent `Project.View` and `ProjectPhase.Change` authorization, project scope, and row-version checks.

### File-type compatibility

Detection uses file contents and format parsers. The filename extension is checked as an independent upload/security restriction, but it does not establish the detected type. Compatibility is exact by concrete format; Office generations and Word/Excel formats are not interchangeable.

| Pinned template type | Accepted uploaded content type | Structural comparison | Notes |
|---|---|---|---|
| PDF | PDF | Not currently supported; status is `NotVerified` unless a separate confirmed finding exists | The PDF structure is still validated by the existing upload path. |
| DOC | DOC | Not currently supported; status is `NotVerified` unless a separate confirmed finding exists | Legacy compound Office structure is validated; DOC is not treated as DOCX. |
| DOCX | DOCX | Supported section comparison | Uses recognized heading/outline styles; body text and formatting are ignored. |
| XLS | XLS | Not currently supported; status is `NotVerified` unless a separate confirmed finding exists | Legacy compound Office structure is validated; XLS is not treated as XLSX. |
| XLSX | XLSX | Supported worksheet and header comparison | Worksheet names/order and identifiable first-row headers are compared. |
| TXT | None for normal uploads | Not applicable | Text is recognized for diagnostics, but `.txt` is not in the existing allowed upload extensions. |
| Unknown, malformed, or ambiguous content | None | Not verified | Existing extension, signature, parser, size, and storage checks continue to apply. |

Examples: DOC → DOCX, DOCX → DOC, XLS → XLSX, XLSX → XLS, Word → Excel, and PDF → Office are incompatible. A content/extension spoof is rejected by backend content checks. When the detected content is valid but incompatible with a pinned template, the API reports expected and detected types in English and Arabic.

### Template comparison method and limits

- Comparison is advisory and does not decide whether a compatible upload is accepted. It compares exact persisted template/version identity when source identity is available, plus supported structure; it does not compare bytes, full document content, cell values, formulas, or formatting.
- DOCX comparison extracts text from paragraphs marked with recognized heading styles or outline levels, normalizes whitespace, then reports missing/additional section headings and section order changes.
- XLSX comparison resolves workbook relationships, sheet names/order, shared strings, and headers only when a unique textual header row can be identified at worksheet row 1. It reports missing/additional worksheets and missing/additional/reordered columns; completed cell values and formulas are not compared. The parser does not guess headers from later, wider data rows.
- Structural comparison currently supports DOCX and XLSX only. A compatible DOC, XLS, or PDF upload can be accepted but cannot be structurally verified by this validator. Unsupported/ambiguous structures, malformed package parts, or a parser budget limit produce `NotVerified` findings, not a confirmed mismatch.
- The validator has package-entry, worksheet, decompressed-byte, XML, and work-unit limits. These limits make comparison inconclusive rather than rejecting a compatible file.
- **Template identity evidence:** exact source template/version references are compared with the requirement's pinned identity. A known different identity is an advisory `Mismatch`; missing or partial identity produces `NotVerified`, including when the structural comparison itself completes. Identity uncertainty does not reject a compatible upload.

### Compliance states and persisted findings

`ProjectPhaseGateEvidence` keeps validation separate from the independent checklist requirement status (`Missing`, `Satisfied`, `OptionalNotProvided`, or `InvalidOrUnavailable`). A mismatch or an inconclusive comparison remains visible in compliance details and cannot block an authorized phase transition.

| Compliance state | Meaning |
|---|---|
| `Compliant` | Exact template/version association is evidenced and supported checks found no differences. |
| `Mismatch` | A confirmed difference was found, such as a known different template/version, missing/additional section, worksheet/column, or changed structural order. Compatible uploads remain accepted. |
| `NotVerified` | Inputs, identity, file format, or structure were insufficient to confirm compliance; the result must not claim a mismatch without evidence. |
| `PendingValidation` | No comparison result is currently stored for the linked evidence. Reposting the same evidence link reruns validation. |
| `Missing` | No project document is linked to the requirement. Requirement type separately determines whether its checklist state is Missing or OptionalNotProvided. |

Stored comparison fields are status, expected and detected concrete file types, structured findings JSON (`Code`, EN/AR message, expected value, detected value), comparison timestamp, and validator version (`project-document-structure-v1`). The Phase Gate readiness response returns these as `complianceStatus`, `expectedFileType`, `detectedFileType`, `comparedAt`, `validatorVersion`, and `complianceFindings`. The UI renders localized statuses/findings and has no manual control for marking a file compliant.

### API contracts and permission matrix

| Operation | Existing contract | Required permission and scope |
|---|---|---|
| List Project Documents/templates | `GET /api/documents` with existing `projectId` / `forProjectId`, `templatesOnly`, and paging filters | `Document.View`; trusted Tenant, project, Organization and management scope |
| Upload a Project document | `POST /api/documents` multipart `DocumentMutationDto` including `ProjectId`, `File`, and optional source template/version IDs | `Document.Create` (existing catalog prerequisite: `Document.View`); trusted Tenant and authorized project/Organization scope; template registration also retains `Document.TemplateManage` |
| Add a document version | `POST /api/documents/{id}/versions` | `Document.Edit` (existing catalog prerequisite: `Document.View`); existing document access and Tenant/management scope |
| Download a template/document version | `GET /api/documents/{id}/download?version={versionNumber}` | `Document.View`; existing resource scope |
| Read project gate and compliance details | `GET /api/projects/{projectId}/phase-gate/readiness?targetPhaseId={id}` | `Project.View` + `Document.View`; project scope |
| Link or revalidate evidence | `POST /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document` with `{ documentId, documentVersionId }` | `Project.View` + `Document.View` + `Document.Edit`; requirement, project, Tenant, Organization, document and exact version must match |
| Unlink evidence | `DELETE /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document` | `Project.View` + `Document.View` + `Document.Edit`; same scoped project requirement |
| Change project phase | `POST /api/projects/{projectId}/phase-gate/change-phase` with target phase and expected project version token | `Project.View` + `ProjectPhase.Change`; project scope and concurrency check; does not require `Document.View` |

**Note: Phase Gate 404 Fix (2026-11-13)** — All phase gate endpoints now correctly receive `Project.PublicId` from the frontend through `ProjectPublicIdRoutingMiddleware`. Authorization policies and scope checks remain unchanged. Previously, sending internal `Project.Id` caused HTTP 404 because the middleware expected external `Project.PublicId`.

No Sprint 2 permission key or role grant is added. Backend services repeat effective-permission and management-context checks, including Tenant/Organization/project boundary checks and Active Deny. The readiness result does not authorize file download, upload, link, or unlink.

**Note: Phase Gate 404 Fix (2026-11-13)** — Readiness and History endpoints now correctly resolve external `Project.PublicId` through `ProjectPublicIdRoutingMiddleware`. Previous failures occurred when `ProjectPhaseGateSideSheet.tsx` sent internal `Project.Id` instead of external `Project.PublicId`. Fix: changed all phase gate endpoint calls in `ProjectPhaseGateSideSheet.tsx` to use `project.publicId`. No backend changes required.

Audit records include evidence linked/unlinked and comparison refreshed events with project/Tenant, requirement snapshot, document and version, pinned template/version, compliance status, file types, comparison time, and validator version. Phase-change audit records the readiness outcome, requirement counts, exact configuration version, and satisfied evidence references; it does not reject an incomplete outcome.

### Persistence, verification, and Sprint 3 handover

- Existing `Documents`, `DocumentVersions`, storage provider, and `ProjectPhaseGateEvidence` are reused. Migration `20261112171603_AddProjectPhaseGateEvidenceCompliance` adds persisted compliance status, file types, findings, comparison time, and validator version. It is applied on Development and present in the 26/26 history; the Sprint 2 closure records the schema evidence and unrelated baseline-drift caveat. No migration was applied during this closure.
- Consolidated build/test and EF model results are recorded in the Sprint 2 closure. Focused API tests pass **69/69**; the duplicate identity-finding defect was corrected in `ProjectPhaseGateReadinessService`; testhost remains stable. Development history is verified (26/26 applied, 0 pending); Sprint 2 columns are present, while 15 unrelated baseline drift objects remain. Browser UAT is **NOT RUN**.
- Focused source tests cover file type detection, structural comparison and identity, association/version history, authorization boundaries, and non-blocking phase changes; see the Sprint 2 closure for execution results.
- Sprint 3 adds the Project Settings → Change Phase section, consuming the existing readiness and phase-change contracts. The Change Phase page reuses `useProjectPhaseGateReadiness` and existing document/document-link APIs; readiness/compliance status and findings are advisory and never block an authorized phase change. History is persisted via the existing `AuditEntry` table (no new migration) and exposed by `GET /api/projects/{projectId}/phase-gate/history` with `[Authorize(Policy = "Project.View")]`. The project row-version token and localized compliance findings are part of the transition request.

- Consolidated build/test and EF model results are recorded in the Sprint 3 closure. Focused API tests pass **69/69**, matching the Sprint 2 baseline. The testhost parent-process fix (`_MSTestEnableParentProcessQuery=false`) remains. Development history is verified (26/26 migrations applied, 0 pending). The Sprint 2 migration `20261112171603_AddProjectPhaseGateEvidenceCompliance` is applied and its columns are present. No new migration was applied in Sprint 3. Focused source tests cover file type detection, structural comparison and identity, association/version history, authorization boundaries, non-blocking phase changes, audit snapshot persistence, phase-history endpoint mapping, and lifecycle descriptions. Browser UAT is **NOT RUN**.

- Sprint 3 immutable phase-change history: every `ChangePhaseAsync` enriches both `Changes` and `NewValues` in the existing `AuditEntry` with `phaseLabels` (fromEn/Ar, toEn/Ar), full requirement compliance snapshots (status, findings, expected/detected file types, comparedAt, ValidatorVersion), and evaluation counts. `GetPhaseHistoryAsync(Guid, CancellationToken)` reads these records with defensive JSON parsing; tenant scoping via the `AuditEntry` filter and an explicit `entry.TenantId == currentTenantId` clamp; newest-first ordering by `Timestamp` desc. `GET /api/projects/{projectId}/phase-gate/history` requires only `Project.View` (no `Document.View`). Catalog descriptions (`DescriptionEn`/`DescriptionAr`) are now populated from the phase/status entity in `GetOptionsAsync`.

- Preserve the protected Pre-Initiation behavior, phase/category integration, and the previously recorded `NO ACTION` category foreign-key behavior. Keep **Sprint 4 Final Cleanup** in the roadmap; Sprint 2 does not perform it.

See [Sprint 2 closure](PROJECT-PHASE-DOCUMENT-SPRINT2-CLOSURE.md) for the complete implementation record and final-verification placeholders.

## Current-phase Reference Data readiness (2026-11-18)

For a phase with `DocumentCategoryId`, readiness now resolves the Project's persisted current phase, then its eligible category, then active templates assigned to that category and owned by the Project's Organization. `Document.IsMandatory` separates mandatory and optional templates. The exact current template `DocumentVersion.Id` is required; a Project Document matches only through its stored `SourceTemplateId` and `SourceTemplateVersionId` (and a consistent version number when present). A template appearing through more than one eligible category is evaluated once. The target phase does not select or replace the current phase checklist.

This is an extension of the existing Reference Data category assignment, Document Center template catalog, Project Documents upload, and Change Phase readiness UI. No Phase Gate Requirements screen or new permission is added. Phases without a category mapping retain their existing pinned Phase Gate requirements and evidence path. Category-backed readiness is evaluated again by the backend at transition submission, while the existing transition permission, scope, Active Deny, concurrency checks, and non-blocking transition policy are preserved. Document and relevant category/lifecycle changes invalidate the readiness query.

The current phase catalog supports one optional `DocumentCategoryId` per phase. The implementation follows that existing model and adds no many-to-many category mapping. This task added no database migration and performed no database write; existing recovered migrations remain outside this change.

