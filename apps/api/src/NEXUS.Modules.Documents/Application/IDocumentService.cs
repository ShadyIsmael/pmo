using Microsoft.AspNetCore.Http;
using NEXUS.SharedKernel;
using NEXUS.Modules.Documents.Domain;

namespace NEXUS.Modules.Documents.Application;

public interface IDocumentService
{
    Task<Result<DocumentDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentListItemDto>> GetPagedAsync(DocumentListQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentCategoryDto>>> GetCategoriesAsync(Guid? organizationId = null, Guid? projectId = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ManagedDocumentCategoryDto>>> GetManagedCategoriesAsync(Guid organizationId, string? search, bool includeInactive, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentCategoryOptionDto>>> GetTemplateCategoryOptionsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentCategoryOptionDto>>> GetProjectUploadCategoryOptionsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ManagedDocumentCategoryDto>> GetManagedCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ManagedDocumentCategoryDto>> CreateCategoryAsync(DocumentCategoryCreateRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ManagedDocumentCategoryDto>> UpdateCategoryAsync(Guid id, DocumentCategoryUpdateRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ManagedDocumentCategoryDto>> SetCategoryStatusAsync(Guid id, bool isActive, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteCategoryAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentOrganizationDto>>> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<Result<DocumentDetailDto>> CreateAsync(DocumentMutationDto dto, Guid userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Associates an existing project document and a specific stored document version with an
    /// organization template version. This is an internal module contract for requirement-aware
    /// Project Management operations; it is not exposed as a standalone Documents API route.
    /// </summary>
    Task<Result<DocumentDetailDto>> LinkProjectDocumentToTemplateAsync(
        Guid projectId,
        Guid projectDocumentId,
        Guid projectDocumentVersionId,
        Guid templateId,
        Guid templateVersionId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<Result<DocumentDetailDto>> AddVersionAsync(Guid id, IFormFile file, string? changeNotes, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<DocumentDownload>> GetDownloadAsync(Guid id, int? versionNumber, CancellationToken cancellationToken = default);
}

public sealed record DocumentDownload(Stream Content, string FileName, string ContentType, long? Length);
