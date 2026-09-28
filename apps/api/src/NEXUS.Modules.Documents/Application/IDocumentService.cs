using Microsoft.AspNetCore.Http;
using NEXUS.SharedKernel;
using NEXUS.Modules.Documents.Domain;

namespace NEXUS.Modules.Documents.Application;

public interface IDocumentService
{
    Task<Result<DocumentDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentListItemDto>> GetPagedAsync(DocumentListQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DocumentCategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Result<DocumentDetailDto>> CreateAsync(DocumentMutationDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<DocumentDetailDto>> AddVersionAsync(Guid id, IFormFile file, string? changeNotes, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<DocumentDownload>> GetDownloadAsync(Guid id, int? versionNumber, CancellationToken cancellationToken = default);
}

public sealed record DocumentDownload(byte[] Content, string FileName, string ContentType);
