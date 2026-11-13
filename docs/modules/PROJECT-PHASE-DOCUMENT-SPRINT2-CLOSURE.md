# Project Phase Document Sprint 2 Closure

**Date:** 2026-11-12  
**Repository:** `C:\Projects\pmo`  
**Sprint:** P1 Sprint 2 — Project Document Upload & Template Compliance  
**Status:** COMPLETE — 69/69 focused API tests pass; Sprint 2 migration applied and history-verified on Development; documented limitations remain (browser UAT not run, unrelated baseline schema drift)  
**Scope:** Project Lifecycle × Document Centre × Project Documents

## Closure status

Sprint 2 is complete for implementation, automated regression, and Development persistence. The five focused API suites executed **69 passed, 0 failed, 0 skipped**, including the previously failing duplicate-finding test. The testhost parent-process crash remains fixed with `MSTest.EnableParentProcessQuery=false`. The confirmed defect — two `TemplateIdentityNotVerified` findings for one identity uncertainty — was corrected in `ProjectPhaseGateReadinessService`: the pinned expected reference is passed into `DocumentTemplateComparison.Compare`, the comparator owns the single identity finding, and a confirmed partial mismatch replaces that finding with one `TemplateIdentityMismatch`. The Development EF gate verified `Shady` / `NexusPmo` (database ID 5), Windows Authentication, model synchronization, and **26/26 migrations applied, 0 pending**; the Sprint 2 migration `20261112171603_AddProjectPhaseGateEvidenceCompliance` is applied and its columns are present. The gate's physical-schema parity check still reports 15 pre-existing baseline drift objects in `IntegrationConnections`, `IntegrationWebhooks`, and `Programs`, unrelated to Sprint 2. No migration was applied and no SQL write was performed in this closure task. Browser/API runtime UAT remains NOT RUN.

Current closure gates:

- **Template identity evidence:** missing or partial source template/version identity produces exactly one `TemplateIdentityNotVerified` finding and `NotVerified`; a known different identity produces one advisory `Mismatch`. A structurally matching document is never reported `Compliant` without sufficient identity evidence.
- **EF migration:** additive migration `20261112171603_AddProjectPhaseGateEvidenceCompliance` is applied on Development; history shows 26/26 applied and 0 pending. No migration apply is required or authorized by this closure.
- **Verification:** 69 of 69 focused API tests pass (27 file-type, 13 comparison, 3 association/history, 10 authorization boundary, 16 lifecycle). Testhost remained stable. Browser/API runtime UAT remains a Product Owner activity and is not run here.
- **Documented limitations:** the Development gate reports 15 pre-existing baseline schema-drift objects outside Sprint 2; no in-repository malware scanning exists; no SQL-backed persistence test or browser UAT was run.

Sprint 1's protected Pre-Initiation behavior, phase/category integration, and non-blocking phase transition behavior remain baseline requirements. Sprint 3's new Change Phase page and Sprint 4 Final Cleanup are outside this Sprint 2 closure.

## 1. Data flow and architectural decisions

1. Document Centre retains reusable Organization templates as existing `Document` records with `IsTemplate = true`, category, version history, and template-level `IsMandatory` metadata. Phase Gate downloads the selected version through the existing download endpoint; it does not copy or overwrite the original template.
2. Organization Phase Gate configuration references a template and exact template version. A project transition snapshot stores its configuration version, and each requirement snapshot pins `TemplateId`, `TemplateVersionId`, category/labels, and the configured `Mandatory` or `Optional` type. The configured requirement type remains authoritative for checklist readiness; a template's `IsMandatory` value does not rewrite Phase Gate configuration.
3. A user downloads the pinned version, completes it offline, and uploads through the existing Project Documents API. For a checklist upload the frontend sends the Project ID, `IsTemplate = false`, source template ID, and source template version ID. The backend persists a new project-owned `Document` and `DocumentVersion`, leaving the reusable template untouched.
4. Upload validation examines file contents using format signatures/parsers. Filename extension remains an independent upload/security restriction and is not accepted as proof of the actual type. A valid but incompatible type is rejected with expected/detected labels in English and Arabic. Compatible same-type files are accepted independently of structural comparison results.
5. Linking an exact document version to a requirement creates or refreshes `ProjectPhaseGateEvidence`. The evidence associates Tenant, Project, Organization, requirement snapshot, document, and document version. The requirement snapshot carries the pinned template/version. The `Document.SourceTemplate...` values retain the upload's original source and are not rewritten when a document is linked to a different requirement.
6. The service validates storage metadata and compares supported structures, persists compliance details, and audits the link/validation. Reposting the same evidence link reruns comparison. A different linked version requires unlinking first; unlinking soft-deletes the previous evidence link so its history and document versions remain available.
7. Phase change uses the existing Phase Gate service to evaluate and audit readiness, but does not reject a transition because a required document is missing, mismatched, or inconclusive. The separate project authorization, management scope, Active Deny, active target phase, and project concurrency token still apply.

