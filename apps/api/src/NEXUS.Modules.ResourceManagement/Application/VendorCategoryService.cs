using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class VendorCategoryService : IVendorCategoryService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public VendorCategoryService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Result<IReadOnlyList<LookupDto>>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var items = await Query()
            .Where(l => l.IsActive)
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.Code)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LookupDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<IReadOnlyList<LookupDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await Query()
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.Code)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LookupDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<LookupDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await Query().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (item is null) return Result<LookupDto>.Failure(Error.NotFound("Vendor category not found"));
        return Result<LookupDto>.Success(ToDto(item));
    }

    public async Task<Result<LookupDto>> CreateAsync(CreateLookupDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return Result<LookupDto>.Failure(Error.Validation("Vendor category code is required"));
        if (string.IsNullOrWhiteSpace(dto.NameEn))
            return Result<LookupDto>.Failure(Error.Validation("Vendor category name is required"));

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();
        var duplicate = await Query().AnyAsync(l => l.Code.ToUpper() == normalizedCode, cancellationToken);
        if (duplicate) return Result<LookupDto>.Failure(Error.Conflict("A vendor category with this code already exists"));

        var lookup = new Lookup
        {
            Category = ReferenceDataCategories.VendorCategory,
            Code = dto.Code.Trim(),
            Name = dto.NameEn.Trim(),
            NameEn = dto.NameEn.Trim(),
            NameAr = dto.NameAr?.Trim() ?? string.Empty,
            Description = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            Color = dto.Color,
            CreatedBy = userId
        };

        _context.Set<Lookup>().Add(lookup);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Lookup",
            EntityId = lookup.Id,
            Action = "CreateVendorCategory",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        return Result<LookupDto>.Success(ToDto(lookup));
    }

    public async Task<Result<LookupDto>> UpdateAsync(Guid id, UpdateLookupDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var lookup = await Query().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lookup is null) return Result<LookupDto>.Failure(Error.NotFound("Vendor category not found"));

        if (dto.Code != null)
        {
            var normalizedCode = dto.Code.Trim().ToUpperInvariant();
            var duplicate = await Query().AnyAsync(l =>
                l.Id != id && l.Code.ToUpper() == normalizedCode, cancellationToken);
            if (duplicate) return Result<LookupDto>.Failure(Error.Conflict("A vendor category with this code already exists"));
            lookup.Code = dto.Code.Trim();
        }

        if (dto.NameEn != null) { lookup.NameEn = dto.NameEn.Trim(); lookup.Name = lookup.NameEn; }
        if (dto.NameAr != null) lookup.NameAr = dto.NameAr.Trim();
        if (dto.Description != null) lookup.Description = dto.Description;
        if (dto.DisplayOrder.HasValue) lookup.DisplayOrder = dto.DisplayOrder.Value;
        if (dto.IsActive.HasValue) lookup.IsActive = dto.IsActive.Value;
        if (dto.Color != null) lookup.Color = dto.Color;
        lookup.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Lookup",
            EntityId = lookup.Id,
            Action = "UpdateVendorCategory",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        return Result<LookupDto>.Success(ToDto(lookup));
    }

    public async Task<Result> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var lookup = await Query().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lookup is null) return Result.Failure(Error.NotFound("Vendor category not found"));

        lookup.IsActive = false;
        lookup.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Lookup",
            EntityId = lookup.Id,
            Action = "DeactivateVendorCategory",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    private IQueryable<Lookup> Query() =>
        _context.Set<Lookup>().Where(l => l.Category == ReferenceDataCategories.VendorCategory && !l.IsDeleted);

    private static LookupDto ToDto(Lookup l) => new()
    {
        Id = l.Id,
        Code = l.Code,
        NameEn = l.NameEn,
        NameAr = l.NameAr,
        Description = l.Description,
        DisplayOrder = l.DisplayOrder,
        IsActive = l.IsActive,
        Color = l.Color
    };
}
