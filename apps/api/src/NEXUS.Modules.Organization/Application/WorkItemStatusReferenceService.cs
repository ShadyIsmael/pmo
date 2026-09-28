using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.Organization.Application;

public sealed class SaveWorkItemStatusRequest
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Color { get; set; }
    public bool IsDefault { get; set; }
}

public interface IWorkItemStatusReferenceService
{
    Task<IReadOnlyList<WorkItemStatusReference>> GetAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<Result<WorkItemStatusReference>> CreateAsync(SaveWorkItemStatusRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<WorkItemStatusReference>> UpdateAsync(Guid id, SaveWorkItemStatusRequest request, string userId, CancellationToken cancellationToken = default);
}

public sealed class WorkItemStatusReferenceService : IWorkItemStatusReferenceService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWorkItemStatusUsageLookup _usageLookup;

    public WorkItemStatusReferenceService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IWorkItemStatusUsageLookup usageLookup)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _usageLookup = usageLookup;
    }

    public async Task<IReadOnlyList<WorkItemStatusReference>> GetAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Lookup>()
            .Where(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted);
        if (!includeInactive) query = query.Where(lookup => lookup.IsActive);

        var statuses = await query
            .OrderBy(lookup => lookup.DisplayOrder)
            .ThenBy(lookup => lookup.Code)
            .ToListAsync(cancellationToken);
        return statuses.Select(ToReference).ToList();
    }

    public async Task<Result<WorkItemStatusReference>> CreateAsync(SaveWorkItemStatusRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request, out var code, out var nameEn, out var nameAr, out var color);
        if (validation is not null) return Result<WorkItemStatusReference>.Failure(validation);

        var tenantId = GetCurrentTenantId();
        var duplicate = await _context.Set<Lookup>().IgnoreQueryFilters().AnyAsync(lookup => lookup.TenantId == tenantId && lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted && lookup.Code == code, cancellationToken);
        if (duplicate) return Result<WorkItemStatusReference>.Failure(Error.Conflict("A work item status with this code already exists"));

        var hasDefault = await _context.Set<Lookup>().AnyAsync(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && lookup.IsActive && lookup.IsDefault, cancellationToken);
        var status = new Lookup
        {
            TenantId = tenantId,
            Category = ReferenceDataCategories.WorkItemStatus,
            Code = code,
            Name = nameEn,
            NameEn = nameEn,
            NameAr = nameAr,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            Color = color,
            IsDefault = request.IsActive && (request.IsDefault || !hasDefault),
            CreatedBy = userId
        };

        if (status.IsDefault) await ClearDefaultsAsync(tenantId, null, cancellationToken);
        _context.Set<Lookup>().Add(status);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkItemStatusReference>.Success(ToReference(status));
    }

    public async Task<Result<WorkItemStatusReference>> UpdateAsync(Guid id, SaveWorkItemStatusRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request, out var code, out var nameEn, out var nameAr, out var color);
        if (validation is not null) return Result<WorkItemStatusReference>.Failure(validation);

        var status = await _context.Set<Lookup>().FirstOrDefaultAsync(lookup => lookup.Id == id && lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted, cancellationToken);
        if (status is null) return Result<WorkItemStatusReference>.Failure(Error.NotFound("Work item status not found"));

        var duplicate = await _context.Set<Lookup>().IgnoreQueryFilters().AnyAsync(lookup => lookup.Id != id && lookup.TenantId == status.TenantId && lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted && lookup.Code == code, cancellationToken);
        if (duplicate) return Result<WorkItemStatusReference>.Failure(Error.Conflict("A work item status with this code already exists"));

        var isUsed = await _usageLookup.IsCodeUsedAsync(status.Code, cancellationToken);
        if (isUsed && !string.Equals(status.Code, code, StringComparison.Ordinal)) return Result<WorkItemStatusReference>.Failure(Error.Conflict("The code of a status already used by work items cannot be changed"));
        if (isUsed && !request.IsActive && status.IsActive) return Result<WorkItemStatusReference>.Failure(Error.Conflict("A status currently used by work items cannot be deactivated"));
        var isUsedByPublishedWorkflow = await _context.Set<WorkflowDefinitionVersion>()
            .AnyAsync(version => version.TenantId == status.TenantId
                && version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published
                && !version.IsDeleted
                && version.WorkflowDefinition.EntityType == "WorkItem"
                && version.WorkflowDefinition.ActivePublishedVersionId == version.Id
                && version.States.Any(state => state.Code == status.Code && state.IsActive && !state.IsDeleted), cancellationToken);
        if (isUsedByPublishedWorkflow && !string.Equals(status.Code, code, StringComparison.Ordinal)) return Result<WorkItemStatusReference>.Failure(Error.Conflict("The code of a status used by the published Work Item workflow cannot be changed"));
        if (isUsedByPublishedWorkflow && !request.IsActive && status.IsActive) return Result<WorkItemStatusReference>.Failure(Error.Conflict("A status used by the published Work Item workflow cannot be deactivated"));
        if (!request.IsActive && status.IsActive)
        {
            var activeCount = await _context.Set<Lookup>().CountAsync(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && lookup.IsActive && !lookup.IsDeleted, cancellationToken);
            if (activeCount <= 1) return Result<WorkItemStatusReference>.Failure(Error.Validation("At least one active work item status is required"));
        }

        status.Code = code;
        status.Name = nameEn;
        status.NameEn = nameEn;
        status.NameAr = nameAr;
        status.DisplayOrder = request.DisplayOrder;
        status.IsActive = request.IsActive;
        status.Color = color;
        status.IsDefault = request.IsActive && request.IsDefault;
        status.UpdatedBy = userId;

        if (status.IsDefault) await ClearDefaultsAsync(status.TenantId, status.Id, cancellationToken);
        else if (!status.IsActive) status.IsDefault = false;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkItemStatusReference>.Success(ToReference(status));
    }

    private async Task ClearDefaultsAsync(Guid tenantId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var defaults = await _context.Set<Lookup>().Where(lookup => lookup.TenantId == tenantId && lookup.Category == ReferenceDataCategories.WorkItemStatus && lookup.IsDefault && (!exceptId.HasValue || lookup.Id != exceptId.Value)).ToListAsync(cancellationToken);
        foreach (var status in defaults) status.IsDefault = false;
    }

    private Guid GetCurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenantId) ? tenantId : TenantConstants.DemoTenantId;

    private static Error? Validate(SaveWorkItemStatusRequest request, out string code, out string nameEn, out string nameAr, out string? color)
    {
        code = request.Code.Trim();
        nameEn = request.NameEn.Trim();
        nameAr = request.NameAr.Trim();
        color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim();
        if (code.Length == 0 || code.Length > 50 || code.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.'))) return Error.Validation("Status code must contain only letters, numbers, hyphens, underscores, or dots");
        if (nameEn.Length == 0 || nameEn.Length > 200) return Error.Validation("English status name is required");
        if (nameAr.Length == 0 || nameAr.Length > 200) return Error.Validation("Arabic status name is required");
        if (color is not null && color.Length > 50) return Error.Validation("Status color is too long");
        return null;
    }

    private static WorkItemStatusReference ToReference(Lookup status) => new(status.Id, status.Code, status.NameEn, status.NameAr, status.DisplayOrder, status.IsActive, status.Color, status.IsDefault);
}