No parallel file-storage system or duplicate document record model is introduced. Existing `Document`, `DocumentVersion`, storage-provider, Document Centre, Project Documents, and Phase Gate interfaces are reused.

## 2. File-type compatibility matrix

Compatibility is exact by detected concrete format. DOC is not assumed to be DOCX; XLS is not assumed to be XLSX; Word and Excel formats are never interchangeable.

| Template's detected type | Accepted uploaded detected type | Structural comparison support | Result notes |
|---|---|---|---|
| PDF | PDF only | No | Compatible upload can be accepted; current structural validator reports `NotVerified` unless a separate confirmed finding exists. |
| DOC | DOC only | No | Legacy compound format is validated; it is not treated as DOCX. |
| DOCX | DOCX only | Yes | Section/heading structure is compared. |
| XLS | XLS only | No | Legacy compound format is validated; it is not treated as XLSX. |
| XLSX | XLSX only | Yes | Worksheet names/order and identifiable headers are compared. |
| TXT | None for regular uploads | No | Recognized for diagnostics, but `.txt` is not an allowed upload extension. |
| Unknown, invalid, or malformed | None | No | Existing type/signature/package validation rejects unsupported or invalid content. |

The actual supported upload extension allowlist remains `.pdf`, `.doc`, `.docx`, `.xls`, and `.xlsx`. The existing configured size limit, multipart request limit, safe storage-provider checks, file-size/content consistency, format parser checks, storage metadata, and hash checks remain in the upload/validation flow. The Main Agent reviewed the current Documents upload/storage path and found no in-repository malware-scanning integration. Scanning provided externally by a storage or hosting service was not verified, so this report does not claim malware scanning.

## 3. Template-comparison methodology and limitations

The comparator is advisory. It avoids byte-for-byte comparison and ignores body text, cell values, formulas, and formatting so normal completion edits do not reject an otherwise compatible upload.

- **DOCX:** parses the Word body and styles, extracts text from paragraphs marked with recognized heading styles or outline levels, normalizes whitespace, then reports missing/additional section names and section-order changes.
- **XLSX:** resolves workbook relationships and shared strings, reads worksheet names and order, and compares headers when a unique textual header row can be identified at row 1. It reports missing/additional worksheets and missing/additional/reordered columns. It does not guess headers from later or wider completed data rows.
- **Other compatible formats:** PDF, DOC, and XLS do not receive structural comparison in this validator, so compatible uploads may be accepted with `NotVerified` status.
- **Inconclusive inputs:** unavailable stored content, ambiguous or malformed package parts, unrecognized headings/header rows, unsupported formats, or exhausted parser budgets produce `NotVerified` findings. These findings do not assert confirmed mismatch.
- **Resource limits:** the comparator applies package-entry, worksheet-count, decompressed-byte, XML, and work-unit limits. Exceeding a limit makes comparison inconclusive; it is not a template mismatch and does not reject a compatible upload.
- **Template identity:** exact source template/version references are compared with the requirement's pinned identity. A known different identity is an advisory `Mismatch`; missing or partial identity produces `NotVerified` with an explicit finding, even when supported structural comparison completes. Identity uncertainty never rejects a compatible upload.

`Compliant` means only that the exact source identity and supported checks have been verified with no findings. It is not approval of the document's substantive correctness, accuracy, or business content.

