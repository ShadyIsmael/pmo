# UPI Task Context Manifest

Task: **Inspect SeedDataProvider.CreateAndVerifyDevelopmentBackupAsync SQL result type and focused regression tests; prepare full Development reset using documented EF database gate and official minimal seed command, with identity guards and post-reset Admin verification**
Primary module: **documents** (task-explicit-alias)
Architecture snapshot: `20261124T143831-c08b56f`

## Selected source reading

| File | Range(s) | Tokens | Why |
|---|---|---:|---|
| `apps/api/src/NEXUS.Modules.Documents/Application/DocumentContentTypes.cs` | 1-36 | 416 | filename-match:type, primary-module, symbol-match:DocumentContentTypes |
| `apps/api/src/NEXUS.Modules.Documents/Application/IStorageProvider.cs` | 1-72 | 1106 | filename-match:provider, primary-module, symbol-match:IStorageProvider |
| `apps/api/src/NEXUS.Modules.Documents/Infrastructure/LocalStorageProvider.cs` | 1-295 | 3732 | filename-match:provider, primary-module, symbol-match:LocalStorageProvider |
| `apps/api/src/NEXUS.Modules.Documents/Application/IArchivedProjectTemplateReferenceValidator.cs` | 1-16 | 154 | filename-match:ef, primary-module |
| `apps/api/src/NEXUS.Modules.Documents/Application/IDocumentCategoryReferenceValidator.cs` | 1-16 | 152 | filename-match:ef, primary-module |
| `apps/api/tests/NEXUS.Api.Tests/Security/DocumentContentTypesTests.cs` | 1-53 | 624 | filename-match:type, primary-module, symbol-match:DocumentContentTypesTests |
| `apps/api/tests/NEXUS.Api.Tests/Security/DocumentsStorageProviderRegistrationTests.cs` | 1-33 | 383 | filename-match:provider, primary-module, symbol-match:DocumentsStorageProviderRegistrationTests |
| `apps/api/src/NEXUS.Modules.Documents/Api/DocumentUploadRequestSizeLimitAttribute.cs` | 13-22 | 169 | primary-module, symbol-match:OnResourceExecutionAsync |
| `apps/api/src/NEXUS.Modules.Documents/Application/DocumentTemplateComparison.cs` | 21-21, 117-124, 128-134, 176-176 | 215 | primary-module, symbol-match:DocumentTemplateComparisonResult |
| `apps/api/src/NEXUS.Modules.Documents/Application/IDocumentService.cs` | 9-9, 10-10, 11-11, 12-12, 13-13 | 221 | primary-module, symbol-match:GetByIdAsync |
| `apps/api/src/NEXUS.Modules.Documents/Infrastructure/DocumentRepository.cs` | 15-21 | 88 | primary-module, symbol-match:GetByIdAsync |
| `apps/api/src/NEXUS.Modules.Documents/ModuleRegistration/DocumentsModuleRegistration.cs` | 17-20 | 38 | primary-module, symbol-match:LocalStorageProvider |
| `apps/api/tests/NEXUS.Api.Tests/Integration/DocumentCategorySharedScopeTests.cs` | 24-147 | 2060 | primary-module, symbol-match:Tenant_shared_seed_categories_are_listed_for_an_authorized_organization_and_can_be_edited_in_place |
| `apps/api/tests/NEXUS.Api.Tests/Security/DocumentCategoryAuthorizationContractTests.cs` | 90-102 | 230 | primary-module, symbol-match:Category_delete_permission_is_assignable_and_part_of_the_protected_admin_baseline |

## Dependency awareness (not automatically read)

- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentCategoryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentDtos.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentFileTypeValidator.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentService.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentUploadLimits.cs` — REFERENCES_TYPE (inferred-token-match; DocumentUploadLimits)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentUploadLimits.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/TemplateDeletionPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Application)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentFileTypeValidator.cs` — REFERENCES_TYPE (inferred-token-match; DocumentFileType)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Entities/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.SharedKernel/Result.cs` — REFERENCES_TYPE (inferred-token-match; Result)
- `modules/projects/ProjectBaselineVersion.cs` — REFERENCES_TYPE (inferred-token-match; Status)
- `apps/api/src/NEXUS.Modules.Documents/Application/DocumentDtos.cs` — REFERENCES_TYPE (inferred-token-match; DocumentCategoryCreateRequest)
- `apps/api/src/NEXUS.Modules.Documents/Domain/Document.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentCategory.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentReview.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/DocumentVersion.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.Documents/Domain/TemplateDeletionRequest.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.Modules.Documents.Domain)
- `apps/api/src/NEXUS.Modules.ProjectManagement/Domain/Project.cs` — REFERENCES_TYPE (inferred-token-match; Project)
- `apps/api/src/NEXUS.SharedKernel/ActualWorkCostLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/ApprovalReviewDto.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/AssignmentRecoveryPermissions.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseDomainEvent.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseEntity.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/BaseValueObject.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/EntityCodeSequence.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/Error.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAggregateRoot.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IApprovalDelegationEligibilityLookup.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)
- `apps/api/src/NEXUS.SharedKernel/IAuditable.cs` — USES_NAMESPACE (inferred-namespace; NEXUS.SharedKernel)

## Critical path coverage

- Not applicable to this task.

## Budget

- Initial selected source: **9588** tokens (method varies per file)
- Requested planning budget: **16000** tokens
- Other-context allowance: **6400** tokens

Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.
