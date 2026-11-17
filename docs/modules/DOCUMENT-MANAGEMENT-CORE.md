# Document Management Core

NEXUS has one document-management module and one template catalog. **Governance / Document Center is the authoritative place to register and maintain organization-approved templates.** Organization Settings configures phase requirements by referencing template IDs and versions from that catalog; it does not maintain another template catalog. Project Documents remains the project-facing view over the shared document records and can create a project copy from an active template, but it does not create or manage approved templates.

Document Categories taxonomy is managed in **Administration → Reference Data → Document Categories** using the same `DocumentCategory` catalog. The Document Library consumes active categories for folders and filters. Register Approved Template loads active options through `GET /api/document-categories/template-options?organizationId=` using `Document.Create` and `Document.TemplateManage`. Project Document upload loads options through `GET /api/document-categories/project-upload-options?projectId=` using `Document.Create` and the authenticated project scope. These minimal upload lookups do not require broad `DocumentCategory.View` listing permission. Both return active, non-deleted categories owned by the selected Organization plus eligible Tenant-shared categories in the authenticated Tenant. The Library does not provide a second category-management surface or catalog. Categories remain descriptive metadata and do not determine Phase Gate requirements, which pin approved Template IDs and Template Version IDs.

## Document and template ownership

- A template is a document record marked as a template and owned by one active Organization within its authenticated Tenant. Tenant identity and the trusted management scope are resolved server-side; an organization ID supplied by a caller is only a selector and never proof of access.
- An ordinary document or project document retains its existing ownership and project links. Introducing organization-owned templates must not require copying or migrating existing Project Documents.
- A template ID remains stable as new immutable file versions are uploaded. Each version has its own stable version ID and provider-neutral file metadata. Archiving or deactivating a template does not delete versions or invalidate historical references. Organization Phase Gate requirements pin a template ID and exact version ID.
- Register Approved Template begins with a required Organization and then a required Document Category. Organization choices come from the caller's active management scope; changing Organization clears the selected Category and reloads active options from the existing category catalog. The API validates the selected Organization against Tenant and management scope and accepts an active Category owned by that Organization or an eligible Tenant-shared Category before storing the file. Project uploads derive Organization from the authorized Project and apply the same category eligibility rule.
- Project Documents may retain the source template ID and exact source version used for a completed copy. A later template version must not change that historical reference.
- Templates are selected from the Document Center catalog. A project upload is not a second template registration path.

## API and permission contract

The API authorizes each document read and mutation, validates the authenticated Tenant and current Organization/Project scope, and returns no local filesystem path or public storage URL.

| Operation | Required canonical permission |
|---|---|
| List/search templates and documents; read metadata/categories; preview/download | `Document.View` |
| Upload ordinary or project document | `Document.Create` |
| Load project-upload category options | `Document.Create` plus authenticated Project scope |
| Register an approved template | `Document.Create` and `Document.TemplateManage` |
| Load template-upload category options | `Document.Create`, `Document.TemplateManage`, and authorized Organization scope |
| Add a version to an ordinary document | `Document.Edit` |
| Add a template version or activate/deactivate a template | `Document.Edit` and `Document.TemplateManage` |
| Archive an ordinary document | `Document.Archive` |
| Archive a template | `Document.Archive` and `Document.TemplateManage` |

The document service rechecks the effective permission and explicit deny claims in addition to endpoint policies. All permissions are subject to Active Deny, Tenant isolation, resource-level scope, and applicable management-context restrictions. No role-based authorization fallback is allowed. Existing permissions are reused; this foundation adds no permission key and grants no new role access automatically.

## File storage

Document metadata and version records remain in SQL Server; file bytes live behind `IStorageProvider`. The provider contract stores a stream under an opaque key and exposes provider identity, open/read, metadata/existence, and deletion operations. Implementations are registered under their provider ID and selected through dependency injection. The Development `LocalStorageProvider` writes outside `wwwroot`, uses server-generated physical names, and calculates length and SHA-256 while streaming. No external File Server is connected by this foundation.