## 4. Compliance states and stored difference details

Each requirement has an independent checklist `Status` and `ComplianceStatus`. A file can be available and linked while the content compliance state is `Mismatch` or `NotVerified`; compliance is advisory and does not grant or revoke access to the document.

| State | Meaning |
|---|---|
| `Compliant` | Exact template/version association is evidenced and supported structural checks found no differences. |
| `Mismatch` | A confirmed source-template/version or supported structural difference was found. Compatible uploads remain accepted and the finding is returned. |
| `NotVerified` | Comparison could not make a reliable determination, including unsupported structural formats, unavailable content, ambiguous structure, parser limits, or absent/partial identity after the required fix. |
| `PendingValidation` | No completed comparison result is stored for the current linked evidence. Reposting the same evidence request reruns comparison. |
| `Missing` | No evidence link exists for the requirement. The independent requirement status reports `Missing` or `OptionalNotProvided` according to configured requirement type. |

The evidence model stores `ComplianceStatus`, `ExpectedFileType`, `DetectedFileType`, serialized structured findings, `ComparedAt`, and `ValidatorVersion`. Each finding includes a stable code, English and Arabic messages, expected value, and detected value. The current validator identifier is `project-document-structure-v1`. The API returns the stored values and finding details; the UI presents localized labels and findings and does not provide a manual compliance checkbox.

## 5. API and data-model contracts

### Existing API routes used

| Purpose | Contract | Behavior |
|---|---|---|
| List project documents or project-eligible templates | `GET /api/documents` with existing query filters such as `projectId`, `forProjectId`, and `templatesOnly` | Existing paged Document API; organization/project filters remain scope-bound. |
| Upload project document | `POST /api/documents` multipart `DocumentMutationDto` | Creates a new project document and version; carries project and optional source template/version fields. |
| Download a template or project document version | `GET /api/documents/{id}/download?version={versionNumber}` | Returns the requested stored version through the existing storage provider. |
| Add a document version | `POST /api/documents/{id}/versions` | Adds a version; does not overwrite previous stored versions. |
| Read Phase Gate summary and details | `GET /api/projects/{projectId}/phase-gate/readiness?targetPhaseId={id}` | Returns readiness counts/status, project row-version token, available transitions, requirement rows, compliance state, file types, comparison time, validator version, and findings. There is no separate Sprint 2 compliance-detail endpoint. |
| Link or revalidate evidence | `POST /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document` with `{ documentId, documentVersionId }` | First link creates evidence; posting the same linked document/version again reruns and persists comparison. A different version conflicts until unlink. |
| Unlink evidence | `DELETE /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document` | Soft-deletes the active evidence relationship and preserves historical association. |
| Change phase | `POST /api/projects/{projectId}/phase-gate/change-phase` with `{ targetPhaseId, expectedProjectVersionToken }` | Requires separate phase authorization and concurrency token; incomplete readiness is recorded, not rejected. |

Sprint 2 does not add a Project Settings → Change Phase page or build Project, Program, or Portfolio compliance reports. The readiness and difference-detail response fields are the handover contract for Sprint 3's Project Settings checklist.

### Reused and extended persistence

- `Document` remains the logical file record. `ProjectId` associates a project document; existing `SourceTemplateId`, `SourceTemplateVersionId`, and source version number retain the template identity used when uploaded.
- `DocumentVersion` retains each stored version, provider ID, opaque storage key, file metadata, SHA-256, uploader, and version number.
- `ProjectPhaseGateSnapshot` and `ProjectPhaseGateRequirementSnapshot` preserve the configuration and pinned template/version history.
- `ProjectPhaseGateEvidence` links Tenant/Project/Organization, requirement snapshot, document, and exact document-version IDs. It persists compliance fields and soft-deletes old links instead of rewriting their history.
- No client-supplied checkbox controls requirement satisfaction or compliance. The service derives availability from the persisted document/version and storage metadata, then derives compliance from server-side validation/comparison.

## 6. Permissions, scope, and audit behavior

No new Sprint 2 permission key or existing role grant is introduced. The existing permission catalog remains authoritative.

