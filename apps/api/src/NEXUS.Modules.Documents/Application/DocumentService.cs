using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Documents.Application;

public sealed class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".xlsx", ".pptx", ".png", ".jpg", ".jpeg", ".txt", ".csv" };
    private const long MaxFileSize = 25 * 1024 * 1024;
    private readonly DbContext _context;
    private readonly IStorageProvider _storage;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService? _audit;

    public DocumentService(DbContext context, IStorageProvider storage, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor? managementContext = null, IAuditService? audit = null)
    {
        _context = context;
        _storage = storage;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
        _audit = audit;
    }

    public async Task<Result<DocumentDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await AccessibleQuery().Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return document is null ? Result<DocumentDetailDto>.Failure(Error.NotFound("Document not found")) : Result<DocumentDetailDto>.Success(ToDetail(document));
    }

    public async Task<PagedResult<DocumentListItemDto>> GetPagedAsync(DocumentListQuery request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = AccessibleQuery();
        if (request.ProjectId.HasValue) query = query.Where(d => d.ProjectId == request.ProjectId);
        if (request.CategoryId.HasValue) query = query.Where(d => d.CategoryId == request.CategoryId);
        if (request.TemplatesOnly) query = query.Where(d => d.IsTemplate);
        if (!request.IncludeArchived) query = query.Where(d => !d.IsArchived);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(d => d.Name.Contains(search) || (d.FileName != null && d.FileName.Contains(search)) || (d.Tags != null && d.Tags.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var documents = await query.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(cancellationToken);
        var categoryIds = documents.Where(d => d.CategoryId.HasValue).Select(d => d.CategoryId!.Value).Distinct().ToList();
        var categories = await _context.Set<DocumentCategory>().Where(c => categoryIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return new PagedResult<DocumentListItemDto>(documents.Select(d => ToListItem(d, categories.GetValueOrDefault(d.CategoryId ?? Guid.Empty))).ToList(), page, pageSize, total);
    }

    public async Task<Result<IReadOnlyList<DocumentCategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetTrustedTenantId(out _))
            return Result<IReadOnlyList<DocumentCategoryDto>>.Failure(Error.Forbidden("A valid tenant context is required."));

        var categories = await _context.Set<DocumentCategory>().AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<DocumentCategoryDto>>.Success(categories.Select(c => new DocumentCategoryDto(c.Id, c.Name, c.Code, c.Description, c.SortOrder)).ToList());
    }

    public async Task<Result<DocumentDetailDto>> CreateAsync(DocumentMutationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (dto.File is null) return Result<DocumentDetailDto>.Failure(Error.Validation("A file is required."));
        var validation = ValidateFile(dto.File);
        if (validation is not null) return Result<DocumentDetailDto>.Failure(validation);
        if (dto.IsTemplate && !(_httpContextAccessor.HttpContext?.User.HasClaim("permission", DocumentPermissions.TemplateManage) ?? false))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Template management permission is required."));
        if (!CanUseProject(dto.ProjectId)) return Result<DocumentDetailDto>.Failure(Error.Forbidden("Project is outside the current management scope."));
        if (dto.CategoryId.HasValue && !await _context.Set<DocumentCategory>().AnyAsync(c => c.Id == dto.CategoryId.Value, cancellationToken))
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected document category is not available."));
        if (dto.SourceTemplateId.HasValue)
        {
            var sourceTemplate = await AccessibleQuery()
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == dto.SourceTemplateId.Value && d.IsTemplate && !d.IsArchived, cancellationToken);
            if (sourceTemplate is null)
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected source template is not available."));

            var sourceVersion = dto.SourceTemplateVersionNumber ?? sourceTemplate.Version;
            if (!sourceTemplate.Versions.Any(v => v.VersionNumber == sourceVersion))
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected source template version is not available."));
        }

        var document = new Document
        {
            TenantId = managementContext.TenantId,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? Path.GetFileNameWithoutExtension(dto.File.FileName) : dto.Name.Trim(),
            Description = dto.Description?.Trim(), ProjectId = dto.ProjectId, CategoryId = dto.CategoryId, IsTemplate = dto.IsTemplate,
            Status = "Active", Version = 1, OwnerId = userId, FileName = Path.GetFileName(dto.File.FileName),
            FileType = Path.GetExtension(dto.File.FileName).TrimStart('.').ToUpperInvariant(), FileSize = dto.File.Length,
            ContentType = DocumentContentTypes.ForFileName(dto.File.FileName), SourceTemplateId = dto.SourceTemplateId, SourceTemplateVersionNumber = dto.SourceTemplateVersionNumber,
            CreatedBy = userId.ToString()
        };
        await using var stream = new MemoryStream();
        await dto.File.CopyToAsync(stream, cancellationToken);
        var storageKey = await _storage.SaveAsync($"v1-{Guid.NewGuid():N}{Path.GetExtension(document.FileName)}", stream.ToArray(), $"{userId:N}/{document.Id:N}", cancellationToken);
        document.StoragePath = storageKey;
        document.Versions.Add(new DocumentVersion { TenantId = managementContext.TenantId, DocumentId = document.Id, VersionNumber = 1, FileName = document.FileName, FileSize = dto.File.Length, ContentType = DocumentContentTypes.ForFileName(dto.File.FileName), StoragePath = storageKey, ChangeNotes = dto.ChangeNotes, UploadedBy = userId, UploadedAt = DateTime.UtcNow, IsCurrent = true, CreatedBy = userId.ToString() });
        _context.Set<Document>().Add(document);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
        await LogAsync("Create", document, userId, cancellationToken);
        return Result<DocumentDetailDto>.Success(ToDetail(document));
    }

    public async Task<Result<DocumentDetailDto>> AddVersionAsync(Guid id, IFormFile file, string? changeNotes, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var validation = ValidateFile(file);
        if (validation is not null) return Result<DocumentDetailDto>.Failure(validation);
        var document = await AccessibleQuery().Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null) return Result<DocumentDetailDto>.Failure(Error.NotFound("Document not found"));
        if (document.IsArchived) return Result<DocumentDetailDto>.Failure(Error.Validation("Archived documents cannot receive new versions."));
        var nextVersion = document.Versions.Count == 0 ? document.Version + 1 : document.Versions.Max(v => v.VersionNumber) + 1;
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var storageKey = await _storage.SaveAsync($"v{nextVersion}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}", stream.ToArray(), $"{userId:N}/{document.Id:N}", cancellationToken);
        foreach (var version in document.Versions) version.IsCurrent = false;
        document.Version = nextVersion; document.FileName = Path.GetFileName(file.FileName); document.FileType = Path.GetExtension(file.FileName).TrimStart('.').ToUpperInvariant(); document.FileSize = file.Length; document.ContentType = DocumentContentTypes.ForFileName(file.FileName); document.StoragePath = storageKey; document.UpdatedBy = userId.ToString();
        document.Versions.Add(new DocumentVersion { TenantId = managementContext.TenantId, DocumentId = document.Id, VersionNumber = nextVersion, FileName = document.FileName, FileSize = file.Length, ContentType = DocumentContentTypes.ForFileName(file.FileName), StoragePath = storageKey, ChangeNotes = changeNotes, UploadedBy = userId, UploadedAt = DateTime.UtcNow, IsCurrent = true, CreatedBy = userId.ToString() });
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
        await LogAsync("NewVersion", document, userId, cancellationToken);
        return Result<DocumentDetailDto>.Success(ToDetail(document));
    }

    public async Task<Result> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var document = await AccessibleQuery().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null) return Result.Failure(Error.NotFound("Document not found"));
        document.IsArchived = true; document.Status = "Archived"; document.UpdatedBy = userId.ToString();
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Archive", document, userId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<DocumentDownload>> GetDownloadAsync(Guid id, int? versionNumber, CancellationToken cancellationToken = default)
    {
        var document = await AccessibleQuery().Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null) return Result<DocumentDownload>.Failure(Error.NotFound("Document not found"));
        var version = versionNumber.HasValue ? document.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber.Value) : document.Versions.FirstOrDefault(v => v.IsCurrent) ?? document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (version?.StoragePath is null || version.FileName is null) return Result<DocumentDownload>.Failure(Error.NotFound("Document content not found"));
        try { return Result<DocumentDownload>.Success(new DocumentDownload(await _storage.GetAsync(version.StoragePath, cancellationToken), version.FileName, DocumentContentTypes.ForFileName(version.FileName))); }
        catch (FileNotFoundException) { return Result<DocumentDownload>.Failure(Error.NotFound("Document content not found")); }
    }

    private IQueryable<Document> AccessibleQuery()
    {
        var query = _context.Set<Document>().Where(d => !d.IsDeleted);
        if (!TryGetTrustedManagementContext(out var scope) || scope is null)
            return query.Where(_ => false);

        // Materialize the in-memory scope as a primitive array before composing
        // the EF query. IReadOnlySet<T>.Contains is not translated reliably by
        // SQL Server providers, while array.Contains is translated to IN (...)
        // and remains server-side (including the empty-scope case).
        var projectIds = scope.Scope.ProjectIds.ToArray();
        return query.Where(d => (d.IsTemplate && !d.ProjectId.HasValue)
            || (d.ProjectId.HasValue && projectIds.Contains(d.ProjectId.Value))
            || (!d.IsTemplate && !d.ProjectId.HasValue && d.OwnerId == scope.UserId));
    }

    private bool CanUseProject(Guid? projectId) =>
        TryGetTrustedManagementContext(out var scope)
        && scope is not null
        && (!projectId.HasValue || scope.Scope.ProjectIds.Contains(projectId.Value));

    private bool TryGetTrustedManagementContext(out ManagementContext? context)
    {
        context = null;
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var userId)
            || userId == Guid.Empty
            || !TryGetTrustedTenantId(out var tenantId))
            return false;

        var current = _managementContext?.Current;
        if (current is null || current.UserId != userId || current.TenantId != tenantId)
            return false;

        context = current;
        return true;
    }

    private bool TryGetTrustedTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue("tenantId"), out tenantId)
            || tenantId == Guid.Empty)
        {
            tenantId = Guid.Empty;
            return false;
        }

        return true;
    }
    private static Error? ValidateFile(IFormFile file) => file.Length <= 0 || file.Length > MaxFileSize ? Error.Validation("File size must be between 1 byte and 25 MB.") : !AllowedExtensions.Contains(Path.GetExtension(file.FileName)) ? Error.Validation("File type is not supported.") : null;
    private static DocumentListItemDto ToListItem(Document d, string? categoryName) => new(d.Id, d.Name, d.Description, d.FileName, d.FileType, d.FileSize, DocumentContentTypes.ForFileName(d.FileName), d.ProjectId, d.CategoryId, categoryName, d.Status, d.Version, d.OwnerId, d.IsTemplate, d.IsArchived, d.SourceTemplateId, d.SourceTemplateVersionNumber, d.CreatedAt, d.UpdatedAt);
    private static DocumentDetailDto ToDetail(Document d) => new(ToListItem(d, null), d.Versions.OrderByDescending(v => v.VersionNumber).Select(v => new DocumentVersionDto(v.Id, v.VersionNumber, v.FileName, v.FileSize, DocumentContentTypes.ForFileName(v.FileName), v.ChangeNotes, v.UploadedBy, v.UploadedAt, v.IsCurrent)).ToList());
    private async Task LogAsync(string action, Document document, Guid userId, CancellationToken cancellationToken)
    {
        if (_audit is null) return;
        await _audit.LogAsync(new AuditEntry { EntityType = "Document", EntityId = document.Id, Action = action, UserId = userId, NewValues = $"{{\"name\":{System.Text.Json.JsonSerializer.Serialize(document.Name)},\"version\":{document.Version}}}", Timestamp = DateTime.UtcNow, CreatedBy = userId.ToString() }, cancellationToken);
    }
}