| Configuration | Default | Purpose |
|---|---|---|
| `Storage:Provider` | `local` | Selects a registered `IStorageProvider` implementation by provider ID. |
| `Storage:DocumentsRoot` | `{AppContext.BaseDirectory}/App_Data/documents` | Private local root for Development files; it must remain outside `wwwroot`. |
| `Storage:MaxUploadBytes` | 25 MiB; maximum configurable value 100 MiB | Maximum accepted upload size. |

Each new `DocumentVersion` stores provider ID, opaque key, original filename, content type, length, checksum, ownership, and version/audit data. New writes leave the legacy `StoragePath` fields null; those fields remain available for safe reads of existing rows. No physical local path is persisted for new content. Downloads and previews pass through the authenticated API after authorization; preview is limited to safe supported types and responses use `nosniff`. The local provider rejects rooted/traversal keys and filesystem links and tolerates cleanup of temporary files after a failed write.

New uploads are limited to `.doc`, `.docx`, `.xls`, `.xlsx`, and `.pdf`, with extension/MIME validation and the configured size limit enforced through the shared API service for templates and Project Documents. PDF/OLE signatures and the OOXML package manifest/main part are checked where applicable. Legacy image, TXT, and CSV rows remain readable, but those types are not accepted for new uploads. The served MIME type is derived from the stored safe filename, not trusted from the request. An uploaded template records its file and metadata; upload alone does not attest to an external signature or constitute a new in-system approval workflow. Business approval/signoff remains external.

## Phase Gate integration — Sprint 2 configuration

Organization Settings → Project Lifecycle stores requirements for a phase transition using the existing Document Center catalog. The initial supported transition is Pre-Initiation → Initiation. New or changed requirements can select only an active, unarchived template version owned by the same Tenant and Organization. Each requirement pins the stable Template ID and exact Template Version ID and snapshots the template name, category, and version label for historical display. Each configuration edit appends an immutable configuration version; Mandatory and Optional remain separate requirement types. Archiving or superseding a template does not delete earlier configuration versions or invalidate their pinned references. An unchanged pinned reference can be carried forward with its prior display snapshot.

The Organization API exposes supported transitions, the current configuration, immutable configuration history, and save operations under `/api/organizations/{organizationId}/phase-gate-requirements`. Read access uses `Settings.View` and `Document.View` plus Organization Settings read access. Saves use `Settings.ManageTenantConfiguration` and `Document.View` plus Organization Settings manage access. Tenant/Organization scope, Active Deny, rowversion concurrency, and audit recording are enforced by the configuration service.

Sprint 2 implements Organization configuration only. Project-level document checklists, automatic phase-transition validation, project phase changes, and compliance reporting remain outside this integration. Optional is not a governance waiver or exception. Uploaded templates are assumed to have received any required external business approval; no in-system document approval workflow is introduced.

### Persistence and verification status

The implementation is source-only and has not been deployed to a database. Migration source `20261110180235_AddOrganizationPhaseGateConfiguration` and the EF-generated model snapshot are present, but no SQL script was generated and no migration was applied. Final EF model synchronization remains Deferred/No; the API build, tests, database connection/schema verification, and authenticated runtime UAT are deferred to consolidated verification.

## Localization and existing project documents

The supported UI is `apps/web-react`. Document Center and Project Documents use the shared API-backed document module, English and Arabic labels, RTL-aware layout, theme support, and server-returned operational data. Existing project document upload, version, archive, download, and “Start from template” workflows remain supported through the same authorized service. Template management controls are available only in the Document Center and only when both the operation permission and `Document.TemplateManage` are effective where required.

## Project Lifecycle and Compliance integration - Sprints 3/4

Organization Phase Gate settings continue to be the sole configuration source, and Governance / Document Center remains the sole approved template source. New projects start at the server-owned stable phase code `pre-initiation`; Project Status remains independent and ordinary project updates do not accept a Phase override. Existing project phase values are not backfilled by this implementation.