| Operation | Required existing permissions | Scope and enforcement |
|---|---|---|
| Read documents, templates, download, or readiness/compliance details | `Document.View`; readiness also requires `Project.View` | Authenticated Tenant, authorized Project/Organization, and management scope. |
| Create Project document | `Document.Create` (existing catalog prerequisite: `Document.View`) | Trusted Tenant/management context and in-scope Project/Organization; template references must belong to that project's Organization. |
| Add/replace document version | `Document.Edit` (existing catalog prerequisite: `Document.View`) | Existing document must be accessible in the caller's Tenant/management scope. |
| Link, revalidate, or unlink Phase Gate evidence | `Project.View` + `Document.View` + `Document.Edit` | Project, requirement snapshot, Tenant, Organization, document, and exact version are checked together. |
| Change phase | `Project.View` + `ProjectPhase.Change` | Project management scope, Active Deny, active in-scope target phase, and expected project row-version; `Document.View` is not required. |
| Manage reusable templates | Existing `Document.Create` + `Document.TemplateManage` path (with its `Document.View` catalog prerequisite) | Organization-scoped template access; no privileged role is inferred by name. |

Evidence audit actions include link, compliance revalidation, and unlink. They capture the project/Tenant, requirement snapshot, exact document/version, pinned template/version, compliance status, expected/detected types, comparison timestamp, validator version, actor, and timestamp. Phase-change audit captures the transition, configuration version, readiness outcome, requirement counts, and satisfied evidence references. A `Ready`/`Incomplete` audit outcome is informational; authorization remains the phase-change permission contract.

## 7. Migration status

| Gate | Status |
|---|---|
| EF entity/configuration fields | Present in `ProjectPhaseGateEvidence` and its EF configuration. |
| Additive migration source | `20261112171603_AddProjectPhaseGateEvidenceCompliance` adds comparison status, types, findings, timestamp, and validator version. |
| Model snapshot synchronization | **PASS** — offline EF model gate: 26 repository migrations, `EF Model synchronized: Yes`. |
| Migration SQL review | **PASS** — `20261112171603_AddProjectPhaseGateEvidenceCompliance` is additive only: five nullable columns plus `ComplianceStatus nvarchar(32) NOT NULL DEFAULT 'PendingValidation'` on `ProjectPhaseGateEvidence` (six columns total); no drops, data updates, indexes, or foreign keys. |
| Development target identity | **PASS** — `SERVERPROPERTY('ServerName')=Shady`, `DB_NAME()=NexusPmo`, database ID 5, Windows Authentication; the gate matched the API's effective Development endpoint. |
| `dbo.__EFMigrationsHistory` | **PASS** — 26/26 repository migrations applied, including `20261112171603`; **0 pending**. |
| Physical schema — Sprint 2 objects | **PASS** — `ProjectPhaseGateEvidence` and its six compliance columns are present; the gate's missing-object report lists no Sprint 2 object. |
| Physical schema — overall | **NOT VERIFIED** — 15 pre-existing baseline drift objects in `IntegrationConnections`, `IntegrationWebhooks`, and `Programs` (nvarchar(max) where the model defines bounded lengths; four missing indexes). Outside Sprint 2; not modified. |
| Migrations applied | **None during this closure** — the target migration was already applied; a read-only preflight confirmed history and 0 pending. No SQL script was generated or executed. |
| Backup | **Not requested** for Development. |
| Required seed mode/idempotency | **Not applicable** to this Sprint 2 persistence change; no seed operation was performed. |

**Evidence:** model report `artifacts/ef-review/dev-db-preflight-model-20261112T202550Z-06f500f5.json`; inspection report `artifacts/ef-review/dev-db-preflight-inspect-20261112T202606Z-6429ebf2.json`; test runs `.tmp/test-results/defect-fixed.trx` and `.tmp/test-results/sprint2-closure.trx`.

Do not edit or rewrite the completed Sprint 1 migration as part of Sprint 2. Do not apply a new migration without explicit authorization and the repository's documented Development EF workflow.

## 8. Regression coverage and actual verification status

