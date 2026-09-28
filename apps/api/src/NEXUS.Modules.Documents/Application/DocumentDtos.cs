using Microsoft.AspNetCore.Http;

namespace NEXUS.Modules.Documents.Application;

public sealed record DocumentListQuery(int Page = 1, int PageSize = 20, Guid? ProjectId = null, Guid? CategoryId = null, bool TemplatesOnly = false, bool IncludeArchived = false, string? Search = null);
public sealed record DocumentCategoryDto(Guid Id, string Name, string? Code, string? Description, int SortOrder);
public sealed record DocumentVersionDto(Guid Id, int VersionNumber, string? FileName, long FileSize, string? ContentType, string? ChangeNotes, Guid? UploadedBy, DateTime UploadedAt, bool IsCurrent);
public sealed record DocumentListItemDto(Guid Id, string Name, string? Description, string? FileName, string? FileType, long? FileSize, string? ContentType, Guid? ProjectId, Guid? CategoryId, string? CategoryName, string? Status, int Version, Guid? OwnerId, bool IsTemplate, bool IsArchived, Guid? SourceTemplateId, int? SourceTemplateVersionNumber, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record DocumentDetailDto(DocumentListItemDto Document, IReadOnlyList<DocumentVersionDto> Versions);
public sealed record DocumentMutationDto(string Name, string? Description, Guid? ProjectId, Guid? CategoryId, bool IsTemplate = false, Guid? SourceTemplateId = null, int? SourceTemplateVersionNumber = null, string? ChangeNotes = null, IFormFile? File = null);