The first project readiness request resolves the current Organization configuration for the project's current/target transition. When it exists, a project-level snapshot pins its configuration ID/version and copies the applicable requirements with stable Template ID/Version ID and display metadata. Later Organization edits do not rewrite an existing project snapshot. With no matching configuration, readiness is `NotConfigured`, the API stores no snapshot, and the phase-change action fails with a clear business error; unrelated project workflows do not require a configured gate.

Project checklist evidence associates one requirement snapshot with an existing project `Document` and an exact `DocumentVersion`. It reuses the existing document service and storage provider. The server validates Tenant, Organization, Project, source Template ID/Version ID, document/version availability, and storage-provider metadata on read and transition submission. Invalid, deleted, inaccessible, or unavailable evidence cannot satisfy a Mandatory requirement. No client-side checkbox or upload flag is authoritative. Mandatory requirements block the supported phase transition; Optional requirements never block. A configured gate with only Optional requirements can be Ready; an empty or unconfigured gate does not claim a misleading percentage. A successful phase change checks the Project concurrency token and writes transition details to the existing Audit Trail.

Project evidence is assumed to have received any required external business approval before submission. The application does not authenticate external signatures or implement reviewer assignment, approval routing, electronic sign-off, or waiver workflows. Optional requirements remain ordinary configuration and do not represent a governance exception. Successful transition and evidence changes reuse the existing Audit Trail; `ProjectPhase` and `ProjectPhaseGateEvidence` map to the Project Management audit module and have English/Arabic entity labels.

| Action | Canonical permission and scope |
|---|---|
| Read readiness/checklist | `Project.View` + `Document.View`, plus authenticated Tenant, Organization/Project, and management scope. |
| Change project phase | `ProjectPhase.Change` + `Document.View`, project-scoped; server validates mandatory evidence and expected Project concurrency token. The new sensitive key is not automatically granted. |
| Link/unlink evidence | `Project.View` + `Document.Edit` + `Document.View`, with exact project/document/version and Tenant/Organization boundaries. |
| View missing evidence names or filter by missing template in Program/Portfolio | `Project.View` + `Document.View`, in addition to the existing authorized management dashboard scope. Without both, requirement details are suppressed and a missing-template filter is Forbidden. |

The effective project readiness API is `GET /api/projects/{projectId}/phase-gate/readiness?targetPhase=...`. Evidence is linked or unlinked through `POST` / `DELETE /api/projects/{projectId}/phase-gate/requirements/{requirementSnapshotId}/document`; phase mutation is `POST /api/projects/{projectId}/phase-gate/change-phase` with `targetPhase` and the expected Project version token. These project APIs do not add a template catalog or an alternate document-upload repository.

Program and Portfolio dashboards read readiness in one authorized batch and return project status, current phase, target/configuration snapshot, Mandatory/Optional counts, readiness vs. actual transition status, phase distribution, and paged/filterable project rows. The applicable compliance denominator is Ready + Incomplete + NotConfigured; NotConfigured is not compliant and NotApplicable is excluded. Compliance is `Ready / Applicable`, null when the denominator is zero. Archived and Cancelled projects are excluded. Portfolio aggregation includes authorized projects through its Programs and directly assigned to the Portfolio exactly once.

### Sprint 3/4 schema and verification status

The Sprint 3/4 source is present but is not runtime-verified or Production-ready. Sprint 1 `20261110170000_AddOrganizationDocumentTemplateStorageMetadata`, Sprint 2 `20261110180235_AddOrganizationPhaseGateConfiguration`, and Sprint 3 `20261110190107_AddProjectPhaseGateLifecycle` migration sources remain unapplied. The Sprint 3 source and designer add project snapshots, requirement snapshots, and evidence persistence. No migration was applied. Sprint 1 EF model/snapshot confirmation remains outstanding; API build, backend/storage tests, consolidated React checks, EF synchronization, database schema inspection, and authenticated browser UAT are all Deferred. The Phase Gate v12 procedure document was unavailable in the workspace; no document-control requirements were inferred.