Focused source tests are present for content-derived PDF/DOC/DOCX/XLS/XLSX detection, incompatible and spoofed content, malformed formats, file-size handling, DOCX/XLSX comparison, identity mismatch, persisted findings/revalidation, project document version history, permission boundaries, cross-scope references, and non-blocking phase changes. Relevant test files include:

- `apps/api/tests/NEXUS.Api.Tests/Security/DocumentFileTypeValidatorTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/DocumentTemplateComparisonTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/ProjectDocumentTemplateAssociationHistoryTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Security/ProjectPhaseGateAuthorizationBoundaryTests.cs`
- `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectPhaseGateLifecycleTests.cs`

| Verification category | Consolidated result |
|---|---|
| API project build | **PASS** — `NEXUS.Api` and `NEXUS.Api.Tests` compiled with 0 errors (pre-existing analyzer/nullable warnings only). |
| Defect regression | **PASS** — `Matching_document_structure_without_source_template_identity_is_not_reported_compliant` now passes 1/1 (exactly one `TemplateIdentityNotVerified` finding). |
| Focused API regression tests | **PASS — 69 passed, 0 failed, 0 skipped** across the five focused test files. `DocumentFileTypeValidatorTests` 27/27, `DocumentTemplateComparisonTests` 13/13, `ProjectDocumentTemplateAssociationHistoryTests` 3/3, `ProjectPhaseGateAuthorizationBoundaryTests` 10/10, `ProjectPhaseGateLifecycleTests` 16/16. Testhost remained stable; no runner crash. |
| Testhost / VSTest | **PASS** — generated `NEXUS.Api.Tests.runtimeconfig.json` contains `"MSTest.EnableParentProcessQuery": false`; all 69 tests were discovered and executed; no `Win32Exception 5`, `Access is denied`, or testhost-connection error appears in the run logs. |
| EF model synchronization | **PASS** — `EF Model synchronized: Yes`; 26 repository migrations; offline model check opened no database. |
| Development identity and history | **PASS** — `Shady` / `NexusPmo` (database ID 5), Windows Authentication; 26/26 applied, 0 pending, including the Sprint 2 migration. |
| Migration applied | **YES (already applied)** — verified in `dbo.__EFMigrationsHistory`; no write performed in this closure. |
| Physical schema — Sprint 2 | **PASS** — compliance columns present and not reported missing by the gate. |
| Physical schema — overall | **NOT VERIFIED** — 15 unrelated baseline drift objects (Integration/Programs) remain; gate exit code 2 for that reason only. |
| Backup | **Not requested** for Development. |
| Required seed mode/idempotency | **Not applicable** to this persistence change; no seed operation was performed. |
| SQL-backed persistence tests | **NOT RUN** — no SQL Server persistence test was executed. |
| Focused frontend typecheck | **PASS** — `npm run typecheck` (exit 0). |
| Focused frontend tests | **PASS** — `npm test -- src/test/project-phase-gate-side-sheet.test.tsx src/test/document-center-parity.test.tsx src/test/api-error-localization.test.ts src/test/audit-phase-gate-localization.test.tsx`; 4 files, 31 tests. |
| EF model and migration gate | **PASS** — `scripts/database/ef-database-gate.ps1 model`; 26 repository migrations, model synchronized, database not opened. `NU1900` vulnerability-feed warning only. |
| `git diff --check` | **PASS** — no whitespace errors; Git reported line-ending normalization warnings only. |
| Authenticated runtime/API UAT | **NOT RUN** |
| Product Owner browser UAT | **NOT RUN** |

### Defect correction — duplicate `TemplateIdentityNotVerified` (fixed 2026-11-12)

**Test:** `ProjectDocumentTemplateAssociationHistoryTests.Matching_document_structure_without_source_template_identity_is_not_reported_compliant`

**Root cause:** when the uploaded document had no complete source-template reference, `ProjectPhaseGateReadinessService` called the three-argument `DocumentTemplateComparison.Compare(stream, stream, type)` overload. That overload is a structure-only entry point and, without an expected reference, emitted "The expected template version identity is missing or incomplete" (`TemplateIdentityNotVerified`). The service then added its own "The source template identity is missing…" finding with the same code, producing two identity findings for one uncertainty — one of them misleading.

