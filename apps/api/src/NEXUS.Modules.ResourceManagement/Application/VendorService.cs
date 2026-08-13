using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class VendorService : IVendorService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public VendorService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<PagedResult<VendorDto>> GetPagedAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Vendor>().Where(v => !v.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(term) ||
                (v.ContactName != null && v.ContactName.ToLower().Contains(term)) ||
                (v.Category != null && v.Category.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<VendorDto>(items.Select(v => v.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<Result<VendorDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result<VendorDto>.Failure(Error.NotFound("Vendor not found"));
        return Result<VendorDto>.Success(vendor.ToDto());
    }

    public async Task<Result<VendorDto>> CreateAsync(CreateVendorDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var vendor = dto.ToEntity();
        vendor.CreatedBy = userId;

        _context.Set<Vendor>().Add(vendor);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Create",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<VendorDto>.Success(vendor.ToDto());
    }

    public async Task<Result<VendorDto>> UpdateAsync(Guid id, UpdateVendorDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result<VendorDto>.Failure(Error.NotFound("Vendor not found"));

        vendor.ApplyUpdate(dto);
        vendor.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Update",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<VendorDto>.Success(vendor.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, cancellationToken);
        if (vendor is null)
            return Result.Failure(Error.NotFound("Vendor not found"));

        vendor.IsDeleted = true;
        vendor.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Vendor",
            EntityId = vendor.Id,
            Action = "Delete",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }
}
