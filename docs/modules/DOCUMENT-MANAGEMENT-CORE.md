# Document Management Core

NEXUS uses one tenant-scoped document repository. The global Document Centre and the Project Documents tab are two views over the same `Document` and `DocumentVersion` records; project linkage is stored on the document rather than duplicated in a project-specific table.

## Runtime contract

- `GET /api/documents` applies search, project, category, template, archive, pagination, tenant, and management-scope filters on the server.
- `GET /api/documents/{id}`, `/download`, and `/preview` authorize the document before returning metadata or bytes.
- `POST /api/documents` stores an immutable version-1 object; `POST /api/documents/{id}/versions` stores a new object and advances the current version.
- `POST /api/documents/{id}/archive` is a non-destructive archive operation.
- Files are restricted to PDF, Office Open XML, images, TXT, and CSV, with a 25 MB limit.

The local provider stores opaque relative keys below the configured `Storage:DocumentsRoot`; it rejects rooted paths and traversal outside that root. A future blob provider can implement the same `IStorageProvider` contract without changing the domain or UI.

## Scope and localization

Tenant isolation remains provided by the shared persistence query filters. Document creation also assigns the trusted authenticated tenant to the document and its first version, and validates category references through the tenant-filtered category set. A selected management context further limits project-linked documents, including project-linked templates, to the current `Scope.ProjectIds`; unlinked ordinary documents are visible only to their owner, while unlinked templates are shared within that Tenant. Missing or mismatched tenant/management context fails closed for document records and mutations. The UI uses server results, supports English and Arabic/RTL, and formats numeric counts and file sizes with western digits.

`ManagementContextMiddleware` rejects all document mutations while the active context is `ReadOnlyViewAs`; the frontend also removes upload, new-version, archive, and template-management controls in that mode. Read actions remain available within the resolved tenant and management scope.

## Templates

Templates are real document records marked `IsTemplate`. Unlinked templates are shared tenant-wide; a project-linked template remains visible only inside its Project management scope. The library intentionally does not seed fabricated files. An administrator or PMO administrator can upload an actual reusable file and publish it as a template; it then appears in the Templates tab and is downloadable through the same authorized path.

Project Documents provides `Start from template`: it lists active templates, downloads the selected blank file, and uploads the completed project copy with the authoritative `SourceTemplateId` and `SourceTemplateVersionNumber`. Those fields are immutable after creation, so a later template version does not change an existing project document's historical source.

## Explicit non-scope

Workflow approvals, e-signature, Office editing, cloud integrations, notifications, retention automation, and a separate Activity/Permissions product area are not part of this foundation.