**Fix (production):** `ProjectPhaseGateReadinessService` now always calls the identity-aware overload with the pinned `new DocumentTemplateReference(requirement.TemplateId, requirement.TemplateVersionId)` and passes `uploadedSourceTemplate: null` when the upload has no complete source reference. The comparator therefore emits exactly one `TemplateIdentityNotVerified` ("…does not contain a complete source template and version reference…"). The service no longer adds a second identity finding. When a confirmed partial mismatch exists (`SourceTemplateId` differs, or the same template with a different `SourceTemplateVersionNumber`), the service replaces that comparator finding with exactly one `TemplateIdentityMismatch` and sets `Mismatch`. The legacy three-argument comparison contract and its dedicated test are unchanged.

**Verification:** the previously failing test passes 1/1; the full five-suite closure run is **69/69**; the file-type, comparison, authorization-boundary, and lifecycle suites are unaffected.

### Earlier test-fixture corrections (retained)

The three authentication failures and two lifecycle failures reported earlier were test-fixture defects, not product regressions:

- The association fixture built `HttpContextAccessor.HttpContext` inside an async factory, so the `AsyncLocal` value was not visible to the awaiting test and `RequireProjectAccess` returned Unauthorized. The fixture now re-activates its principal in the caller's context.
- The lifecycle cross-tenant case restored a tenant accessor without permission claims, producing a 403 during the post-restore readiness read. The helper now carries `Project.View` and `Document.View`.
- The lifecycle "ready" fixture never persisted the pinned template/document-version rows, and its storage mock could not open file content, so the mandatory requirement was marked invalid. The fixture now seeds the pinned template version and provides readable content metadata.

## 9. Subagent contributions and skills used

Assigned parallel workstreams covered Project Document Association, File Type Validation, Template Comparison & Compliance, and Authorization/Audit & Regression. The association review added an archived-template guard that accepts a pinned archived version only for the same Project and Organization. The file-type workstream supplied content-based detection and focused tests; the comparison workstream supplied DOCX/XLSX structural findings and tests for missing identity. Authorization review confirmed reuse of existing permissions, management scope, and Active Deny, and identified the out-of-scope Program/Portfolio compliance report routes/UI; those report additions were removed from Sprint 2. Test triage corrected the identity-comparison test cases. The Main Agent integrated the changes, reconciled the migration/source docs, and retained only the Project-level readiness/details contract.

Main Agent skills applied in this closeout: documentation-first task execution; EF database delivery gate and EF Core patterns; secure coding standards; frontend data integrity.

### Closure-task contributions (2026-11-12)

- **Agent A (`defect-fixer`)** — implemented the single-finding correction in `ProjectPhaseGateReadinessService`; static tracing confirmed the failing test path while leaving the legacy three-argument comparator contract intact.
- **Agent B (Main Agent)** — executed the defect regression (1/1) and the five focused suites (**69/69**), preserving the permanent testhost configuration.
- **Agent C (Main Agent)** — ran the documented host-side Development preflight: identity, Windows Authentication, 26/26 applied history, 0 pending, Sprint 2 columns present; reviewed the additive migration. No database write was performed.
- **Agent D (`security-reviewer`)** — independent read-only review: **PASS** on tenant/Organization/project isolation, template/version integrity, content-derived file-type enforcement, single-finding behavior, advisory compliance with Active Deny/granular permissions, absence of Program/Portfolio compliance UI, and the accurate malware-scanning limitation.
- **Prior testhost workstream** — `config-auditor` (PASS 6/6) and `docs-verifier` (PASS, no FAIL) validated the permanent VSTest configuration and documentation evidence.

### Files modified in this closure

- **Production:** `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateReadinessService.cs` (single-finding correction).
- **Test infrastructure:** `apps/api/tests/NEXUS.Api.Tests/NEXUS.Api.Tests.csproj` (testhost parent-query override), `apps/api/tests/NEXUS.Api.Tests/Security/ProjectDocumentTemplateAssociationHistoryTests.cs` and `apps/api/tests/NEXUS.Api.Tests/Integration/ProjectPhaseGateLifecycleTests.cs` (fixture corrections).
- **Documentation:** this closure, `PROJECT-PHASE-DOCUMENT-INTEGRATION.md`, `docs/quality/MASTER-UAT-CHECKLIST.md`, `docs/development/dotnet-sdk-troubleshooting.md`, `docs/development/local-setup.md`.
- No production API runtime/security configuration, permission key, migration source, or database schema was changed in this closure.

