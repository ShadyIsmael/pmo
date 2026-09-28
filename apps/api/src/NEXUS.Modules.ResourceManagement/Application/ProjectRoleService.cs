using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class ProjectRoleService : IProjectRoleService
{
    private const string Category = "ProjectRole";
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public ProjectRoleService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Result<IReadOnlyList<LookupDto>>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.Set<Lookup>()
            .Where(l => l.Category == Category && l.IsActive && !l.IsDeleted)
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.Code)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LookupDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<IReadOnlyList<LookupDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.Set<Lookup>()
            .Where(l => l.Category == Category && !l.IsDeleted)
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.Code)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<LookupDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<LookupDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _context.Set<Lookup>()
            .FirstOrDefaultAsync(l => l.Id == id && l.Category == Category && !l.IsDeleted, cancellationToken);
        if (item is null) return Result<LookupDto>.Failure(Error.NotFound("Project Role not found"));
        return Result<LookupDto>.Success(ToDto(item));
    }

    public async Task<Result<LookupDto>> CreateAsync(CreateLookupDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var normalizedCode = dto.Code.Trim().ToUpperInvariant();
        var duplicate = await _context.Set<Lookup>().AnyAsync(l =>
            l.Category == Category && l.Code.ToUpper() == normalizedCode && !l.IsDeleted, cancellationToken);
        if (duplicate) return Result<LookupDto>.Failure(Error.Conflict("A Project Role with this code already exists"));

        var lookup = new Lookup
        {
            Category = Category,
            Code = dto.Code.Trim(),
            Name = dto.NameEn,
            NameEn = dto.NameEn,
            NameAr = dto.NameAr,
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
            Action = "CreateProjectRole",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        return Result<LookupDto>.Success(ToDto(lookup));
    }

    public async Task<Result<LookupDto>> UpdateAsync(Guid id, UpdateLookupDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var lookup = await _context.Set<Lookup>()
            .FirstOrDefaultAsync(l => l.Id == id && l.Category == Category && !l.IsDeleted, cancellationToken);
        if (lookup is null) return Result<LookupDto>.Failure(Error.NotFound("Project Role not found"));

        if (dto.Code != null)
        {
            var normalizedCode = dto.Code.Trim().ToUpperInvariant();
            var duplicate = await _context.Set<Lookup>().AnyAsync(l =>
                l.Id != id && l.Category == Category && l.Code.ToUpper() == normalizedCode && !l.IsDeleted, cancellationToken);
            if (duplicate) return Result<LookupDto>.Failure(Error.Conflict("A Project Role with this code already exists"));
            lookup.Code = dto.Code.Trim();
        }

        if (dto.NameEn != null) { lookup.NameEn = dto.NameEn; lookup.Name = dto.NameEn; }
        if (dto.NameAr != null) lookup.NameAr = dto.NameAr;
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
            Action = "UpdateProjectRole",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        return Result<LookupDto>.Success(ToDto(lookup));
    }

    public async Task<Result> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var lookup = await _context.Set<Lookup>()
            .FirstOrDefaultAsync(l => l.Id == id && l.Category == Category && !l.IsDeleted, cancellationToken);
        if (lookup is null) return Result.Failure(Error.NotFound("Project Role not found"));

        lookup.IsActive = false;
        lookup.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Lookup",
            EntityId = lookup.Id,
            Action = "DeactivateProjectRole",
            UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

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
