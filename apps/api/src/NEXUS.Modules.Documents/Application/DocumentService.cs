using System.Security.Claims;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Documents.Application;

public sealed class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx"
    };
    private static readonly byte[] OleSignature = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
    private const long MaximumContentTypesManifestBytes = 1024 * 1024;
    private const long MaximumOpenXmlMainPartBytes = DocumentUploadLimits.MaximumConfiguredBytes;
    private const int MaximumPdfIndexBytes = 1024 * 1024;
    private const int MaximumPdfXrefBytes = 16 * 1024;
    private const uint FreeSector = 0xffffffff;
    private const uint EndOfChain = 0xfffffffe;

    private readonly DbContext _context;
    private readonly IStorageProvider _storage;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService _audit;
    private readonly IArchivedProjectTemplateReferenceValidator? _archivedProjectTemplateReferenceValidator;
    private readonly long _maxUploadBytes;

    public DocumentService(
        DbContext context,
        IStorageProvider storage,
        IHttpContextAccessor httpContextAccessor,
        IAuditService audit,
        IManagementContextAccessor? managementContext = null,
        IConfiguration? configuration = null,
        IArchivedProjectTemplateReferenceValidator? archivedProjectTemplateReferenceValidator = null)
    {
        _context = context;
        _storage = storage;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
        _audit = audit;
        _archivedProjectTemplateReferenceValidator = archivedProjectTemplateReferenceValidator;
        _maxUploadBytes = DocumentUploadLimits.ResolveMaximumBytes(configuration);
    }

    public async Task<Result<DocumentDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.View))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Document view permission is required."));
        var document = await AccessibleQuery().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        var category = document is { CategoryId: Guid categoryId }
            ? await _context.Set<DocumentCategory>().AsNoTracking()
                .Where(item => item.Id == categoryId && item.TenantId == document.TenantId)
                .Select(item => new { item.Name, item.NameAr }).FirstOrDefaultAsync(cancellationToken)
            : null;
        return document is null
            ? Result<DocumentDetailDto>.Failure(Error.NotFound("Document not found"))
            : Result<DocumentDetailDto>.Success(ToDetail(document, GetOrganizationName(document.OrganizationId), category?.Name, category?.NameAr));
    }

    public async Task<PagedResult<DocumentListItemDto>> GetPagedAsync(DocumentListQuery request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        if (!HasEffectivePermission(DocumentPermissions.View))
            return new PagedResult<DocumentListItemDto>(Array.Empty<DocumentListItemDto>(), page, pageSize, 0);
        var query = AccessibleQuery();
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            query = query.Where(_ => false);

        var activeProjectTemplateLookup = request.TemplatesOnly && request.ForProjectId is not null;
        if (activeProjectTemplateLookup)
        {
            var projectId = request.ForProjectId.GetValueOrDefault();
            var resolvedOrganizationId = managementContext?.Scope.ProjectOrganizationIds.GetValueOrDefault(projectId);
            if (resolvedOrganizationId is null || !CanAccessOrganization(managementContext!, resolvedOrganizationId.Value))
            {
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(document => document.IsTemplate && document.OrganizationId == resolvedOrganizationId.Value);
            }
        }
        else if (request.ForProjectId.HasValue)
        {
            // The target-project selector is meaningful only for template lookup.
            query = query.Where(_ => false);
        }

        if (request.ProjectId.HasValue && !(request.TemplatesOnly && request.ForProjectId.HasValue))
            query = query.Where(document => document.ProjectId == request.ProjectId.Value);
        if (request.CategoryId.HasValue)
            query = query.Where(document => document.CategoryId == request.CategoryId.Value);
        if (request.OrganizationId.HasValue)
        {
            if (managementContext is null || !CanAccessOrganization(managementContext, request.OrganizationId.Value))
                query = query.Where(_ => false);
            else
                query = query.Where(document => document.OrganizationId == request.OrganizationId.Value);
        }
        if (request.TemplatesOnly)
            query = query.Where(document => document.IsTemplate && document.OrganizationId.HasValue);

        var status = request.Status?.Trim();
        if (activeProjectTemplateLookup)
            query = query.Where(document => !document.IsArchived);
        else if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(document => !document.IsArchived);
        else if (string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
            query = query.Where(document => document.IsArchived);
        else if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(_ => false);
        else if (!request.IncludeArchived)
            query = query.Where(document => !document.IsArchived);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(document => document.Name.Contains(search)
                || (document.FileName != null && document.FileName.Contains(search))
                || (document.Tags != null && document.Tags.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var documents = await query.OrderByDescending(document => document.UpdatedAt ?? document.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(cancellationToken);
        var categoryIds = documents.Where(document => document.CategoryId.HasValue)
            .Select(document => document.CategoryId!.Value).Distinct().ToArray();
        var categoryEntities = categoryIds.Length == 0
            ? new List<DocumentCategory>()
            : await _context.Set<DocumentCategory>().Where(category => category.TenantId == managementContext!.TenantId
                    && categoryIds.Contains(category.Id))
                .AsNoTracking().ToListAsync(cancellationToken);
        var categoryNames = categoryEntities.ToDictionary(category => category.Id, category => (string?)category.Name);
        var categoryNamesAr = categoryEntities.ToDictionary(category => category.Id, category => (string?)category.NameAr);
        var organizations = managementContext?.Scope.OrganizationReferences
            .GroupBy(organization => organization.Id)
            .ToDictionary(group => group.Key, group => group.First().Name)
            ?? new Dictionary<Guid, string>();
        var items = documents.Select(document => ToListItem(
            document,
            document.CategoryId.HasValue ? categoryNames.GetValueOrDefault(document.CategoryId.Value) : null,
            document.CategoryId.HasValue ? categoryNamesAr.GetValueOrDefault(document.CategoryId.Value) : null,
            document.OrganizationId.HasValue ? organizations.GetValueOrDefault(document.OrganizationId.Value) : null)).ToList();

        return new PagedResult<DocumentListItemDto>(items, page, pageSize, total);
    }

    public async Task<Result<IReadOnlyList<DocumentCategoryDto>>> GetCategoriesAsync(
        Guid? organizationId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.View))
            return Result<IReadOnlyList<DocumentCategoryDto>>.Failure(Error.Forbidden("Document view permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            return Result<IReadOnlyList<DocumentCategoryDto>>.Failure(Error.Forbidden("A valid tenant context is required."));
        if (projectId.HasValue)
        {
            if (!managementContext.Scope.ProjectIds.Contains(projectId.Value))
                return Result<IReadOnlyList<DocumentCategoryDto>>.Failure(Error.NotFound("The requested project was not found."));

            if (!managementContext.Scope.ProjectOrganizationIds.TryGetValue(projectId.Value, out var projectOrganizationId))
                return Result<IReadOnlyList<DocumentCategoryDto>>.Success(Array.Empty<DocumentCategoryDto>());

            if (!CanAccessOrganization(managementContext, projectOrganizationId)
                || (organizationId.HasValue && organizationId.Value != projectOrganizationId))
                return Result<IReadOnlyList<DocumentCategoryDto>>.Failure(Error.NotFound("The requested project was not found."));
            organizationId = projectOrganizationId;
        }
        if (!organizationId.HasValue)
            return Result<IReadOnlyList<DocumentCategoryDto>>.Success(Array.Empty<DocumentCategoryDto>());
        if (!CanAccessOrganization(managementContext, organizationId.Value))
            return Result<IReadOnlyList<DocumentCategoryDto>>.Failure(Error.NotFound("The selected organization was not found."));

        var categories = await _context.Set<DocumentCategory>().AsNoTracking()
            .Where(category => category.TenantId == managementContext.TenantId
                && category.OrganizationId == organizationId.Value
                && category.IsActive
                && !category.IsDeleted)
            .OrderBy(category => category.SortOrder).ThenBy(category => category.Name).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<DocumentCategoryDto>>.Success(categories
            .Select(category => new DocumentCategoryDto(category.Id, category.Name, category.Code, category.Description,
                category.SortOrder, category.OrganizationId, category.Name, category.NameAr, category.IsActive)).ToList());
    }

    public async Task<Result<IReadOnlyList<ManagedDocumentCategoryDto>>> GetManagedCategoriesAsync(
        Guid organizationId,
        string? search,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentCategoryPermissions.View))
            return Result<IReadOnlyList<ManagedDocumentCategoryDto>>.Failure(Error.Forbidden("Document category view permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            return Result<IReadOnlyList<ManagedDocumentCategoryDto>>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (organizationId == Guid.Empty || !CanAccessOrganization(managementContext, organizationId))
            return Result<IReadOnlyList<ManagedDocumentCategoryDto>>.Failure(Error.NotFound("The selected organization was not found."));

        var query = _context.Set<DocumentCategory>().AsNoTracking()
            .Where(category => category.TenantId == managementContext.TenantId
                && !category.IsDeleted
                && (category.OrganizationId == organizationId || category.OrganizationId == null));
        if (!includeInactive)
            query = query.Where(category => category.IsActive);
        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(category => category.Name.Contains(searchTerm)
                || (category.NameAr != null && category.NameAr.Contains(searchTerm))
                || (category.Description != null && category.Description.Contains(searchTerm))
                || (category.Code != null && category.Code.Contains(searchTerm)));

        var categories = await query.OrderBy(category => category.OrganizationId == null ? 0 : 1)
            .ThenBy(category => category.SortOrder).ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
        var usage = await GetCategoryUsageCountsAsync(
            categories.Select(category => category.Id).ToArray(),
            managementContext,
            [organizationId],
            cancellationToken);
        return Result<IReadOnlyList<ManagedDocumentCategoryDto>>.Success(categories
            .Select(category => ToManagedCategoryDto(category, usage.GetValueOrDefault(category.Id)))
            .ToList());
    }

    public async Task<Result<IReadOnlyList<DocumentCategoryOptionDto>>> GetTemplateCategoryOptionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Create)
            || !HasEffectivePermission(DocumentPermissions.TemplateManage))
            return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
                Error.Forbidden("Document create and template management permissions are required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
                Error.Forbidden("A valid tenant and management context are required."));
        if (organizationId == Guid.Empty || !CanAccessOrganization(managementContext, organizationId))
            return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
                Error.NotFound("The selected organization was not found."));

        var options = await GetActiveCategoryOptionsAsync(managementContext.TenantId, organizationId, cancellationToken);
        return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Success(options);
    }

    public async Task<Result<IReadOnlyList<DocumentCategoryOptionDto>>> GetProjectUploadCategoryOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Create))
            return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
                Error.Forbidden("Document create permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
                Error.Forbidden("A valid tenant and management context are required."));
        if (projectId == Guid.Empty
            || !managementContext.Scope.ProjectIds.Contains(projectId)
            || !managementContext.Scope.ProjectOrganizationIds.TryGetValue(projectId, out var organizationId)
            || !CanAccessOrganization(managementContext, organizationId))
            return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Failure(
                Error.NotFound("The requested project was not found."));

        var options = await GetActiveCategoryOptionsAsync(managementContext.TenantId, organizationId, cancellationToken);
        return Result<IReadOnlyList<DocumentCategoryOptionDto>>.Success(options);
    }

    private Task<List<DocumentCategoryOptionDto>> GetActiveCategoryOptionsAsync(
        Guid tenantId,
        Guid organizationId,
        CancellationToken cancellationToken) => _context.Set<DocumentCategory>().AsNoTracking()
        .Where(category => category.TenantId == tenantId
            && (category.OrganizationId == organizationId || category.OrganizationId == null)
            && category.IsActive
            && !category.IsDeleted)
        .OrderBy(category => category.OrganizationId == null ? 0 : 1)
        .ThenBy(category => category.SortOrder).ThenBy(category => category.Name)
        .Select(category => new DocumentCategoryOptionDto(category.Id, category.Name, category.NameAr))
        .ToListAsync(cancellationToken);

    public async Task<Result<ManagedDocumentCategoryDto>> GetManagedCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentCategoryPermissions.View))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("Document category view permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));

        var category = await _context.Set<DocumentCategory>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == managementContext.TenantId && !item.IsDeleted, cancellationToken);
        if (category is null || (category.OrganizationId.HasValue
            && !CanAccessOrganization(managementContext, category.OrganizationId.Value)))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.NotFound("Document category not found."));

        var accessibleOrganizationIds = managementContext.Scope.OrganizationReferences.Select(organization => organization.Id).ToArray();
        var usage = await GetCategoryUsageCountsAsync([category.Id], managementContext, accessibleOrganizationIds, cancellationToken);
        return Result<ManagedDocumentCategoryDto>.Success(ToManagedCategoryDto(category, usage.GetValueOrDefault(category.Id)));
    }

    public async Task<Result<ManagedDocumentCategoryDto>> CreateCategoryAsync(
        DocumentCategoryCreateRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentCategoryPermissions.Create))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("Document category create permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (request.OrganizationId == Guid.Empty || !CanAccessOrganization(managementContext, request.OrganizationId))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.NotFound("The selected organization was not found."));

        var names = NormalizeCategoryNames(request.NameEn, request.NameAr);
        if (names.Error is not null)
            return Result<ManagedDocumentCategoryDto>.Failure(names.Error);
        if (request.Description?.Length > 4000)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Validation("Category description cannot exceed 4,000 characters.", "description"));
        if (await CategoryNameExistsAsync(managementContext.TenantId, request.OrganizationId, names.NameEnKey!, names.NameArKey!, null, cancellationToken))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Conflict("A category with the same English or Arabic name already exists in this organization."));

        var category = new DocumentCategory
        {
            TenantId = managementContext.TenantId,
            OrganizationId = request.OrganizationId,
            Name = names.NameEn!,
            NameAr = names.NameAr!,
            NormalizedNameEn = names.NameEnKey!,
            NormalizedNameAr = names.NameArKey!,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedBy = userId.ToString()
        };
        _context.Set<DocumentCategory>().Add(category);
        var saved = await SaveCategoryAndAuditAsync(category, "Create", userId, null,
            new { category.Name, category.NameAr, category.Description, category.IsActive, category.OrganizationId },
            "Name,NameAr,Description,IsActive,OrganizationId", cancellationToken);
        if (saved.IsFailure)
            return Result<ManagedDocumentCategoryDto>.Failure(saved.Error!);
        return Result<ManagedDocumentCategoryDto>.Success(ToManagedCategoryDto(category, 0));
    }

    public async Task<Result<ManagedDocumentCategoryDto>> UpdateCategoryAsync(
        Guid id,
        DocumentCategoryUpdateRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentCategoryPermissions.Edit))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("Document category edit permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var category = await _context.Set<DocumentCategory>().FirstOrDefaultAsync(
            item => item.Id == id && item.TenantId == managementContext.TenantId && !item.IsDeleted, cancellationToken);
        if (category is null)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.NotFound("Document category not found."));
        if (!category.OrganizationId.HasValue)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Conflict("Legacy tenant-wide categories are read-only to preserve historical references."));
        if (!CanAccessOrganization(managementContext, category.OrganizationId.Value))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.NotFound("Document category not found."));

        var names = NormalizeCategoryNames(request.NameEn, request.NameAr);
        if (names.Error is not null)
            return Result<ManagedDocumentCategoryDto>.Failure(names.Error);
        if (request.Description?.Length > 4000)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Validation("Category description cannot exceed 4,000 characters.", "description"));
        if (await CategoryNameExistsAsync(managementContext.TenantId, category.OrganizationId.Value,
                names.NameEnKey!, names.NameArKey!, category.Id, cancellationToken))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Conflict("A category with the same English or Arabic name already exists in this organization."));

        var original = new { category.Name, category.NameAr, category.Description };
        var changedFields = new List<string>();
        if (category.Name != names.NameEn) changedFields.Add(nameof(category.Name));
        if (category.NameAr != names.NameAr) changedFields.Add(nameof(category.NameAr));
        var normalizedDescription = request.Description?.Trim();
        if (category.Description != normalizedDescription) changedFields.Add(nameof(category.Description));
        if (changedFields.Count == 0)
            return Result<ManagedDocumentCategoryDto>.Success(await ToManagedCategoryDtoAsync(category, managementContext, [category.OrganizationId.Value], cancellationToken));

        var changes = new
        {
            Name = new { from = category.Name, to = names.NameEn },
            NameAr = new { from = category.NameAr, to = names.NameAr },
            Description = new { from = category.Description, to = normalizedDescription }
        };
        category.Name = names.NameEn!;
        category.NameAr = names.NameAr!;
        category.NormalizedNameEn = names.NameEnKey!;
        category.NormalizedNameAr = names.NameArKey!;
        category.Description = normalizedDescription;
        category.UpdatedBy = userId.ToString();
        var saved = await SaveCategoryAndAuditAsync(category, "Update", userId, original, changes,
            string.Join(',', changedFields), cancellationToken);
        if (saved.IsFailure)
            return Result<ManagedDocumentCategoryDto>.Failure(saved.Error!);
        return Result<ManagedDocumentCategoryDto>.Success(await ToManagedCategoryDtoAsync(category, managementContext, [category.OrganizationId.Value], cancellationToken));
    }

    public async Task<Result<ManagedDocumentCategoryDto>> SetCategoryStatusAsync(
        Guid id,
        bool isActive,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentCategoryPermissions.ActivateDeactivate))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("Document category activation permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var category = await _context.Set<DocumentCategory>().FirstOrDefaultAsync(
            item => item.Id == id && item.TenantId == managementContext.TenantId && !item.IsDeleted, cancellationToken);
        if (category is null)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.NotFound("Document category not found."));
        if (!category.OrganizationId.HasValue)
            return Result<ManagedDocumentCategoryDto>.Failure(Error.Conflict("Legacy tenant-wide categories are read-only to preserve historical references."));
        if (!CanAccessOrganization(managementContext, category.OrganizationId.Value))
            return Result<ManagedDocumentCategoryDto>.Failure(Error.NotFound("Document category not found."));

        if (category.IsActive != isActive)
        {
            var original = new { category.IsActive };
            var changes = new { IsActive = new { from = category.IsActive, to = isActive } };
            category.IsActive = isActive;
            category.UpdatedBy = userId.ToString();
            var saved = await SaveCategoryAndAuditAsync(category, isActive ? "Activate" : "Deactivate", userId,
                original, changes, nameof(category.IsActive), cancellationToken);
            if (saved.IsFailure)
                return Result<ManagedDocumentCategoryDto>.Failure(saved.Error!);
        }

        return Result<ManagedDocumentCategoryDto>.Success(await ToManagedCategoryDtoAsync(
            category, managementContext, [category.OrganizationId.Value], cancellationToken));
    }

    public Task<Result<IReadOnlyList<DocumentOrganizationDto>>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.View))
            return Task.FromResult(Result<IReadOnlyList<DocumentOrganizationDto>>.Failure(Error.Forbidden("Document view permission is required.")));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null)
            return Task.FromResult(Result<IReadOnlyList<DocumentOrganizationDto>>.Failure(Error.Forbidden("A valid tenant and management context are required.")));

        IReadOnlyList<DocumentOrganizationDto> organizations = managementContext.Scope.OrganizationReferences
            .OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(organization => organization.Id)
            .Select(organization => new DocumentOrganizationDto(organization.Id, organization.Name))
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<DocumentOrganizationDto>>.Success(organizations));
    }

    public async Task<Result<DocumentDetailDto>> CreateAsync(DocumentMutationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Create))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Document create permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (dto.File is null)
            return Result<DocumentDetailDto>.Failure(Error.Validation("A file is required."));
        if (!CanUseProject(dto.ProjectId))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Project is outside the current management scope."));
        if (dto.IsTemplate && dto.ProjectId.HasValue)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Organization templates cannot be linked to a project."));
        if (dto.IsTemplate && !HasTemplateManagePermission())
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Template management permission is required."));
        if (dto.IsTemplate && !dto.OrganizationId.HasValue)
            return Result<DocumentDetailDto>.Failure(Error.Validation("An organization is required for an approved template."));
        if (dto.OrganizationId.HasValue && !dto.IsTemplate)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Organization ownership can be assigned only to templates."));
        if (dto.IsTemplate && !IsActiveAuthorizedOrganization(managementContext, dto.OrganizationId!.Value))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("The selected organization is outside the current tenant or management scope."));
        if (dto.IsTemplate && !dto.CategoryId.HasValue)
            return Result<DocumentDetailDto>.Failure(Error.Validation("A document category is required for an approved template."));

        var uploadName = GetSafeFileName(dto.File);
        if (uploadName.Error is not null)
            return Result<DocumentDetailDto>.Failure(uploadName.Error);
        var resolvedName = string.IsNullOrWhiteSpace(dto.Name)
            ? Path.GetFileNameWithoutExtension(uploadName.FileName) ?? string.Empty
            : dto.Name.Trim();
        if (!IsValidDisplayName(resolvedName))
            return Result<DocumentDetailDto>.Failure(Error.Validation("Document name must be between 1 and 200 characters and contain no control characters."));
        if (dto.Description?.Length > 4000)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Document description cannot exceed 4,000 characters."));

        Guid? categoryOrganizationId = dto.IsTemplate ? dto.OrganizationId : null;
        if (!dto.IsTemplate && dto.ProjectId.HasValue
            && managementContext.Scope.ProjectOrganizationIds.TryGetValue(dto.ProjectId.Value, out var categoryProjectOrganizationId))
            categoryOrganizationId = categoryProjectOrganizationId;

        var categoryName = (string?)null;
        var categoryNameAr = (string?)null;
        if (dto.CategoryId.HasValue)
        {
            if (!categoryOrganizationId.HasValue || !CanAccessOrganization(managementContext, categoryOrganizationId.Value))
                return Result<DocumentDetailDto>.Failure(Error.Validation("A document category requires an organization-scoped template or project."));
            var eligibleCategory = await _context.Set<DocumentCategory>().AsNoTracking()
                .FirstOrDefaultAsync(category => category.Id == dto.CategoryId.Value
                    && category.TenantId == managementContext.TenantId
                    && (category.OrganizationId == categoryOrganizationId.Value || category.OrganizationId == null)
                    && category.IsActive
                    && !category.IsDeleted, cancellationToken);
            if (eligibleCategory is null)
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected document category is inactive or unavailable for this organization."));
            categoryName = eligibleCategory.Name;
            categoryNameAr = eligibleCategory.NameAr;
        }

        DocumentVersion? sourceTemplateVersion = null;
        Document? sourceTemplateDocument = null;
        Guid? sourceTemplateId = null;
        if (dto.SourceTemplateId.HasValue || dto.SourceTemplateVersionId.HasValue || dto.SourceTemplateVersionNumber.HasValue)
        {
            if (dto.IsTemplate || !dto.ProjectId.HasValue || !dto.SourceTemplateId.HasValue)
                return Result<DocumentDetailDto>.Failure(Error.Validation("A project and source template are required to record a template reference."));
            if (!managementContext.Scope.ProjectOrganizationIds.TryGetValue(dto.ProjectId.Value, out var projectOrganizationId)
                || !CanAccessOrganization(managementContext, projectOrganizationId))
                return Result<DocumentDetailDto>.Failure(Error.Forbidden("The project organization is outside the current management scope."));

            sourceTemplateDocument = await AccessibleQuery().Include(document => document.Versions)
                .FirstOrDefaultAsync(document => document.Id == dto.SourceTemplateId.Value
                    && document.TenantId == managementContext.TenantId
                    && document.IsTemplate
                    && document.OrganizationId == projectOrganizationId, cancellationToken);
            if (sourceTemplateDocument is null)
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected source template is not available for this project."));

            sourceTemplateVersion = dto.SourceTemplateVersionId.HasValue
                ? sourceTemplateDocument.Versions.FirstOrDefault(version => version.Id == dto.SourceTemplateVersionId.Value
                    && version.TenantId == managementContext.TenantId
                    && version.DocumentId == sourceTemplateDocument.Id
                    && !version.IsDeleted)
                : sourceTemplateDocument.Versions.FirstOrDefault(version => version.VersionNumber == (dto.SourceTemplateVersionNumber ?? sourceTemplateDocument.Version)
                    && version.TenantId == managementContext.TenantId
                    && version.DocumentId == sourceTemplateDocument.Id
                    && !version.IsDeleted);
            if (sourceTemplateVersion is null
                || (dto.SourceTemplateVersionNumber.HasValue && sourceTemplateVersion.VersionNumber != dto.SourceTemplateVersionNumber.Value))
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected source template version is not available."));

            if (sourceTemplateDocument.IsArchived
                && (_archivedProjectTemplateReferenceValidator is null
                    || !await _archivedProjectTemplateReferenceValidator.IsExactPinnedReferenceAsync(
                        managementContext.TenantId,
                        dto.ProjectId.Value,
                        projectOrganizationId,
                        sourceTemplateDocument.Id,
                        sourceTemplateVersion.Id,
                        cancellationToken)))
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected source template is not available for this project."));

            sourceTemplateId = sourceTemplateDocument.Id;
        }

        var expectedTemplateType = sourceTemplateVersion is null
            ? DocumentFileType.Unknown
            : (await DetectStoredVersionTypeAsync(sourceTemplateVersion,
                sourceTemplateVersion.IsCurrent ? sourceTemplateDocument?.StoragePath : null,
                cancellationToken)).DetectedType;
        var upload = await StoreValidatedUploadAsync(dto.File, uploadName.FileName!, cancellationToken, expectedTemplateType);
        if (upload.Error is not null)
            return Result<DocumentDetailDto>.Failure(upload.Error);
        var stored = upload.Stored!;
        var storedFileName = upload.FileName!;
        var contentType = DocumentContentTypes.ForFileName(storedFileName);
        var document = new Document
        {
            TenantId = managementContext.TenantId,
            Name = resolvedName,
            Description = dto.Description?.Trim(),
            ProjectId = dto.ProjectId,
            CategoryId = dto.CategoryId,
            OrganizationId = dto.IsTemplate ? dto.OrganizationId : null,
            IsTemplate = dto.IsTemplate,
            IsMandatory = dto.IsTemplate && dto.IsMandatory,
            Status = "Active",
            Version = 1,
            OwnerId = userId,
            FileName = storedFileName,
            FileType = Path.GetExtension(storedFileName).TrimStart('.').ToUpperInvariant(),
            FileSize = stored.Length,
            ContentType = contentType,
            StoragePath = null,
            SourceTemplateId = sourceTemplateId,
            SourceTemplateVersionNumber = sourceTemplateVersion?.VersionNumber,
            SourceTemplateVersionId = sourceTemplateVersion?.Id,
            CreatedBy = userId.ToString()
        };
        document.Versions.Add(new DocumentVersion
        {
            TenantId = managementContext.TenantId,
            DocumentId = document.Id,
            VersionNumber = 1,
            FileName = storedFileName,
            FileSize = stored.Length,
            ContentType = contentType,
            StoragePath = null,
            StorageProviderId = stored.ProviderId,
            StorageKey = stored.StorageKey,
            Sha256 = stored.Sha256,
            ChangeNotes = dto.ChangeNotes,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            IsCurrent = true,
            CreatedBy = userId.ToString()
        });
        _context.Set<Document>().Add(document);
        var persistence = await SaveAndAuditAsync("Create", document, userId, stored.StorageKey, cancellationToken);
        if (persistence.IsFailure)
            return Result<DocumentDetailDto>.Failure(persistence.Error!);
        return Result<DocumentDetailDto>.Success(ToDetail(document, dto.IsTemplate
            ? managementContext.Scope.OrganizationReferences.FirstOrDefault(organization => organization.Id == dto.OrganizationId)?.Name
            : null, categoryName, categoryNameAr));
    }

    public async Task<Result<DocumentDetailDto>> AddVersionAsync(Guid id, IFormFile file, string? changeNotes, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Edit))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Document edit permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (file is null)
            return Result<DocumentDetailDto>.Failure(Error.Validation("A file is required."));
        var document = await AccessibleQuery().Include(document => document.Versions)
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
        if (document is null)
            return Result<DocumentDetailDto>.Failure(Error.NotFound("Document not found"));
        if (document.IsArchived)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Archived documents cannot receive new versions."));
        if (document.IsTemplate && !HasTemplateManagePermission())
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Template management permission is required."));

        var safeName = GetSafeFileName(file);
        if (safeName.Error is not null)
            return Result<DocumentDetailDto>.Failure(safeName.Error);
        var expectedTemplateType = document.IsTemplate
            ? DocumentFileType.Unknown
            : await GetSourceTemplateTypeAsync(document, managementContext, cancellationToken);
        var upload = await StoreValidatedUploadAsync(file, safeName.FileName!, cancellationToken, expectedTemplateType);
        if (upload.Error is not null)
            return Result<DocumentDetailDto>.Failure(upload.Error);
        var stored = upload.Stored!;
        var storedFileName = upload.FileName!;
        var nextVersion = document.Versions.Count == 0 ? document.Version + 1 : document.Versions.Max(version => version.VersionNumber) + 1;
        foreach (var version in document.Versions)
            version.IsCurrent = false;
        document.Version = nextVersion;
        document.FileName = storedFileName;
        document.FileType = Path.GetExtension(storedFileName).TrimStart('.').ToUpperInvariant();
        document.FileSize = stored.Length;
        document.ContentType = DocumentContentTypes.ForFileName(storedFileName);
        document.StoragePath = null;
        document.UpdatedBy = userId.ToString();
        document.Versions.Add(new DocumentVersion
        {
            TenantId = managementContext.TenantId,
            DocumentId = document.Id,
            VersionNumber = nextVersion,
            FileName = storedFileName,
            FileSize = stored.Length,
            ContentType = DocumentContentTypes.ForFileName(storedFileName),
            StoragePath = null,
            StorageProviderId = stored.ProviderId,
            StorageKey = stored.StorageKey,
            Sha256 = stored.Sha256,
            ChangeNotes = changeNotes,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            IsCurrent = true,
            CreatedBy = userId.ToString()
        });
        var persistence = await SaveAndAuditAsync("NewVersion", document, userId, stored.StorageKey, cancellationToken);
        if (persistence.IsFailure)
            return Result<DocumentDetailDto>.Failure(persistence.Error!);
        var versionCategory = document.CategoryId.HasValue
            ? await _context.Set<DocumentCategory>().AsNoTracking()
                .Where(category => category.Id == document.CategoryId.Value)
                .Select(category => new { category.Name, category.NameAr }).FirstOrDefaultAsync(cancellationToken)
            : null;
        return Result<DocumentDetailDto>.Success(ToDetail(document, GetOrganizationName(document.OrganizationId), versionCategory?.Name, versionCategory?.NameAr));
    }

    public async Task<Result<DocumentDetailDto>> LinkProjectDocumentToTemplateAsync(
        Guid projectId,
        Guid projectDocumentId,
        Guid projectDocumentVersionId,
        Guid templateId,
        Guid templateVersionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Edit))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Document edit permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext)
            || managementContext is null
            || managementContext.UserId != userId)
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("A valid tenant and management context are required."));
        if (projectId == Guid.Empty || projectDocumentId == Guid.Empty || projectDocumentVersionId == Guid.Empty
            || templateId == Guid.Empty || templateVersionId == Guid.Empty)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Project, document, and template version references are required."));
        if (!CanUseProject(projectId)
            || !managementContext.Scope.ProjectOrganizationIds.TryGetValue(projectId, out var projectOrganizationId)
            || !CanAccessOrganization(managementContext, projectOrganizationId))
            return Result<DocumentDetailDto>.Failure(Error.Forbidden("Project is outside the current management scope."));

        var document = await AccessibleQuery().Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == projectDocumentId
                && item.TenantId == managementContext.TenantId
                && item.ProjectId == projectId
                && !item.IsTemplate,
                cancellationToken);
        if (document is null)
            return Result<DocumentDetailDto>.Failure(Error.NotFound("Project document not found."));
        if (document.IsArchived)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Archived project documents cannot satisfy a phase gate requirement."));
        if (document.OrganizationId.HasValue
            || document.ProjectId is not Guid documentProjectId
            || !managementContext.Scope.ProjectOrganizationIds.TryGetValue(documentProjectId, out var documentOrganizationId)
            || documentOrganizationId != projectOrganizationId)
            return Result<DocumentDetailDto>.Failure(Error.Validation("Project document organization does not match the requested project."));

        var projectVersion = document.Versions.FirstOrDefault(version => version.Id == projectDocumentVersionId
            && version.TenantId == managementContext.TenantId
            && !version.IsDeleted);
        if (projectVersion is null)
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected project document version is not available."));
        var projectStorageKey = projectVersion.StorageKey ?? projectVersion.StoragePath
            ?? (projectVersion.IsCurrent ? document.StoragePath : null);
        if (string.IsNullOrWhiteSpace(projectStorageKey)
            || (!string.IsNullOrWhiteSpace(projectVersion.StorageProviderId)
                && !string.Equals(projectVersion.StorageProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)))
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected project document version is unavailable in the configured storage provider."));
        if (string.IsNullOrWhiteSpace(projectVersion.FileName)
            || !AllowedExtensions.Contains(Path.GetExtension(projectVersion.FileName)))
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected project document version has an unsupported file type."));
        try
        {
            var storedMetadata = await _storage.GetMetadataAsync(projectStorageKey, cancellationToken);
            if (storedMetadata is null
                || !string.Equals(storedMetadata.ProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)
                || storedMetadata.Length != projectVersion.FileSize
                || (!string.IsNullOrWhiteSpace(projectVersion.Sha256)
                    && !string.Equals(storedMetadata.Sha256, projectVersion.Sha256, StringComparison.OrdinalIgnoreCase)))
                return Result<DocumentDetailDto>.Failure(Error.Validation("The selected project document file is unavailable."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected project document file is unavailable."));
        }

        var template = await AccessibleQuery().Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == templateId
                && item.TenantId == managementContext.TenantId
                && item.IsTemplate
                && item.OrganizationId == projectOrganizationId,
                cancellationToken);
        if (template is null)
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected template does not belong to this project organization."));

        var templateVersion = template.Versions.FirstOrDefault(version => version.Id == templateVersionId
            && version.TenantId == managementContext.TenantId
            && !version.IsDeleted);
        if (templateVersion is null)
            return Result<DocumentDetailDto>.Failure(Error.Validation("The selected template version is not available."));

        var templateStorageKey = templateVersion.StorageKey ?? templateVersion.StoragePath
            ?? (templateVersion.IsCurrent ? template.StoragePath : null);
        var expectedTemplateType = await DetectStoredVersionTypeAsync(templateVersion, templateStorageKey, cancellationToken);
        var uploadedDocumentType = await DetectStoredVersionTypeAsync(projectVersion, projectStorageKey, cancellationToken);
        if (expectedTemplateType.IsValid && uploadedDocumentType.IsValid
            && !DocumentFileTypeValidator.IsCompatible(expectedTemplateType.DetectedType, uploadedDocumentType))
            return Result<DocumentDetailDto>.Failure(CreateTemplateFileTypeMismatchError(
                expectedTemplateType.DetectedType, uploadedDocumentType.DetectedType));

        var linkedCategory = document.CategoryId.HasValue
            ? await _context.Set<DocumentCategory>().AsNoTracking().Where(category => category.Id == document.CategoryId.Value)
                .Select(category => new { category.Name, category.NameAr }).FirstOrDefaultAsync(cancellationToken)
            : null;
        return Result<DocumentDetailDto>.Success(ToDetail(document, GetOrganizationName(document.OrganizationId), linkedCategory?.Name, linkedCategory?.NameAr));
    }

    public async Task<Result> ArchiveAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Archive))
            return Result.Failure(Error.Forbidden("Document archive permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var document = await AccessibleQuery().FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
        if (document is null)
            return Result.Failure(Error.NotFound("Document not found"));
        if (document.IsTemplate && !HasTemplateManagePermission())
            return Result.Failure(Error.Forbidden("Template management permission is required."));

        document.IsArchived = true;
        document.Status = "Archived";
        document.UpdatedBy = userId.ToString();
        return await SaveAndAuditAsync("Archive", document, userId, null, cancellationToken);
    }

    public async Task<Result> ActivateAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.Edit))
            return Result.Failure(Error.Forbidden("Document edit permission is required."));
        if (!TryGetTrustedManagementContext(out var managementContext) || managementContext is null || managementContext.UserId != userId)
            return Result.Failure(Error.Forbidden("A valid tenant and management context are required."));
        var document = await AccessibleQuery().FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
        if (document is null)
            return Result.Failure(Error.NotFound("Document not found"));
        if (document.IsTemplate && !HasTemplateManagePermission())
            return Result.Failure(Error.Forbidden("Template management permission is required."));

        document.IsArchived = false;
        document.Status = "Active";
        document.UpdatedBy = userId.ToString();
        return await SaveAndAuditAsync("Activate", document, userId, null, cancellationToken);
    }

    public async Task<Result<DocumentDownload>> GetDownloadAsync(Guid id, int? versionNumber, CancellationToken cancellationToken = default)
    {
        if (!HasEffectivePermission(DocumentPermissions.View))
            return Result<DocumentDownload>.Failure(Error.Forbidden("Document view permission is required."));
        var document = await AccessibleQuery().Include(document => document.Versions)
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
        if (document is null)
            return Result<DocumentDownload>.Failure(Error.NotFound("Document not found"));

        var version = versionNumber.HasValue
            ? document.Versions.FirstOrDefault(item => item.VersionNumber == versionNumber.Value)
            : document.Versions.FirstOrDefault(item => item.IsCurrent) ?? document.Versions.OrderByDescending(item => item.VersionNumber).FirstOrDefault();
        var storageKey = version?.StorageKey ?? version?.StoragePath ?? (version?.IsCurrent == true ? document.StoragePath : null);
        if (version is null || string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(version.FileName))
            return Result<DocumentDownload>.Failure(Error.NotFound("Document content not found"));
        if (!string.IsNullOrWhiteSpace(version.StorageProviderId)
            && !string.Equals(version.StorageProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase))
            return Result<DocumentDownload>.Failure(Error.NotFound("Document content not found"));

        try
        {
            if (!await _storage.ExistsAsync(storageKey, cancellationToken))
                return Result<DocumentDownload>.Failure(Error.NotFound("Document content not found"));
            var stream = await _storage.OpenReadAsync(storageKey, cancellationToken);
            return Result<DocumentDownload>.Success(new DocumentDownload(
                stream,
                version.FileName,
                DocumentContentTypes.ForFileName(version.FileName),
                version.FileSize));
        }
        catch (FileNotFoundException)
        {
            return Result<DocumentDownload>.Failure(Error.NotFound("Document content not found"));
        }
    }

    private IQueryable<Document> AccessibleQuery()
    {
        var query = _context.Set<Document>().Where(document => !document.IsDeleted);
        if (!TryGetTrustedManagementContext(out var scope) || scope is null)
            return query.Where(_ => false);

        var projectIds = scope.Scope.ProjectIds.ToArray();
        var organizationIds = scope.Scope.OrganizationReferences.Select(organization => organization.Id).Distinct().ToArray();
        var userId = scope.UserId;
        return query.Where(document =>
            (document.ProjectId.HasValue && projectIds.Contains(document.ProjectId.Value)
                && (!document.IsTemplate || (document.OrganizationId.HasValue && organizationIds.Contains(document.OrganizationId.Value))))
            || (document.IsTemplate && document.OrganizationId.HasValue && organizationIds.Contains(document.OrganizationId.Value))
            || (!document.IsTemplate && !document.OrganizationId.HasValue && !document.ProjectId.HasValue && document.OwnerId == userId));
    }

    private bool CanUseProject(Guid? projectId) =>
        TryGetTrustedManagementContext(out var scope)
        && scope is not null
        && (!projectId.HasValue || scope.Scope.ProjectIds.Contains(projectId.Value));

    private static bool CanAccessOrganization(ManagementContext context, Guid organizationId) =>
        context.Scope.OrganizationIds.Contains(organizationId)
        && context.Scope.OrganizationReferences.Any(organization => organization.Id == organizationId);

    private static bool IsActiveAuthorizedOrganization(ManagementContext context, Guid organizationId) =>
        CanAccessOrganization(context, organizationId);

    private string? GetOrganizationName(Guid? organizationId) =>
        organizationId.HasValue && TryGetTrustedManagementContext(out var context) && context is not null
            ? context.Scope.OrganizationReferences.FirstOrDefault(organization => organization.Id == organizationId.Value)?.Name
            : null;

    private bool HasTemplateManagePermission() => HasEffectivePermission(DocumentPermissions.TemplateManage);

    private bool HasEffectivePermission(string permission)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user is not null
            && user.HasClaim("permission", permission)
            && !user.HasClaim("deny", permission);
    }

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

    private async Task<UploadStoreResult> StoreValidatedUploadAsync(
        IFormFile file,
        string safeFileName,
        CancellationToken cancellationToken,
        DocumentFileType expectedTemplateType = DocumentFileType.Unknown)
    {
        var validation = ValidateFileMetadata(file, safeFileName, _maxUploadBytes);
        if (validation is not null)
            return UploadStoreResult.Failed(validation);

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var extensionAllowed = AllowedExtensions.Contains(extension);
        if (!extensionAllowed && expectedTemplateType == DocumentFileType.Unknown)
            return UploadStoreResult.Failed(CreateUnverifiedFileTypeError());

        await using var source = file.OpenReadStream();
        var prefix = new byte[(int)Math.Min(8, Math.Max(0, file.Length))];
        var prefixLength = 0;
        while (prefixLength < prefix.Length)
        {
            var read = await source.ReadAsync(prefix.AsMemory(prefixLength, prefix.Length - prefixLength), cancellationToken);
            if (read == 0) break;
            prefixLength += read;
        }

        var actualFileType = new DocumentFileTypeDetectionResult(false, DocumentFileType.Unknown);
        if (source.CanSeek)
        {
            source.Position = 0;
            actualFileType = DocumentFileTypeValidator.Detect(source);
            source.Position = prefixLength;
        }
        if (expectedTemplateType != DocumentFileType.Unknown
            && actualFileType.IsValid
            && !DocumentFileTypeValidator.IsCompatible(expectedTemplateType, actualFileType))
            return UploadStoreResult.Failed(CreateTemplateFileTypeMismatchError(
                expectedTemplateType, actualFileType.DetectedType));

        if (!extensionAllowed)
            return UploadStoreResult.Failed(CreateUnverifiedFileTypeError());
        if (!actualFileType.IsValid)
            return UploadStoreResult.Failed(CreateUnverifiedFileTypeError());

        var extensionFileType = GetDocumentFileTypeForExtension(extension);
        if (actualFileType.DetectedType != extensionFileType)
            return UploadStoreResult.Failed(Error.Validation(
                "The detected file type does not match the selected file extension. / نوع الملف المكتشف لا يتوافق مع امتداد الملف المحدد."));

        if (!HasValidSignature(extension, prefix.AsSpan(0, prefixLength)))
            return UploadStoreResult.Failed(Error.Validation("File content does not match the selected document type."));
        if (extension == ".pdf" && !HasValidPdfStructure(source))
            return UploadStoreResult.Failed(Error.Validation("The PDF structure is invalid."));
        if (extension is ".doc" or ".xls")
        {
            if (!HasValidCompoundOfficeHeader(source))
                return UploadStoreResult.Failed(Error.Validation("The legacy Office compound file header is invalid."));
        }
        if (extension is ".docx" or ".xlsx")
        {
            if (!HasValidOpenXmlPackage(source, extension))
                return UploadStoreResult.Failed(Error.Validation("The Office Open XML package is invalid or does not match the selected document type."));
        }

        var prefixStream = new PrefixReplayStream(prefix.AsMemory(0, prefixLength), source);
        var limitedStream = new SizeLimitedReadStream(prefixStream, _maxUploadBytes);
        StoredFile stored;
        try
        {
            stored = await _storage.StoreAsync(limitedStream, extension, cancellationToken);
        }
        catch (UploadLimitExceededException)
        {
            return UploadStoreResult.Failed(Error.Validation($"File size cannot exceed {_maxUploadBytes} bytes."));
        }

        if (stored.Length != file.Length)
        {
            await TryDeleteStorageAsync(stored.StorageKey, CancellationToken.None);
            return UploadStoreResult.Failed(Error.Validation("Uploaded file size did not match the received content."));
        }
        if (!string.Equals(stored.ProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(stored.StorageKey)
            || string.IsNullOrWhiteSpace(stored.Sha256)
            || stored.Sha256.Length != 64
            || stored.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            await TryDeleteStorageAsync(stored.StorageKey, CancellationToken.None);
            return UploadStoreResult.Failed(Error.Validation("Storage provider returned invalid object metadata."));
        }

        return new UploadStoreResult(stored, safeFileName, null);
    }

    private async Task<DocumentFileType> GetSourceTemplateTypeAsync(
        Document document,
        ManagementContext managementContext,
        CancellationToken cancellationToken)
    {
        if (!document.SourceTemplateId.HasValue
            || !document.ProjectId.HasValue
            || !managementContext.Scope.ProjectOrganizationIds.TryGetValue(document.ProjectId.Value, out var projectOrganizationId)
            || !CanAccessOrganization(managementContext, projectOrganizationId))
            return DocumentFileType.Unknown;

        var sourceTemplate = await AccessibleQuery().Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == document.SourceTemplateId.Value
                && item.TenantId == managementContext.TenantId
                && item.IsTemplate
                && item.OrganizationId == projectOrganizationId,
                cancellationToken);
        if (sourceTemplate is null)
            return DocumentFileType.Unknown;

        var sourceVersion = document.SourceTemplateVersionId.HasValue
            ? sourceTemplate.Versions.FirstOrDefault(version => version.Id == document.SourceTemplateVersionId.Value
                && version.TenantId == managementContext.TenantId && !version.IsDeleted)
            : document.SourceTemplateVersionNumber.HasValue
                ? sourceTemplate.Versions.FirstOrDefault(version => version.VersionNumber == document.SourceTemplateVersionNumber.Value
                    && version.TenantId == managementContext.TenantId && !version.IsDeleted)
                : null;
        if (sourceVersion is null)
            return DocumentFileType.Unknown;

        var fallbackStorageKey = sourceVersion.IsCurrent ? sourceTemplate.StoragePath : null;
        var detection = await DetectStoredVersionTypeAsync(sourceVersion, fallbackStorageKey, cancellationToken);
        return detection.IsValid ? detection.DetectedType : DocumentFileType.Unknown;
    }

    private async Task<DocumentFileTypeDetectionResult> DetectStoredVersionTypeAsync(
        DocumentVersion version,
        string? fallbackStorageKey,
        CancellationToken cancellationToken)
    {
        var storageKey = version.StorageKey ?? version.StoragePath ?? fallbackStorageKey;
        if (string.IsNullOrWhiteSpace(storageKey)
            || (!string.IsNullOrWhiteSpace(version.StorageProviderId)
                && !string.Equals(version.StorageProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)))
            return new DocumentFileTypeDetectionResult(false, DocumentFileType.Unknown);

        try
        {
            var metadata = await _storage.GetMetadataAsync(storageKey, cancellationToken);
            if (metadata is null
                || !string.Equals(metadata.ProviderId, _storage.ProviderId, StringComparison.OrdinalIgnoreCase)
                || metadata.Length != version.FileSize
                || (!string.IsNullOrWhiteSpace(version.Sha256)
                    && !string.Equals(metadata.Sha256, version.Sha256, StringComparison.OrdinalIgnoreCase)))
                return new DocumentFileTypeDetectionResult(false, DocumentFileType.Unknown);

            await using var content = await _storage.OpenReadAsync(storageKey, cancellationToken);
            return content.CanSeek
                ? DocumentFileTypeValidator.Detect(content)
                : new DocumentFileTypeDetectionResult(false, DocumentFileType.Unknown);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new DocumentFileTypeDetectionResult(false, DocumentFileType.Unknown);
        }
    }

    private static Error CreateTemplateFileTypeMismatchError(DocumentFileType expectedType, DocumentFileType detectedType)
    {
        var expectedTypeLabel = DocumentFileTypeValidator.GetEnglishLabel(expectedType);
        var expectedTypeLabelAr = DocumentFileTypeValidator.GetArabicLabel(expectedType);
        var detectedTypeLabel = DocumentFileTypeValidator.GetEnglishLabel(detectedType);
        var detectedTypeLabelAr = DocumentFileTypeValidator.GetArabicLabel(detectedType);
        var message = $"The uploaded file type ({detectedTypeLabel}) is incompatible with the required template type ({expectedTypeLabel}). "
            + $"/ نوع الملف المرفوع ({detectedTypeLabelAr}) غير متوافق مع نوع القالب المطلوب ({expectedTypeLabelAr}).";

        return Error.BusinessValidation(
            "DOCUMENT_TEMPLATE_FILE_TYPE_MISMATCH",
            message,
            details: new Dictionary<string, object?>
            {
                ["expectedType"] = expectedTypeLabel,
                ["expectedTypeAr"] = expectedTypeLabelAr,
                ["detectedType"] = detectedTypeLabel,
                ["detectedTypeAr"] = detectedTypeLabelAr
            });
    }

    private static Error CreateUnverifiedFileTypeError() => Error.BusinessValidation(
        "UNSUPPORTED_FILE_TYPE",
        "The uploaded file type could not be verified or is unsupported. / تعذّر التحقق من نوع الملف المرفوع أو أن نوعه غير مدعوم.");

    private static DocumentFileType GetDocumentFileTypeForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => DocumentFileType.Pdf,
        ".doc" => DocumentFileType.Doc,
        ".docx" => DocumentFileType.Docx,
        ".xls" => DocumentFileType.Xls,
        ".xlsx" => DocumentFileType.Xlsx,
        _ => DocumentFileType.Unknown
    };

    private static Error? ValidateFileMetadata(IFormFile file, string safeFileName, long maxFileSize)
    {
        if (file.Length <= 0 || file.Length > maxFileSize)
            return Error.Validation($"File size must be between 1 byte and {maxFileSize} bytes.");

        var suppliedType = file.ContentType?.Split(';', 2)[0].Trim();
        var expectedType = DocumentContentTypes.ForFileName(safeFileName).Split(';', 2)[0].Trim();
        if (!string.IsNullOrWhiteSpace(suppliedType)
            && !string.Equals(suppliedType, expectedType, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(suppliedType, DocumentContentTypes.Fallback, StringComparison.OrdinalIgnoreCase))
            return Error.Validation("File content type does not match the selected file extension.");

        return null;
    }

    private static bool HasValidSignature(string extension, ReadOnlySpan<byte> prefix) => extension.ToLowerInvariant() switch
    {
        ".pdf" => prefix.StartsWith("%PDF-"u8),
        ".doc" or ".xls" => prefix.StartsWith(OleSignature),
        ".docx" or ".xlsx" => prefix.StartsWith("PK\u0003\u0004"u8),
        _ => false
    };

    private static bool HasValidOpenXmlPackage(Stream source, string extension)
    {
        if (!source.CanSeek)
            return false;

        var originalPosition = source.Position;
        try
        {
            source.Position = 0;
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
            var contentTypesEntry = archive.GetEntry("[Content_Types].xml");
            var requiredPart = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? "word/document.xml"
                : "xl/workbook.xml";
            var expectedRoot = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? "document"
                : "workbook";
            var expectedRootNamespaces = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
                    "http://purl.oclc.org/ooxml/wordprocessingml/main"
                }
                : new[]
                {
                    "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
                    "http://purl.oclc.org/ooxml/spreadsheetml/main"
                };
            var expectedContentType = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
            var mainPart = archive.GetEntry(requiredPart);
            if (contentTypesEntry is null || contentTypesEntry.Length <= 0
                || contentTypesEntry.Length > MaximumContentTypesManifestBytes
                || mainPart is null
                || mainPart.Length <= 0
                || mainPart.Length > MaximumOpenXmlMainPartBytes)
                return false;

            using var manifestStream = contentTypesEntry.Open();
            using var reader = XmlReader.Create(manifestStream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumContentTypesManifestBytes,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });
            var manifest = XDocument.Load(reader);
            XNamespace contentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
            var mainPartContentType = manifest.Root?.Elements(contentTypesNamespace + "Override")
                .FirstOrDefault(element => string.Equals(
                    ((string?)element.Attribute("PartName"))?.TrimStart('/'), requiredPart, StringComparison.Ordinal))?
                .Attribute("ContentType")?.Value;
            if (!string.Equals(mainPartContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
                return false;

            using var mainPartStream = mainPart.Open();
            using var mainPartReader = XmlReader.Create(mainPartStream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumOpenXmlMainPartBytes,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });
            var sawExpectedRoot = false;
            while (mainPartReader.Read())
            {
                if (mainPartReader.NodeType == XmlNodeType.Element && !sawExpectedRoot)
                {
                    sawExpectedRoot = mainPartReader.LocalName == expectedRoot
                        && expectedRootNamespaces.Contains(mainPartReader.NamespaceURI, StringComparer.Ordinal);
                    if (!sawExpectedRoot) return false;
                }
            }
            return sawExpectedRoot;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
        finally
        {
            source.Position = originalPosition;
        }
    }

    private static bool HasValidPdfStructure(Stream source)
    {
        if (!source.CanSeek || source.Length < 32)
            return false;

        var originalPosition = source.Position;
        try
        {
            source.Position = 0;
            Span<byte> header = stackalloc byte[8];
            if (!TryReadExactly(source, header)
                || !header[..5].SequenceEqual("%PDF-"u8)
                || header[5] is < (byte)'1' or > (byte)'2'
                || header[6] != (byte)'.'
                || header[7] is < (byte)'0' or > (byte)'9')
                return false;

            var tailLength = (int)Math.Min(source.Length, MaximumPdfIndexBytes);
            source.Position = source.Length - tailLength;
            var tail = new byte[tailLength];
            if (!TryReadExactly(source, tail)) return false;
            var tailText = Encoding.Latin1.GetString(tail);
            var eofIndex = tailText.LastIndexOf("%%EOF", StringComparison.Ordinal);
            if (eofIndex < 0) return false;
            for (var index = eofIndex + 5; index < tail.Length; index++)
            {
                if (tail[index] is not (byte)0 and not (byte)9 and not (byte)10 and not (byte)12 and not (byte)13 and not (byte)32)
                    return false;
            }

            var startXrefIndex = tailText.LastIndexOf("startxref", eofIndex, StringComparison.Ordinal);
            if (startXrefIndex < 0) return false;
            var offsetText = tailText[(startXrefIndex + "startxref".Length)..eofIndex].Trim();
            if (!long.TryParse(offsetText, NumberStyles.None, CultureInfo.InvariantCulture, out var xrefOffset)
                || xrefOffset < 0
                || xrefOffset >= source.Length)
                return false;

            source.Position = xrefOffset;
            var xrefLength = (int)Math.Min(source.Length - xrefOffset, MaximumPdfXrefBytes);
            var xrefBytes = new byte[xrefLength];
            if (!TryReadExactly(source, xrefBytes)) return false;
            var xrefText = Encoding.Latin1.GetString(xrefBytes).TrimStart(' ', '\t', '\r', '\n', '\f', '\0');
            if (xrefText.StartsWith("xref", StringComparison.Ordinal)
                && xrefText.Length > 4
                && char.IsWhiteSpace(xrefText[4]))
                return true;

            // PDFs may store the cross-reference table in a stream object instead of an xref table.
            var objectStart = xrefText.IndexOf(" obj", StringComparison.Ordinal);
            var typeIndex = xrefText.IndexOf("/Type", StringComparison.Ordinal);
            var xrefTypeIndex = typeIndex < 0 ? -1 : xrefText.IndexOf("/XRef", typeIndex, StringComparison.Ordinal);
            var streamIndex = xrefText.IndexOf("stream", StringComparison.Ordinal);
            return objectStart >= 0 && typeIndex > objectStart && xrefTypeIndex > typeIndex && streamIndex > xrefTypeIndex;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            source.Position = originalPosition;
        }
    }

    private static bool HasValidCompoundOfficeHeader(Stream source)
    {
        if (!source.CanSeek || source.Length < 1024)
            return false;

        var originalPosition = source.Position;
        try
        {
            source.Position = 0;
            Span<byte> header = stackalloc byte[512];
            if (!TryReadExactly(source, header) || !header[..OleSignature.Length].SequenceEqual(OleSignature))
                return false;

            var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(header[26..]);
            var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
            var miniSectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
            if (BinaryPrimitives.ReadUInt16LittleEndian(header[24..]) != 0x003e
                || BinaryPrimitives.ReadUInt16LittleEndian(header[28..]) != 0xfffe
                || miniSectorShift != 6
                || (majorVersion == 3 && sectorShift != 9)
                || (majorVersion == 4 && sectorShift != 12)
                || majorVersion is not (3 or 4))
                return false;

            for (var index = 34; index < 40; index++)
            {
                if (header[index] != 0) return false;
            }

            var sectorSize = 1 << sectorShift;
            if (source.Length < sectorSize * 2L || (source.Length - sectorSize) % sectorSize != 0)
                return false;
            var sectorCount = (uint)((source.Length - sectorSize) / sectorSize);
            var directorySectorCount = BinaryPrimitives.ReadUInt32LittleEndian(header[40..]);
            var fatSectorCount = BinaryPrimitives.ReadUInt32LittleEndian(header[44..]);
            var firstDirectorySector = BinaryPrimitives.ReadUInt32LittleEndian(header[48..]);
            var miniStreamCutoff = BinaryPrimitives.ReadUInt32LittleEndian(header[56..]);
            var firstMiniFatSector = BinaryPrimitives.ReadUInt32LittleEndian(header[60..]);
            var miniFatSectorCount = BinaryPrimitives.ReadUInt32LittleEndian(header[64..]);
            var firstDifatSector = BinaryPrimitives.ReadUInt32LittleEndian(header[68..]);
            var difatSectorCount = BinaryPrimitives.ReadUInt32LittleEndian(header[72..]);
            if (fatSectorCount == 0
                || fatSectorCount > sectorCount
                || firstDirectorySector >= sectorCount
                || miniStreamCutoff != 4096
                || (majorVersion == 3 && directorySectorCount != 0)
                || (majorVersion == 4 && (directorySectorCount == 0 || directorySectorCount > sectorCount))
                || (miniFatSectorCount == 0 && firstMiniFatSector is not (EndOfChain or FreeSector))
                || (miniFatSectorCount > 0 && firstMiniFatSector >= sectorCount)
                || (difatSectorCount == 0 && firstDifatSector is not (EndOfChain or FreeSector))
                || (difatSectorCount > 0 && firstDifatSector >= sectorCount))
                return false;

            var inlineFatCount = 0;
            var inlineFatIds = new HashSet<uint>();
            for (var index = 0; index < 109; index++)
            {
                var id = BinaryPrimitives.ReadUInt32LittleEndian(header[(76 + index * sizeof(uint))..]);
                if (id == FreeSector) continue;
                if (id >= sectorCount || !inlineFatIds.Add(id)) return false;
                inlineFatCount++;
            }

            if (inlineFatCount != Math.Min(109, fatSectorCount)
                || (fatSectorCount > 109 && difatSectorCount == 0))
                return false;

            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentOutOfRangeException or OverflowException)
        {
            return false;
        }
        finally
        {
            source.Position = originalPosition;
        }
    }

    private static bool TryReadExactly(Stream source, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = source.Read(buffer[total..]);
            if (read == 0) return false;
            total += read;
        }
        return true;
    }
    private static (string? FileName, Error? Error) GetSafeFileName(IFormFile file)
    {
        var fileName = file.FileName?.Normalize(System.Text.NormalizationForm.FormC);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || fileName is "." or ".."
            || fileName.IndexOfAny(['/', '\\', ':']) >= 0
            || fileName.Any(char.IsControl)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            return (null, Error.Validation("File name is invalid."));
        return (fileName, null);
    }

    private static bool IsValidDisplayName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length <= 200
        && !name.Any(char.IsControl);

    private async Task TryDeleteStorageAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch
        {
            // Preserve the original database/storage exception; reconciliation is handled by audit/ops.
        }
    }

    private static DocumentListItemDto ToListItem(Document document, string? categoryName, string? categoryNameAr, string? organizationName) => new(
        document.Id,
        document.Name,
        document.Description,
        document.FileName,
        document.FileType,
        document.FileSize,
        DocumentContentTypes.ForFileName(document.FileName),
        document.ProjectId,
        document.CategoryId,
        categoryName,
        categoryNameAr,
        document.IsArchived ? "Archived" : "Active",
        document.Version,
        document.OwnerId,
        document.IsTemplate,
        document.IsMandatory,
        document.IsArchived,
        document.SourceTemplateId,
        document.SourceTemplateVersionNumber,
        document.CreatedAt,
        document.UpdatedAt,
        document.OrganizationId,
        organizationName,
        document.SourceTemplateVersionId);

    private static ManagedDocumentCategoryDto ToManagedCategoryDto(DocumentCategory category, int usageCount) => new(
        category.Id,
        category.OrganizationId,
        category.Name,
        category.NameAr,
        category.Description,
        category.Code,
        category.IsActive,
        usageCount,
        category.CreatedAt,
        category.UpdatedAt,
        category.OrganizationId.HasValue);

    private async Task<ManagedDocumentCategoryDto> ToManagedCategoryDtoAsync(
        DocumentCategory category,
        ManagementContext managementContext,
        Guid[] organizationIds,
        CancellationToken cancellationToken)
    {
        var usage = await GetCategoryUsageCountsAsync([category.Id], managementContext, organizationIds, cancellationToken);
        return ToManagedCategoryDto(category, usage.GetValueOrDefault(category.Id));
    }

    private async Task<Dictionary<Guid, int>> GetCategoryUsageCountsAsync(
        Guid[] categoryIds,
        ManagementContext managementContext,
        Guid[] organizationIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds.Length == 0 || organizationIds.Length == 0)
            return new Dictionary<Guid, int>();

        var projectIds = managementContext.Scope.ProjectOrganizationIds
            .Where(item => organizationIds.Contains(item.Value))
            .Select(item => item.Key)
            .Distinct()
            .ToArray();
        var usageQuery = _context.Set<Document>().IgnoreQueryFilters().AsNoTracking()
            .Where(document => document.TenantId == managementContext.TenantId
                && !document.IsDeleted
                && document.CategoryId.HasValue
                && categoryIds.Contains(document.CategoryId.Value)
                && ((document.OrganizationId.HasValue && organizationIds.Contains(document.OrganizationId.Value))
                    || (document.ProjectId.HasValue && projectIds.Contains(document.ProjectId.Value))));
        return await usageQuery
            .GroupBy(document => document.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);
    }

    private async Task<bool> CategoryNameExistsAsync(
        Guid tenantId,
        Guid organizationId,
        string normalizedNameEn,
        string normalizedNameAr,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        await _context.Set<DocumentCategory>().AsNoTracking().AnyAsync(category =>
            category.TenantId == tenantId
            && category.OrganizationId == organizationId
            && !category.IsDeleted
            && (!excludingId.HasValue || category.Id != excludingId.Value)
            && (category.NormalizedNameEn == normalizedNameEn || category.NormalizedNameAr == normalizedNameAr),
            cancellationToken);

    private static CategoryNameNormalization NormalizeCategoryNames(string? nameEn, string? nameAr)
    {
        var displayNameEn = NormalizeDisplayName(nameEn);
        if (displayNameEn is null)
            return CategoryNameNormalization.Invalid(Error.Validation("English category name is required and must be 1 to 200 characters without control characters.", "nameEn"));
        var displayNameAr = NormalizeDisplayName(nameAr);
        if (displayNameAr is null)
            return CategoryNameNormalization.Invalid(Error.Validation("Arabic category name is required and must be 1 to 200 characters without control characters.", "nameAr"));

        return new CategoryNameNormalization(
            displayNameEn,
            displayNameAr,
            displayNameEn.Normalize(NormalizationForm.FormKC).ToUpperInvariant(),
            displayNameAr.Normalize(NormalizationForm.FormKC).ToUpperInvariant(),
            null);
    }

    private static string? NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = string.Join(' ', value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length is < 1 or > 200 || normalized.Any(char.IsControl) ? null : normalized;
    }

    private async Task<Result> SaveCategoryAndAuditAsync(
        DocumentCategory category,
        string action,
        Guid userId,
        object? originalValues,
        object? newValues,
        string changedFields,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            var audit = await _audit.LogAsync(new AuditEntry
            {
                TenantId = category.TenantId,
                EntityType = "DocumentCategory",
                EntityId = category.Id,
                Action = action,
                UserId = userId,
                OriginalValues = originalValues is null ? null : System.Text.Json.JsonSerializer.Serialize(originalValues),
                NewValues = newValues is null ? null : System.Text.Json.JsonSerializer.Serialize(newValues),
                ChangedFields = changedFields,
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId.ToString()
            }, cancellationToken);
            if (audit.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return audit;
            }
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result.Failure(Error.Conflict("The document category changed during this request. Reload it and try again."));
        }
        catch (DbUpdateException exception) when (IsCategoryDuplicateException(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result.Failure(Error.Conflict("A category with the same English or Arabic name already exists in this organization."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool IsCategoryDuplicateException(DbUpdateException exception)
    {
        var message = exception.GetBaseException().Message;
        return message.Contains("UX_DocumentCategories_Organization_NameEn", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UX_DocumentCategories_Organization_NameAr", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CategoryNameNormalization(
        string? NameEn,
        string? NameAr,
        string? NameEnKey,
        string? NameArKey,
        Error? Error)
    {
        public static CategoryNameNormalization Invalid(Error error) => new(null, null, null, null, error);
    }

    private static DocumentDetailDto ToDetail(Document document, string? organizationName, string? categoryName, string? categoryNameAr = null) => new(
        ToListItem(document, categoryName, categoryNameAr, organizationName),
        document.Versions.OrderByDescending(version => version.VersionNumber)
            .Select(version => new DocumentVersionDto(version.Id, version.VersionNumber, version.FileName, version.FileSize,
                DocumentContentTypes.ForFileName(version.FileName), version.ChangeNotes, version.UploadedBy, version.UploadedAt, version.IsCurrent))
            .ToList());

    private async Task<Result> SaveAndAuditAsync(
        string action,
        Document document,
        Guid userId,
        string? storedKey,
        CancellationToken cancellationToken,
        object? originalValues = null,
        object? changes = null,
        string? changedFields = null)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var auditResult = await LogAsync(action, document, userId, cancellationToken, originalValues, changes, changedFields);
            if (auditResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(storedKey))
                    await TryDeleteStorageAsync(storedKey, CancellationToken.None);
                return auditResult;
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch
        {
            if (transaction is not null)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(storedKey))
                await TryDeleteStorageAsync(storedKey, CancellationToken.None);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<Result> SaveAndAuditInCurrentTransactionAsync(
        string action,
        Document document,
        Guid userId,
        CancellationToken cancellationToken,
        object? originalValues = null,
        object? changes = null,
        string? changedFields = null)
    {
        // Requirement-aware Project Management operations own the transaction so document source
        // linkage, evidence pinning, and the audit record commit or roll back together.
        await _context.SaveChangesAsync(cancellationToken);
        return await LogAsync(action, document, userId, cancellationToken, originalValues, changes, changedFields);
    }

    private Task<Result> LogAsync(
        string action,
        Document document,
        Guid userId,
        CancellationToken cancellationToken,
        object? originalValues = null,
        object? changes = null,
        string? changedFields = null)
    {
        var values = new
        {
            document.Name,
            document.Version,
            document.IsTemplate,
            document.OrganizationId,
            Status = document.IsArchived ? "Archived" : "Active",
            document.SourceTemplateId,
            document.SourceTemplateVersionId,
            document.SourceTemplateVersionNumber
        };
        return _audit.LogAsync(new AuditEntry
        {
            EntityType = document.IsTemplate ? "DocumentTemplate" : "Document",
            EntityId = document.Id,
            Action = action,
            ProjectId = document.ProjectId,
            UserId = userId,
            OriginalValues = originalValues is null ? null : System.Text.Json.JsonSerializer.Serialize(originalValues),
            NewValues = System.Text.Json.JsonSerializer.Serialize(values),
            Changes = changes is null ? null : System.Text.Json.JsonSerializer.Serialize(changes),
            ChangedFields = changedFields,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        }, cancellationToken);
    }

    private sealed record UploadStoreResult(StoredFile? Stored, string? FileName, Error? Error)
    {
        public static UploadStoreResult Failed(Error error) => new(null, null, error);
    }

    private sealed class UploadLimitExceededException : IOException { }

    private sealed class PrefixReplayStream : Stream
    {
        private readonly ReadOnlyMemory<byte> _prefix;
        private readonly Stream _source;
        private int _prefixOffset;

        public PrefixReplayStream(ReadOnlyMemory<byte> prefix, Stream source)
        {
            _prefix = prefix;
            _source = source;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var prefixCount = CopyPrefix(buffer);
            return prefixCount > 0 ? prefixCount : _source.Read(buffer);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var prefixCount = CopyPrefix(buffer.Span);
            return prefixCount > 0 ? prefixCount : await _source.ReadAsync(buffer, cancellationToken);
        }

        private int CopyPrefix(Span<byte> destination)
        {
            var remaining = _prefix.Length - _prefixOffset;
            if (remaining <= 0 || destination.Length == 0) return 0;
            var count = Math.Min(remaining, destination.Length);
            _prefix.Span.Slice(_prefixOffset, count).CopyTo(destination);
            _prefixOffset += count;
            return count;
        }
    }

    private sealed class SizeLimitedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _limit;
        private long _read;

        public SizeLimitedReadStream(Stream inner, long limit)
        {
            _inner = inner;
            _limit = limit;
            _read = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            var remaining = _limit - _read;
            if (remaining <= 0)
            {
                Span<byte> probe = stackalloc byte[1];
                if (_inner.Read(probe) > 0) throw new UploadLimitExceededException();
                return 0;
            }

            var count = _inner.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
            _read += count;
            return count;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return 0;
            var remaining = _limit - _read;
            if (remaining <= 0)
            {
                var probe = new byte[1];
                if (await _inner.ReadAsync(probe.AsMemory(), cancellationToken) > 0) throw new UploadLimitExceededException();
                return 0;
            }

            var count = await _inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], cancellationToken);
            _read += count;
            return count;
        }
    }
}