### Security limitations and residual risks

- **No malware scanning integration exists.** Uploads are validated for size, extension, content signature, structure, storage metadata, and SHA-256 only; any antivirus step must come from external storage/hosting and is unverified.
- **Identity evidence is declaration-based.** `SourceTemplate...` values are validated against an accessible same-Organization template/version but are not cryptographically bound; `Compliant` is advisory and reachable only for DOCX/XLSX with complete matching identity and clean supported structure checks.
- **Overall Development physical-schema parity is not verified** because of 15 pre-existing baseline drift objects in `IntegrationConnections`, `IntegrationWebhooks`, and `Programs`; Sprint 2 objects are verified.
- **Non-blocking phase transition is intentional:** an authorized actor can change phase with missing/mismatched/not-verified evidence; audit records the readiness outcome. A readiness GET can pin a snapshot (documented write-on-read).
- Browser/API UAT and SQL-backed persistence tests were **not run**.

## 10. Sprint 3 handover and roadmap boundary

Sprint 3 may use the existing Phase Gate readiness contract for the Project Settings → Change Phase checklist:

1. Read the current project/target-phase readiness using the existing readiness API and retain the returned project concurrency token.
2. Render configured requirements and their independent checklist state, compliance status, expected/detected types, localized difference findings, and pinned template version.
3. Use existing template download, Project Documents upload, link/unlink, and revalidation APIs. Do not create a second document store or a client-side compliance override.
4. Submit phase changes using the server-issued current project version token and the existing `Project.View` + `ProjectPhase.Change` policy. A missing, mismatched, or `NotVerified` document must never disable an otherwise authorized transition.
5. Respect `Document.View` boundaries: when the user can change phase but cannot read documents, keep document content/details hidden while leaving the separately authorized phase action available.

Do not build the new Change Phase screen in Sprint 2. Keep the protected Pre-Initiation phase and Sprint 1 phase/category behavior intact. Keep **Sprint 4 Final Cleanup** in the roadmap; Sprint 2 does not start or absorb it.

## 11. Final Sprint 2 closure decision

**Decision: COMPLETE — Sprint 2 is closed with documented limitations.**

| Success criterion | Result |
|---|---|
| 1. Duplicate `TemplateIdentityNotVerified` finding fixed | **PASS** — exactly one identity finding; a confirmed mismatch yields exactly one `TemplateIdentityMismatch`. |
| 2. Focused Sprint 2 API suites execute | **PASS** — 69 discovered, 69 passed, 0 failed, 0 skipped. |
| 3. Testhost stable with the permanent configuration | **PASS** — `MSTest.EnableParentProcessQuery=false`; no runner crash. |
| 4. EF Model/Snapshot synchronized | **PASS** — 26 migrations, `EF Model synchronized: Yes`. |
| 5. Sprint 2 migration applied and verified on Development | **PASS for Sprint 2 objects** — 26/26 applied, 0 pending, compliance columns present. Overall schema parity remains qualified by 15 unrelated baseline drift objects. |
| 6. Security and non-blocking compliance rules intact | **PASS** — independent review; Tenant/Organization/Project isolation, permissions/Active Deny, advisory compliance. |
| 7. Closure documentation reflects evidence and limitations | **PASS** — this closure, the integration document, and the Master UAT checklist updated. |

**Not claimed:** browser/API runtime UAT, SQL-backed persistence tests, malware scanning, full physical-schema parity, or Sprint 3 work. Sprint 3 handover remains in §10.

## Related documentation

- [Project Phase and Document Integration](PROJECT-PHASE-DOCUMENT-INTEGRATION.md)
- [Sprint 1 closure](PROJECT-PHASE-DOCUMENT-SPRINT1-CLOSURE.md)
- [Master UAT Checklist](../quality/MASTER-UAT-CHECKLIST.md)
