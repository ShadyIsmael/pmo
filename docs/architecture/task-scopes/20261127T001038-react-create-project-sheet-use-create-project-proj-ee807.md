# UPI Task Context Manifest

Task: **React CreateProjectSheet useCreateProject project create request HTTP POST project API project detail page useProjectById phase label resolution on reload**
Primary module: **projects** (task-explicit-alias)
Architecture snapshot: `20261126T220221-d00a29d`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/web-react/src/features/delivery/WorkItemDetailsSideSheetFull.tsx` | 1-163 | 5955 | filename-match:detail, filename-match:id, filename-match:sheet, module-mentioned, primary-module, symbol-match:WorkItemDetailsSideSheetFull |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectPhaseGateArchivedTemplateReferenceValidator.cs` | 1-64 | 767 | filename-match:id, filename-match:phase, module-mentioned, primary-module, symbol-match:ProjectPhaseGateArchivedTemplateReferenceValidator |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateEvidence.cs` | 9-24 | 213 | filename-match:id, filename-match:phase, module-mentioned, primary-module, symbol-match:ProjectPhaseGateEvidence |
| `apps/web-react/src/test/project-change-request-sheet.test.tsx` | 29-33 | 85 | filename-match:request, filename-match:sheet, module-mentioned, primary-module, symbol-match:renderSheet |
| `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhase.cs` | 1-16 | 149 | filename-match:phase, module-mentioned, primary-module, symbol-match:ProjectPhase |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentCategoryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentContentTypes.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentFileTypeValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentTemplateComparison.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentUploadLimits.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/IArchivedProjectTemplateReferenceValidator.cs` — IMPLEMENTS_INTERFACE (source-class-declaration; ProjectPhaseGateArchivedTemplateReferenceValidator declares IArchivedProjectTemplateReferenceValidator)
- `apps/api/src/NEXUS.Modules.Documents/Application/IArchivedProjectTemplateReferenceValidator.cs` — REFERENCES_TYPE (inferred-token-match; IArchivedProjectTemplateReferenceValidator)
- `apps/api/src/NEXUS.Modules.Documents/Application/IArchivedProjectTemplateReferenceValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/IDocumentCategoryReferenceValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/IDocumentService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/IStorageProvider.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/TemplateDeletionPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — REFERENCES_TYPE (inferred-token-match; Document)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Baseline.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/CalendarPolicyConstants.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Deliverable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; ProjectId)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Milestone.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectChangeRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectChangeRequestApproval.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectHandoverRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectManagerAssignment.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectMember.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectMemberRole.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseCatalogEntry.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.ProjectManagement.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/ProjectPhaseGateRequirementSnapshot.cs` — REFERENCES_TYPE (inferred-token-match; ProjectPhaseGateRequirementSnapshot)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **7169** tokens (method varies per file)
- Requested planning budget: **12000** tokens
- Other-context allowance: **4800** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
